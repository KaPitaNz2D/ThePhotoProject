# Terrain Foliage Batching Investigation — Level_Prototype

**Scene:** `Assets/[04]Level/Environment/Level_Prototype.unity`
**Unity version:** 6000.3.19f1 (URP)
**Branch:** `art/Environment`
**Date:** 2026-08-12 – 2026-08-13

## Summary

`Level_Prototype` was rendering 3000–4000 batches at only ~600k triangles — an unusually low tris/batch ratio that pointed at many small, uninstanced draw calls rather than heavy geometry. Root-caused to Unity Terrain's native tree renderer never engaging GPU instancing for the project's tree prototypes, regardless of configuration. Fixed with a custom `Graphics.RenderMeshInstanced`-based renderer that bypasses Terrain's tree system entirely, cutting batches by **~89%** with confirmed real GPU instancing. Two follow-on correctness bugs (stale prototype cache, missing LOD-bias handling) were found and fixed after initial deployment; a separate, unrelated data issue (an edited `LODGroup` threshold on one tree prefab) was also discovered and flagged.

---

## 1. Initial diagnosis

### 1.1 Scene structure

The `.unity` scene file itself is nearly empty — 29 `GameObject`s total, 10 of which are `Terrain` tiles (9 inactive/unused, 1 active). No hand-placed props account for meaningful batch count. This ruled out static/dynamic batching settings and pointed at procedurally-placed content invisible in the Hierarchy: terrain-painted vegetation, stored in binary `TerrainData` assets rather than as scene `GameObject`s.

### 1.2 Vegetation source

Found one `VegetationSpawner` component (third-party asset, `Assets/[06]ThirdParty/VegetationSpawner`) on the active terrain, configured with:

```
instanceCount: 2033        (SugarMaple_Modeling_011 tree prototype)
maxMeshTrees: 50            → maps to Terrain.treeMaximumFullLODCount
```

`VegetationSpawner` places trees via `TerrainData.SetTreeInstances` — i.e. Unity's native terrain tree system, not spawned `GameObject`s. Grass/detail layers were unconfigured (`grassPrefabs: []`), so foliage batching was isolated to tree rendering.

### 1.3 Static-analysis pass (pre-MCP)

Before a live Unity MCP connection was available, inspected project files directly:

- `SugarMaple_Modeling_011.prefab`: `LODGroup` with 3 LOD levels, 2 materials per level (`DemoTree_Trunk`, `PlaceHolder_Leaf`).
- `DemoTree_Trunk.mat`: `Universal Render Pipeline/Lit`, `m_EnableInstancingVariants: 1` (GPU Instancing on).
- `PlaceHolder_Leaf.mat`: custom `Shader Graphs/QuadToBillboard` shader graph, `m_EnableInstancingVariants: 0` (GPU Instancing **off**).
- `ProjectSettings.asset`: `m_StaticBatching: 0` for Standalone (project-wide static batching disabled — not the primary cause, since terrain trees don't use static batching anyway).

Initial hypothesis: leaf material's missing instancing flag was blocking batching. User enabled it — **no effect on batch count**, disproving the hypothesis and motivating a live-editor investigation.

---

## 2. Live investigation (Unity MCP)

With an active Unity Editor MCP connection, replaced file-based speculation with direct measurement via `UnityEditor.UnityStats` (reflection) and targeted, reversible experiments.

### 2.1 Terrain component settings (ground truth)

Read live `SerializedObject` state on the `Terrain` component:

| Setting | Value |
|---|---|
| `m_DrawInstanced` | `True` (user had enabled it) |
| `m_BakeLightProbesForTrees` | `False` (user had disabled it — this and `Draw Instanced` are mutually exclusive per Unity docs) |
| `m_TreeMaximumFullLODCount` | 50 |
| `m_EnableTreesAndDetailsRayTracing` | `False` (ruled out RT acceleration structures as a blocker) |

All settings were already correct per Unity's documented requirements for tree GPU instancing. Batches remained unchanged.

### 2.2 Empirical isolation

Rather than continuing to reason from settings, measured directly by toggling one variable at a time and re-reading `UnityStats` after forcing a render (`manage_camera` screenshot):

| Test | batches | drawCalls | triangles | `instancedBatches` |
|---|---|---|---|---|
| Baseline (trees on) | 3855 | 3855 | 601,824 | **0** |
| `Terrain.drawTreesAndFoliage = false` | 71 | 71 | 46,786 | 0 |
| `reflectionProbeUsage = Off` | 3855 | 3855 | 601,824 | 0 |
| Leaf material shader swapped to stock `URP/Lit` | 3855 | 3855 | 601,824 | 0 |
| Tree instance count reduced to 5 (out of view) | 71 | 71 | 46,786 | 0 |

Findings:
- **Trees account for 98% of batches** (3784/3855) and 92% of triangles — confirmed by direct enable/disable, not inference.
- `batches == drawCalls` in every test, and the ratio is consistently **~1.9 draws per visible tree** (trunk + leaf submesh, one draw each) — the signature of zero draw-call combination, not merely "few large batches."
- `instancedBatches` stayed at `0` across every configuration change tested, including a full shader swap to a known-instancing-compatible stock shader. This ruled out: the custom shader graph, reflection probes, light probes (already off), the `drawInstanced` flag itself, and material-level instancing settings.
- `GraphicsSettings.useScriptableRenderPipelineBatching = True` and `GPUResidentDrawerMode = Instanced Drawing` were both already active at the project level — not a project-wide instancing gate either.

### 2.3 Conclusion

With every documented instancing prerequisite satisfied and instancing still never engaging, root-caused to a structural limitation rather than a configuration issue: **Unity's Terrain tree renderer does not GPU-instance non-SpeedTree tree prototypes** (plain `LODGroup`-based prefabs), independent of `drawInstanced`, material flags, or shader. This is consistent with Unity 6's rewritten terrain rendering path (`QuadTreeBatchNode.Render`, visible in Frame Debugger output) and with the Terrain inspector's own disclaimer that several tree LOD settings "have no effect on SpeedTree trees" — implying the reverse configuration (LODGroup-driven, non-SpeedTree) is the one actually gated.

Confirmed no SpeedTree asset (`.st9`/`.spm`) existed anywhere in the project to serve as a control group; both active tree prototypes import via `PrefabImporter`, not `SpeedTreeImporter`.

---

## 3. Interim mitigation — instance count reduction

Before building a permanent fix, reduced `VegetationSpawner` tree density as a stopgap (adjusting per-type spacing):

| | Before | After |
|---|---|---|
| Tree instances | 2033 | 568 (477 + 91, two prototypes) |
| Batches | 3855 | 1222 |
| Triangles | 601,824 | 331,598 |
| `instancedBatches` | 0 | 0 (unresolved — linear scaling, not a real fix) |

Draw-call math confirmed the scaling was still purely linear: `(1222 − 71 baseline) / 568 trees ≈ 2.03 draws/tree` — identical per-object cost to the 2033-tree case. This was explicitly communicated as a temporary budget lever, not a resolution, since any future tile with similar tree density would reproduce the original batch count.

Work at this stage (material reorganization, new tree variant, `VegetationSpawner` package integration, terrain settings, baked lighting data) was committed: `7d99326` — *"Add tree vegetation via VegetationSpawner and tune terrain batching."*

---

## 4. Permanent fix — custom GPU-instanced tree renderer

### 4.1 Decision

Two paths were viable for real instancing:

1. **Re-author trees in SpeedTree Modeler** — the path Unity's Terrain system is actually built around, but requires a separate external GUI application not reachable via Editor scripting/MCP automation.
2. **Bypass Terrain's native tree renderer** with a custom instanced draw path — fully achievable via Editor scripting.

Chose (2): fully scriptable, verifiable immediately, and decoupled from Terrain's internal (and apparently non-functional, for this asset type) instancing decision entirely.

### 4.2 Implementation

New component: [`Assets/[02]Code/Rendering/InstancedTreeRenderer.cs`](../Assets/[02]Code/Rendering/InstancedTreeRenderer.cs), `[ExecuteAlways]`, `[RequireComponent(typeof(Terrain))]`.

**Data source:** reads `Terrain.terrainData.treeInstances` directly — the same data `VegetationSpawner` already writes via `SetTreeInstances`. No change to the vegetation placement pipeline.

**Render path:** for each rendering camera (`RenderPipelineManager.beginCameraRendering`):

1. Skip `CameraType.Preview` / `CameraType.Reflection` cameras.
2. For each tree instance: compute world position (`terrain.transform.position + Vector3.Scale(instance.position, terrainData.size)`), cull by distance and by camera frustum (`GeometryUtility.TestPlanesAABB`).
3. Select an LOD level by replicating `LODGroup`'s own screen-relative-height formula rather than flat world-space distance (see §6 for a correction made to this formula after initial deployment).
4. Compute each instance's world matrix from `TreeInstance.position/rotation/widthScale/heightScale`, combined with each render element's *local* transform relative to the prefab root (root's own world position is intentionally excluded — Terrain's tree placement treats prototype root transform as a pivot only, not an offset).
5. Bucket instances by `(Mesh, submeshIndex, Material)` and submit via `Graphics.RenderMeshInstanced`, chunked at the 1023-instance-per-call API limit.

`terrain.drawTreesAndFoliage` is set to `false` on enable to prevent double-rendering against the native path.

### 4.3 Bugs found and fixed during implementation

| Issue | Cause | Fix |
|---|---|---|
| `CS1615: Argument 1 may not be passed with the 'ref' keyword` | `Graphics.RenderMeshInstanced<T>(List<T>)` overload resolution did not match the `ref RenderParams` signature reported by reflection for this call shape | Removed `ref`; the non-ref call resolves correctly |
| `InvalidOperationException: Material needs to enable instancing` at runtime | `Material.001` — SugarMaple002's trunk material — is **embedded inside the FBX**, not a standalone asset; runtime `enableInstancing = true` changes to embedded sub-assets don't survive reimport/don't reliably persist | Extracted to a real asset, [`SugarMaple002_Trunk.mat`](../Assets/[01]Art/Materials/PlaceHolder/SugarMaple002_Trunk.mat), with instancing enabled |
| Trunk mesh silently disappeared from render after extraction | `ModelImporter.AddRemap` targeting the embedded material's `SourceAssetIdentifier` did not correctly rebind the mesh's material slot on reimport, leaving `Truck_LOD0`/`Truck_LOD1` renderers with an **empty** `sharedMaterials` array | Bypassed the FBX-level remap; assigned the extracted material directly to the affected renderers via `PrefabUtility.EditPrefabContentsScope` |
| Triangle count spiked to **1.12M** (vs. 319k native baseline) after instancing started working | Initial LOD selection used flat world-space distance bands. A wide/zoomed-out camera (e.g. Scene View) has many trees within a generous distance threshold at high screen-magnification-equivalent, so flat distance thresholds selected high-poly LODs where native *screen-relative* LOD selection would have dropped to billboards | Replaced with a `LODGroup`-matching screen-relative-height formula; reduced to **421k** |
| Stale cached `Mesh`/`Material` references throwing `MissingReferenceException` during debug introspection | `RebuildPrototypeCache()` needed manual re-invocation after any prefab/material asset change during testing | Turned out to be a real production bug, not just a testing inconvenience — see §5 |

### 4.4 Results (at time of initial deployment)

Measured identically to §2.2 (same camera, same `UnityStats` reflection read), against the 568-tree post-mitigation scene state:

| Metric | Native rendering | Custom instanced renderer | Δ |
|---|---|---|---|
| Batches | 1208 | **132** | **−89%** |
| `drawCalls` | 1213 | 132 | −89% |
| `instancedBatches` | 0 | **60** | real instancing confirmed |
| `instancedBatchedDrawCalls` | 0 | 1320 | ~1320 tree-element draws folded into 60 GPU-instanced calls |
| Triangles | 319,380 | 421,604 | +32% (see §4.3, resolved further in §6) |
| `setPassCalls` | 54 | 46 | — |

Visual correctness spot-checked via `Main Camera` screenshots at each stage; tree placement, rotation, and scale match native rendering. Pre-existing placeholder-art artifacts (a floating white UI panel, checkerboard terrain texture) were confirmed present in the *native-rendering baseline* screenshot too — unrelated to this change.

---

## 5. Post-deployment fix — stale prototype cache

**Symptom reported by user:** newly added trees (via either `VegetationSpawner` or Unity's native **Paint Trees** tool — both write to the same `TerrainData.treePrototypes`/`treeInstances`) rendered the wrong prefab.

**Root cause:** `InstancedTreeRenderer` built its `prototypeIndex → prefab render data` lookup once in `OnEnable()` and never refreshed it. Adding, removing, or reordering tree prototypes at runtime (through either tool) left the renderer mapping tree instances against outdated prefab data — a `TreeInstance.prototypeIndex` that used to point at prefab A could now point at prefab B, and the renderer would keep drawing A.

**Confirmed live:** read the active terrain's live prototype/instance counts against the component's cached `prototypeCache` — found `treePrototypes.Length == 0` on the terrain but the renderer still had `1` stale cached entry.

**Fix:** added `ComputePrototypeSignature()` — a cheap hash of prototype count + each prefab's `GetInstanceID()` — checked every camera render (`RebuildPrototypeCacheIfChanged()`). The full cache only rebuilds when the signature actually changes, so both `VegetationSpawner` edits and native Paint-Trees edits are picked up automatically with no manual trigger.

**Verified live:** added a tree prototype + instance directly to a terrain with 0/0 state, forced a render, and confirmed the cache went from 0 → 1 cached prototype with no manual `RebuildPrototypeCache()` call — the bug that previously required manual intervention throughout testing (§4.3, last row) was the same bug the user hit.

---

## 6. Post-deployment fix — missing `QualitySettings.lodBias`

**Symptom reported by user:** LOD transition distances from the custom renderer were noticeably shorter than expected — trees dropped to lower-detail LODs too close to the camera.

**Root cause:** Unity's actual LOD selection multiplies the computed screen-relative height by `QualitySettings.lodBias` before comparing against each LOD's threshold — higher bias keeps full detail visible out to *greater* distances. This project's "PC" quality level has `lodBias = 2`. The renderer's LOD formula (§4.2 step 3) never included this factor, so its effective relative height was half of what native `LODGroup` rendering would compute for the same distance — LODs downgraded at roughly half the correct distance.

**Fix:**
```csharp
float relativeHeight = (lodGroupSize * instance.widthScale * 0.5f * QualitySettings.lodBias)
                      / (distanceToCamera * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
```
`lodBias` is read once per camera render (not per instance) for efficiency.

**Investigation note — a red herring ruled out along the way:** while chasing this, `LODGroup.GetLODs()[].screenRelativeTransitionHeight` was read live from `SugarMaple_Modeling_011.prefab` and returned values roughly half of what had been recorded earlier in the same session (e.g. LOD0: `0.249` vs. an earlier `0.5004828`). This looked like it might explain the bug on its own (a dynamically bias-adjusted threshold), but `git diff` showed it was a **real, uncommitted edit to the prefab's `LODGroup` component** — unrelated to `lodBias` or to this renderer. See §7.

---

## 7. Discovered (not caused by this work): uncommitted `LODGroup` edit on `SugarMaple_Modeling_011.prefab`

While investigating §6, found this uncommitted diff against the last commit (`7d99326`):

```diff
   m_LODs:
-  - screenRelativeHeight: 0.5004828
+  - screenRelativeHeight: 0.249
-  - screenRelativeHeight: 0.2040185
+  - screenRelativeHeight: 0.09750359
-  - screenRelativeHeight: 0.0769
+  - screenRelativeHeight: 0.05263936
```

All three LOD thresholds are roughly halved. `InstancedTreeRenderer` only *reads* `LODGroup` data — it never writes to it — so this was not introduced by this work. Most likely cause: the `LODGroup` inspector has draggable percentage handles that are easy to bump by accident while clicking around the component.

Because native rendering reads the same `LODGroup` data, this change affects native-rendered instances of this prefab too, not just the custom renderer — halved thresholds mean the tree must appear twice as large on screen before Unity is willing to show LOD0/LOD1, i.e. every LOD switches sooner regardless of rendering path.

**Status: unresolved, pending confirmation.** Not reverted — could be an intentional tuning change. Flagged to the user; can revert to the last-committed values (`0.5004828` / `0.2040185` / `0.0769`) on request.

---

## 8. Known limitations / follow-ups

1. **Per-instance color/wind tint is not replicated.** `TreeInstance.color` / `.lightmapColor` (health/wind variation baked in by `VegetationSpawner`) are ignored — every instance of a prototype renders with the material's base color. Would require per-instance data via a `StructuredBuffer` and custom shader support to restore.
2. **No shadow-cascade-specific handling.** Shadow casting is enabled per `RenderParams.shadowCastingMode`, but cascade-specific culling/LOD is not separately tuned — acceptable at current tree counts, worth re-checking if density increases substantially.
3. **`InstancedTreeRenderer` is currently only attached to the single active terrain tile.** Several other terrain tiles were found to have their own tree prototypes/instances (from native Paint-Trees testing) but remain inactive; if any are activated, the component needs to be attached there too (drop-in `[RequireComponent(typeof(Terrain))]`, no per-tile configuration required).
4. **Bypasses Terrain's native tree tooling.** The in-editor "paint trees" brush and Terrain's own LOD/billboard crossfade settings no longer apply to rendering (though `VegetationSpawner`'s procedural placement is unaffected, since it writes to `TerrainData` directly). Intentional trade-off given `VegetationSpawner` already owns placement.
5. **SpeedTree conversion remains the "native-path" alternative** if long-term needs include Terrain's built-in wind system, in-editor tree painting, or removing the custom renderer as a maintenance burden. Requires the external SpeedTree Modeler application; out of scope for automated tooling.
6. **§7's LODGroup edit is still open** — pending user confirmation on whether to revert.

---

## 9. Files changed

- **Added:** `Assets/[02]Code/Rendering/InstancedTreeRenderer.cs` — custom GPU-instanced tree renderer; auto-refreshing prototype cache (§5); `lodBias`-corrected LOD selection (§6)
- **Added:** `Assets/[01]Art/Materials/PlaceHolder/SugarMaple002_Trunk.mat` — extracted from embedded FBX material, GPU Instancing enabled
- **Modified:** `Assets/[01]Art/Meshes/Enviroment/Placeholder/SugarMaple002_Modeling_004.prefab` — trunk renderers repointed to extracted material
- **Modified:** `Assets/[01]Art/Meshes/Enviroment/Placeholder/SugarMaple002_Modeling_004.fbx.meta` — external material remap record
- **Modified:** `Assets/[04]Level/Environment/Level_Prototype.unity` — `InstancedTreeRenderer` added to active `Terrain`
- **Modified:** `Assets/[04]Level/Environment/Terrain/New Terrain.asset` — terrain state from testing (tree prototype/instance changes)
- **Not staged / unresolved:** `Assets/[01]Art/Meshes/Enviroment/Placeholder/SugarMaple_Modeling_011.prefab` — uncommitted `LODGroup` threshold edit, see §7
- Prior commit `7d99326`: `VegetationSpawner` package integration, tree variants, terrain settings tuning, instance-count reduction (see §3)
