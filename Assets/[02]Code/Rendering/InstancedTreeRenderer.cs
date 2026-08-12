using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Terrain's native tree renderer does not GPU-instance non-SpeedTree tree
// prototypes (confirmed empirically: instancedBatches stays 0 regardless of
// drawInstanced / material instancing settings). This bypasses it entirely,
// drawing terrain tree instances via Graphics.RenderMeshInstanced.
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class InstancedTreeRenderer : MonoBehaviour
{
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

    private void OnEnable()
    {
        terrain = GetComponent<Terrain>();
        RebuildPrototypeCache();
        if (disableNativeTreeDrawing) terrain.drawTreesAndFoliage = false;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
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
        if (sig == cachedPrototypeSignature) return;
        cachedPrototypeSignature = sig;
        RebuildPrototypeCache();
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
                    info.lodLevels[l] = BuildElements(lods[l].renderers);
                    info.screenRelativeThresholds[l] = lods[l].screenRelativeTransitionHeight;
                }
            }
            else
            {
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                info.lodLevels = new[] { BuildElements(renderers) };
            }

            prototypeCache[i] = info;
        }
    }

    private static List<RenderElement> BuildElements(Renderer[] renderers)
    {
        var list = new List<RenderElement>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var localMatrix = Matrix4x4.TRS(r.transform.localPosition, r.transform.localRotation, r.transform.localScale);
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
            if (info.screenRelativeThresholds != null)
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
}
