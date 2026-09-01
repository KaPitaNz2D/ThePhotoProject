# Branch Summary: `art/Environment`

7 commits ahead of `main` · 352 files changed, +49,897 / -4,371

## Overview

This branch builds out the game's environment/terrain art pipeline end-to-end: a
multi-tile terrain, procedural vegetation placement, a custom GPU-instanced tree
renderer (to work around a Unity Terrain batching limitation), placeholder tree/
foliage assets, and a dedicated scene for building/authoring those assets.

## Commit-by-commit

### 1. `dd34363` — Add Ref And Simpler Deform Terrain
Initial scaffolding: added a `[07]Ref` folder with reference heightmap/map images
and a first pass terrain asset plus a `Level_Prototype` scene.

### 2. `20fa5b4` — Add Placeholder and real Asset
- Replaced the single terrain with a **10-tile terrain grid** (~500×500 each) for
  large-scale rendering performance testing.
- Added **PuffBall** foliage mesh set (7 model variants + LODs, material, prefabs).
- Added a **QuadToBillboard** shader graph for billboard/cross-fade foliage LODs.
- Added a placeholder tree (`SugarMaple_Modeling_011`) with LOD group.
- Added `PlayerSprint` (shift-to-sprint, optional stamina) and a matching input
  action; minor `PlayerMovement` tweak.
- Updated URP/quality/graphics settings and volume profile for the new terrain
  and foliage rendering needs.

### 3. `7d99326` — Add tree vegetation via VegetationSpawner and tune terrain batching
- Integrated the third-party **VegetationSpawner** package for scripted terrain
  vegetation placement.
- Added a second tree variant (`SugarMaple002`), `TruckMat`, and a maple leaf
  texture; reorganized placeholder materials under `Materials/PlaceHolder/` and
  switched them to the new billboard shader.
- Populated the terrain with tree instances via VegetationSpawner; enabled
  `Draw Instanced` / disabled `Bake Light Probes For Trees` on all tiles.
- Reduced spawned tree count (2033 → 568) as an interim fix after finding that
  native Terrain GPU instancing doesn't engage for this (non-SpeedTree) tree
  prototype — batches ~3855 → ~1220, triangles ~602k → ~332k. Root cause noted as
  unresolved at this point.
- Baked lightmaps/occlusion data; auto-upgraded terrain layers.

### 4. `c62f579` — Add custom GPU-instanced tree renderer to fix Terrain batching
Root-caused and fixed the batching issue from the previous commit:
- Added **`InstancedTreeRenderer`** ([Assets/[02]Code/Rendering/InstancedTreeRenderer.cs](Assets/[02]Code/Rendering/InstancedTreeRenderer.cs)),
  which bypasses Terrain's native tree renderer entirely and draws tree instances
  via `Graphics.RenderMeshInstanced`, replicating `LODGroup`'s screen-relative-
  height LOD selection. Result: batches cut ~89% (1208 → 132), with real GPU
  instancing confirmed (`instancedBatches` 0 → 60).
- Fixed a stale-prototype-cache bug (new/reordered tree types now auto-detected
  instead of cached once on enable).
- Fixed a missing `QualitySettings.lodBias` term in the LOD formula that was
  causing LOD transitions at roughly half the correct distance.
- Extracted `SugarMaple002`'s trunk material out of the embedded FBX material
  (embedded sub-asset edits don't survive reimport reliably).
- Documented the full investigation in [Docs/terrain-batching-investigation.md](Docs/terrain-batching-investigation.md).
- Pulled in VegetationSpawner's `_Demo` sample content (demo models/materials/
  terrain/scene) alongside the package.

### 5. `4733d09` — Update Leaf Shadergraph
Iterated on the `QuadToBillboard` shader graph and leaf texture; added a
`TestGrass` prefab; minor tweaks to `InstancedTreeRenderer` and the terrain
asset/scene.

### 6. `0f8b68f` — Update Asset And Remove Asset
- Added a **Pine Tree** asset with 4 variants and 2 LODs.
- Added new ground textures.
- Authored a new map design based on **Baxter State Park / Kidney Lake**
  reference terrain.
- Removed the now-unused original terrain asset.

### 7. `9cc3adb` — Recategorized Asset file and Add New Scene for "Asset Maker"
- Reorganized assets into clearer categories: **Fungus**, **Woods**, **Tree**.
- Added a new **`Enviroment_Maker`** scene ([Assets/[04]Level/AsssetMaker/Enviroment_Maker.unity](Assets/[04]Level/AsssetMaker/Enviroment_Maker.unity))
  dedicated to prefab/asset creation and in-game "Asset Maker" tooling.

## Key new systems/files

- [Assets/[02]Code/Rendering/InstancedTreeRenderer.cs](Assets/[02]Code/Rendering/InstancedTreeRenderer.cs) — custom GPU-instanced tree
  renderer, the core technical fix of this branch.
- [Assets/[06]ThirdParty/VegetationSpawner/](Assets/[06]ThirdParty/VegetationSpawner) — third-party vegetation placement package.
- [Assets/[02]Code/Shader/QuadToBillboard.shadergraph](Assets/[02]Code/Shader/QuadToBillboard.shadergraph) — billboard/cross-fade foliage
  shader.
- [Assets/[04]Level/Environment/Level_Prototype.unity](Assets/[04]Level/Environment/Level_Prototype.unity) — main environment prototype
  scene (terrain, trees, foliage, lighting bakes).
- [Assets/[04]Level/AsssetMaker/Enviroment_Maker.unity](Assets/[04]Level/AsssetMaker/Enviroment_Maker.unity) — new scene for authoring
  prefabs/assets.
- [Docs/terrain-batching-investigation.md](Docs/terrain-batching-investigation.md) — write-up of the terrain batching
  investigation and fix.
- `Assets/[02]Code/Script/Player/PlayerSprint.cs` — sprint mechanic added mid-branch.

## Known loose end

Noted in the `c62f579` commit message: an unrelated, uncommitted `LODGroup`
threshold edit was found on `SugarMaple_Modeling_011.prefab` during the
investigation — not caused by this work, left unstaged pending confirmation.
Worth checking whether it's still pending.
