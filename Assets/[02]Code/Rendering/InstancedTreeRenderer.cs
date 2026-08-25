using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Terrain's native tree renderer does not GPU-instance non-SpeedTree tree
// prototypes (confirmed empirically: instancedBatches stays 0 regardless of
// drawInstanced / material instancing settings). This bypasses it entirely,
// drawing terrain tree instances via Graphics.RenderMeshInstanced.
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class InstancedTreeRenderer : MonoBehaviour
{
    [Tooltip("Untick to fully turn off this custom renderer: it stops drawing trees and stops touching the render pipeline, and Terrain's native tree rendering takes back over.")]
    [SerializeField] private bool enableCustomRenderer = true;
    [SerializeField] private bool disableNativeTreeDrawing = true;
    [SerializeField] private float cullDistance = 300f;
    [SerializeField] private float cullRadius = 6f;
    [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;

    private Terrain terrain;
    private static readonly Plane[] FrustumPlanesArray = new Plane[6];

    private struct RenderElement
    {
        public Mesh mesh;
        public int submeshIndex;
        public Material material;
        public Matrix4x4 localMatrix;
    }

    private class PrototypeInfo
    {
        public List<RenderElement>[] lodLevels;
        // Screen-relative-height thresholds per LOD level (mirrors LODGroup.GetLODs()[].screenRelativeTransitionHeight).
        // Null when the prototype has no LODGroup — falls back to flat distance culling for that prototype.
        public float[] screenRelativeThresholds;
        public float lodGroupSize;
    }

    private readonly Dictionary<int, PrototypeInfo> prototypeCache = new Dictionary<int, PrototypeInfo>();
    private readonly Dictionary<(Mesh, int, Material), List<Matrix4x4>> buckets =
        new Dictionary<(Mesh, int, Material), List<Matrix4x4>>();
    private static readonly List<Matrix4x4> ChunkScratch = new List<Matrix4x4>(1023);

    // Tree prototypes can change at any time — VegetationSpawner editing its tree list, or
    // Unity's native "Paint Trees" tool adding/removing/reordering prototypes — with no event
    // to hook. Re-checking a cheap signature every camera render (instead of only caching once
    // in OnEnable) keeps the renderer from drawing a stale prefab at the wrong prototype index.
    private int cachedPrototypeSignature = int.MinValue;

    // The signature above only sees WHICH prefab sits at each prototype index. Editing the prefab
    // itself (swapping a material, replacing a mesh, adding an LOD) leaves both the array and the
    // prefab's instance ID untouched, so the cached meshes/materials would go stale. In the editor,
    // asset-change notifications force a rebuild; see the UNITY_EDITOR block below.
    private bool cacheDirty;

    private static readonly List<InstancedTreeRenderer> ActiveRenderers = new List<InstancedTreeRenderer>();

    // Dev-time hook: a separate TreeLODDebugController toggles this to force every terrain's
    // trees to their highest-detail LOD, for reviewing environment art without distance-based
    // LOD switching getting in the way. Static so one controller affects every terrain tile.
    public static bool DebugForceHighestLOD;

    // Both must survive domain reloads (script recompiles, play mode toggles): OnEnable runs
    // again after every reload for an already-enabled component, and a plain non-serialized
    // field would forget that treeDistance was already zeroed out. Without the persisted flag,
    // a reload while suppressed re-captures originalTreeDistance from the already-zeroed value,
    // permanently losing the real original and leaving trees invisible even after this
    // component is disabled or removed.
    [SerializeField, HideInInspector] private float originalTreeDistance;
    [SerializeField, HideInInspector] private bool nativeTreeDrawingSuppressed;

    private void OnEnable()
    {
        terrain = GetComponent<Terrain>();
        ActiveRenderers.Add(this);
        RebuildPrototypeCache();
        ApplyEnabledState();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ActiveRenderers.Remove(this);
        RestoreNativeTreeDrawing();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Reacts to the enableCustomRenderer/disableNativeTreeDrawing checkboxes being toggled
        // in the Inspector, which does not by itself trigger OnEnable/OnDisable.
        if (!isActiveAndEnabled) return;
        if (terrain == null) terrain = GetComponent<Terrain>();
        ApplyEnabledState();
    }
#endif

    private void ApplyEnabledState()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

        if (enableCustomRenderer)
        {
            // Terrain.drawTreesAndFoliage gates BOTH trees and grass/detail objects together —
            // turning it off would silently disable grass rendering too. Suppress only native
            // TREE rendering via treeDistance = 0 (grass uses the separate detailObjectDistance),
            // so this renderer stays compatible with terrain grass/detail layers.
            if (disableNativeTreeDrawing) SuppressNativeTreeDrawing();
            else RestoreNativeTreeDrawing();

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
        else
        {
            RestoreNativeTreeDrawing();
        }
    }

    private void SuppressNativeTreeDrawing()
    {
        if (terrain == null || nativeTreeDrawingSuppressed) return;
        originalTreeDistance = terrain.treeDistance;
        terrain.treeDistance = 0f;
        nativeTreeDrawingSuppressed = true;
    }

    private void RestoreNativeTreeDrawing()
    {
        if (!nativeTreeDrawingSuppressed || terrain == null) return;
        terrain.treeDistance = originalTreeDistance;
        nativeTreeDrawingSuppressed = false;
    }

    private int ComputePrototypeSignature()
    {
        var prototypes = terrain.terrainData.treePrototypes;
        unchecked
        {
            int sig = 17;
            sig = sig * 31 + prototypes.Length;
            for (int i = 0; i < prototypes.Length; i++)
            {
                var prefab = prototypes[i].prefab;
                sig = sig * 31 + (prefab != null ? prefab.GetInstanceID() : 0);
            }
            return sig;
        }
    }

    private void RebuildPrototypeCacheIfChanged()
    {
        int sig = ComputePrototypeSignature();
        if (sig == cachedPrototypeSignature && !cacheDirty) return;
        cachedPrototypeSignature = sig;
        cacheDirty = false;
        RebuildPrototypeCache();
    }

    [ContextMenu("Rebuild Tree Cache")]
    public void InvalidateCache()
    {
        cacheDirty = true;
    }

    public void RebuildPrototypeCache()
    {
        prototypeCache.Clear();
        if (terrain == null || terrain.terrainData == null) return;

        var prototypes = terrain.terrainData.treePrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            var prefab = prototypes[i].prefab;
            if (prefab == null) continue;

            var info = new PrototypeInfo();
            var lodGroup = prefab.GetComponent<LODGroup>();

            if (lodGroup != null)
            {
                var lods = lodGroup.GetLODs();
                info.lodLevels = new List<RenderElement>[lods.Length];
                info.screenRelativeThresholds = new float[lods.Length];
                info.lodGroupSize = lodGroup.size;
                for (int l = 0; l < lods.Length; l++)
                {
                    info.lodLevels[l] = BuildElements(lods[l].renderers, prefab.transform);
                    info.screenRelativeThresholds[l] = lods[l].screenRelativeTransitionHeight;
                }
            }
            else
            {
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                info.lodLevels = new[] { BuildElements(renderers, prefab.transform) };
            }

            prototypeCache[i] = info;
        }
    }

    // Matrix relative to the prefab ROOT, not just the renderer's immediate parent — a renderer
    // nested under an intermediate transform (or the root itself) can carry scale/rotation that
    // r.transform.localPosition/localRotation/localScale alone would miss. Native Terrain tree
    // rendering bakes the full prefab hierarchy including the root's own scale, so prototypes
    // like Pine_Tree_004 (root localScale 2,2,2) would render undersized here otherwise.
    private static List<RenderElement> BuildElements(Renderer[] renderers, Transform prefabRoot)
    {
        var list = new List<RenderElement>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var localMatrix = prefabRoot.worldToLocalMatrix * r.transform.localToWorldMatrix;
            var mats = r.sharedMaterials;
            var mesh = mf.sharedMesh;
            int submeshCount = Mathf.Min(mesh.subMeshCount, mats.Length);
            for (int sm = 0; sm < submeshCount; sm++)
            {
                if (mats[sm] == null) continue;
                list.Add(new RenderElement
                {
                    mesh = mesh,
                    submeshIndex = sm,
                    material = mats[sm],
                    localMatrix = localMatrix
                });
            }
        }
        return list;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (terrain == null || terrain.terrainData == null) return;
        if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;

        // Prefab mode (and any other isolated stage) renders a preview scene through a normal
        // SceneView camera, and Camera.scene is set to that preview scene. Graphics.RenderMeshInstanced
        // ignores scene membership, so without this the terrain's trees would be drawn on top of the
        // prefab being edited. A camera with an invalid Camera.scene renders everything — leave it alone.
        var camScene = cam.scene;
        if (camScene.IsValid() && camScene != gameObject.scene) return;

        DrawTrees(cam);
    }

    private void DrawTrees(Camera cam)
    {
        RebuildPrototypeCacheIfChanged();
        foreach (var list in buckets.Values) list.Clear();

        var camPos = cam.transform.position;
        var terrainPos = terrain.transform.position;
        var terrainSize = terrain.terrainData.size;
        var instances = terrain.terrainData.treeInstances;
        float halfAngleTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float lodBias = QualitySettings.lodBias;

        GeometryUtility.CalculateFrustumPlanes(cam, FrustumPlanesArray);

        foreach (var inst in instances)
        {
            if (!prototypeCache.TryGetValue(inst.prototypeIndex, out var info) || info.lodLevels == null || info.lodLevels.Length == 0)
                continue;

            var worldPos = terrainPos + Vector3.Scale(inst.position, terrainSize);
            float dist = Vector3.Distance(camPos, worldPos);
            if (dist > cullDistance || dist < 0.001f) continue;

            float radius = Mathf.Max(inst.widthScale, inst.heightScale) * cullRadius;
            if (!GeometryUtility.TestPlanesAABB(FrustumPlanesArray, new Bounds(worldPos, Vector3.one * radius * 2f)))
                continue;

            int lod;
            if (DebugForceHighestLOD)
            {
                lod = 0;
            }
            else if (info.screenRelativeThresholds != null)
            {
                // Mirrors LODGroup's own screen-relative-height selection so a wide/orthographic
                // or zoomed-out camera drops to lower LODs exactly like native Terrain rendering did,
                // instead of a flat world-distance cutoff that over-renders full mesh at any zoom level.
                // QualitySettings.lodBias scales the effective screen-relative height Unity compares
                // against LOD thresholds (e.g. PC quality = 2 here) — omitting it made every LOD
                // transition happen at roughly half the distance native LODGroup rendering would.
                float relativeHeight = (info.lodGroupSize * inst.widthScale * 0.5f * lodBias) / (dist * halfAngleTan);

                lod = -1;
                for (int l = 0; l < info.screenRelativeThresholds.Length; l++)
                {
                    if (relativeHeight >= info.screenRelativeThresholds[l]) { lod = l; break; }
                }
                if (lod < 0) continue; // below the smallest LOD's threshold -> Terrain would cull this tree too
            }
            else
            {
                lod = 0;
            }

            var elements = info.lodLevels[lod];
            if (elements == null) continue;

            var treeMatrix = Matrix4x4.TRS(
                worldPos,
                Quaternion.Euler(0f, inst.rotation * Mathf.Rad2Deg, 0f),
                new Vector3(inst.widthScale, inst.heightScale, inst.widthScale));

            for (int e = 0; e < elements.Count; e++)
            {
                var el = elements[e];
                var key = (el.mesh, el.submeshIndex, el.material);
                if (!buckets.TryGetValue(key, out var matrixList))
                {
                    matrixList = new List<Matrix4x4>();
                    buckets[key] = matrixList;
                }
                matrixList.Add(treeMatrix * el.localMatrix);
            }
        }

        var bounds = new Bounds(terrainPos + terrainSize * 0.5f, terrainSize + Vector3.one * 50f);

        foreach (var kvp in buckets)
        {
            var matrices = kvp.Value;
            if (matrices.Count == 0) continue;

            var (mesh, submeshIndex, material) = kvp.Key;
            var rp = new RenderParams(material)
            {
                layer = gameObject.layer,
                worldBounds = bounds,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = true,
                camera = cam
            };

            for (int start = 0; start < matrices.Count; start += 1023)
            {
                int count = Mathf.Min(1023, matrices.Count - start);
                ChunkScratch.Clear();
                for (int i = 0; i < count; i++) ChunkScratch.Add(matrices[start + i]);
                Graphics.RenderMeshInstanced(rp, mesh, submeshIndex, ChunkScratch);
            }
        }
    }

#if UNITY_EDITOR
    private static void InvalidateAllCaches()
    {
        for (int i = 0; i < ActiveRenderers.Count; i++)
        {
            if (ActiveRenderers[i] != null) ActiveRenderers[i].cacheDirty = true;
        }
    }

    // Covers prefab edits that reach disk: exiting/saving prefab mode, model reimports,
    // material asset saves. Cheap enough to invalidate unconditionally — the rebuild only
    // walks the handful of tree prototypes on this terrain.
    private class TreeAssetWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            InvalidateAllCaches();
        }
    }

    // Covers edits that have NOT hit disk yet — tweaking a material or a prefab's components
    // straight from the Inspector, where the change is live in memory before any save.
    [InitializeOnLoadMethod]
    private static void HookEditorChangeEvents()
    {
        ObjectChangeEvents.changesPublished -= OnObjectChangesPublished;
        ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
    }

    private static void OnObjectChangesPublished(ref ObjectChangeEventStream stream)
    {
        if (ActiveRenderers.Count == 0) return;
        for (int i = 0; i < stream.length; i++)
        {
            var kind = stream.GetEventType(i);
            if (kind == ObjectChangeKind.ChangeAssetObjectProperties ||
                kind == ObjectChangeKind.UpdatePrefabInstances)
            {
                InvalidateAllCaches();
                return;
            }
        }
    }
#endif
}
