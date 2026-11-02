using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ทำให้เห็ด/ดอกไม้ที่ "ระบายลง Terrain" ถ่ายรูปติดได้ — ติดที่ GameObject เดียวกับ Terrain
///
/// ปัญหา: ของที่วาดด้วย Paint Trees / Paint Details ไม่มี GameObject จริงในฉาก (Nature Renderer วาดให้)
/// PhotoSubject ที่อยู่บน Prefab เลยไม่มีตัวตน PhotoShooter ยิง SphereCast ไปก็ไม่เจอ
///
/// ตอนเริ่มซีนสร้าง "เป้าล่องหน" (Trigger + Tag Photographable + PhotoSubject) บน Layer Photographable:
///   - Tree: ทุก Instance ที่ Prefab มี PhotoSubject ที่ผูก Profile แล้ว — ขนาดตาม Collider ใน Prefab (ไม่มีก็ใช้ขนาดโมเดล) คูณ Scale ของ Instance
///   - Detail: จับเป็นบล็อก blockSize เมตร บล็อกไหนมีดอกไม้ของ detailSubjects อยู่ก็ได้กล่อง 1 กล่องคลุมบล็อกนั้น
/// ไม่แตะการวาด/ชนของ Terrain เดิม — PhotoShooter ต้องรวม Layer Photographable ไว้ใน Detectable Layer
/// Layer Photographable ไม่อยู่ในของที่กล้อง/สายตาสัตว์ชน เป้าพวกนี้จึงไม่บังกล้องหรือบังสายตาสัตว์
/// </summary>
[RequireComponent(typeof(Terrain))]
public class TerrainPhotoSubjects : MonoBehaviour
{
    [Serializable]
    public class DetailSubject
    {
        [Tooltip("Prefab ของ Detail (ดอกไม้) ใน Paint Details")]
        public GameObject detailPrefab;
        public JournalSubjectProfile profile;
    }

    [Header("Trees (เห็ด)")]
    [Tooltip("เป้าเล็กสุด (เมตร) กันเห็ดเล็กๆ เล็งโดนยาก")]
    public float minTreeTargetSize = 0.35f;

    [Header("Details (ดอกไม้)")]
    public List<DetailSubject> detailSubjects = new List<DetailSubject>();
    [Tooltip("ขนาดบล็อก (เมตร) ที่รวมดอกไม้เป็นเป้า 1 กล่อง")]
    public float blockSize = 3f;
    [Tooltip("ความสูงกล่องเป้าของดอกไม้ (เมตร)")]
    public float detailTargetHeight = 1.5f;

    [Header("Target")]
    public string photographableTag = "Photographable";
    public string photographableLayer = "Photographable";
    public bool logSummary = true;

    private void Start()
    {
        Terrain terrain = GetComponent<Terrain>();
        if (terrain.terrainData == null) return;

        int layer = LayerMask.NameToLayer(photographableLayer);
        if (layer < 0) layer = 0;

        var root = new GameObject("PhotoSubjectTargets").transform;
        root.SetParent(transform, false);

        int trees = BuildTreeTargets(terrain, root, layer);
        int blocks = BuildDetailTargets(terrain, root, layer);
        if (logSummary) Debug.Log($"[TerrainPhotoSubjects] สร้างเป้าถ่ายรูป: เห็ด/ต้นไม้ {trees} จุด, ดอกไม้ {blocks} บล็อก", this);
    }

    // ==================== Trees ====================
    private int BuildTreeTargets(Terrain terrain, Transform root, int layer)
    {
        TerrainData data = terrain.terrainData;
        TreePrototype[] prototypes = data.treePrototypes;
        var subjects = new PhotoSubject[prototypes.Length];
        var localBounds = new Bounds[prototypes.Length];

        for (int i = 0; i < prototypes.Length; i++)
        {
            GameObject prefab = prototypes[i].prefab;
            if (prefab == null) continue;
            PhotoSubject subject = prefab.GetComponentInChildren<PhotoSubject>(true);
            if (subject == null || subject.profile == null) continue;

            subjects[i] = subject;
            localBounds[i] = PrefabLocalBounds(prefab.transform);
        }

        int count = 0;
        Vector3 origin = terrain.transform.position;
        foreach (TreeInstance instance in data.treeInstances)
        {
            PhotoSubject subject = subjects[instance.prototypeIndex];
            if (subject == null) continue;

            Vector3 position = origin + Vector3.Scale(instance.position, data.size);
            Quaternion rotation = Quaternion.Euler(0f, instance.rotation * Mathf.Rad2Deg, 0f);
            Bounds b = localBounds[instance.prototypeIndex];
            Vector3 scale = new Vector3(instance.widthScale, instance.heightScale, instance.widthScale);
            Vector3 size = Vector3.Max(Vector3.Scale(b.size, scale), Vector3.one * minTreeTargetSize);

            CreateTarget(root, subject.profile, layer, position + rotation * Vector3.Scale(b.center, scale), rotation, size);
            count++;
        }
        return count;
    }

    // ขอบเขตเป้าในพิกัด Root ของ Prefab: ใช้ Collider ใน Prefab ก่อน (ศิลปินวางไว้คลุมตัวเห็ด) ไม่มีค่อยใช้ขอบเขตของโมเดล
    private static Bounds PrefabLocalBounds(Transform prefabRoot)
    {
        bool found = false;
        Bounds result = default;
        Matrix4x4 toRoot = prefabRoot.worldToLocalMatrix;

        foreach (Collider c in prefabRoot.GetComponentsInChildren<Collider>(true))
        {
            Bounds local = ColliderLocalBounds(c);
            Encapsulate(ref result, ref found, toRoot * c.transform.localToWorldMatrix, local);
        }
        if (!found)
        {
            foreach (MeshFilter mf in prefabRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Encapsulate(ref result, ref found, toRoot * mf.transform.localToWorldMatrix, mf.sharedMesh.bounds);
            }
        }
        return found ? result : new Bounds(Vector3.up * 0.15f, Vector3.one * 0.3f);
    }

    private static Bounds ColliderLocalBounds(Collider c)
    {
        switch (c)
        {
            case BoxCollider box: return new Bounds(box.center, box.size);
            case SphereCollider sphere: return new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
            case CapsuleCollider capsule:
            {
                Vector3 size = Vector3.one * capsule.radius * 2f;
                size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                return new Bounds(capsule.center, size);
            }
            case MeshCollider mesh when mesh.sharedMesh != null: return mesh.sharedMesh.bounds;
            default: return new Bounds(Vector3.zero, Vector3.one * 0.3f);
        }
    }

    private static void Encapsulate(ref Bounds result, ref bool found, Matrix4x4 matrix, Bounds local)
    {
        Vector3 e = local.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = local.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            Vector3 p = matrix.MultiplyPoint3x4(corner);
            if (!found) { result = new Bounds(p, Vector3.zero); found = true; }
            else result.Encapsulate(p);
        }
    }

    // ==================== Details ====================
    private int BuildDetailTargets(Terrain terrain, Transform root, int layer)
    {
        TerrainData data = terrain.terrainData;
        DetailPrototype[] prototypes = data.detailPrototypes;
        int width = data.detailWidth, height = data.detailHeight;
        if (width == 0 || height == 0) return 0;

        float cellX = data.size.x / width, cellZ = data.size.z / height;
        int blockCellsX = Mathf.Max(1, Mathf.RoundToInt(blockSize / cellX));
        int blockCellsZ = Mathf.Max(1, Mathf.RoundToInt(blockSize / cellZ));
        Vector3 origin = terrain.transform.position;
        int count = 0;

        // รวม Prototype ที่เป็นชนิดเดียวกัน (เช่น Patch_01 กับ Patch_02) ให้เป็นบล็อกเดียวกัน
        var layersByProfile = new Dictionary<JournalSubjectProfile, List<int[,]>>();
        for (int i = 0; i < prototypes.Length; i++)
        {
            JournalSubjectProfile profile = ProfileForDetail(prototypes[i].prototype);
            if (profile == null) continue;
            if (!layersByProfile.TryGetValue(profile, out List<int[,]> list)) layersByProfile[profile] = list = new List<int[,]>();
            list.Add(data.GetDetailLayer(0, 0, width, height, i));
        }

        foreach (KeyValuePair<JournalSubjectProfile, List<int[,]>> pair in layersByProfile)
        {
            for (int bz = 0; bz < height; bz += blockCellsZ)
            for (int bx = 0; bx < width; bx += blockCellsX)
            {
                if (!BlockHasDetail(pair.Value, bx, bz, Mathf.Min(bx + blockCellsX, width), Mathf.Min(bz + blockCellsZ, height))) continue;

                Vector3 center = origin + new Vector3((bx + blockCellsX * 0.5f) * cellX, 0f, (bz + blockCellsZ * 0.5f) * cellZ);
                center.y = terrain.SampleHeight(center) + origin.y + detailTargetHeight * 0.5f;
                CreateTarget(root, pair.Key, layer, center, Quaternion.identity, new Vector3(blockCellsX * cellX, detailTargetHeight, blockCellsZ * cellZ));
                count++;
            }
        }
        return count;
    }

    private JournalSubjectProfile ProfileForDetail(GameObject detailPrefab)
    {
        if (detailPrefab == null) return null;
        foreach (DetailSubject entry in detailSubjects)
        {
            if (entry != null && entry.detailPrefab == detailPrefab) return entry.profile;
        }
        return null;
    }

    private static bool BlockHasDetail(List<int[,]> layers, int x0, int z0, int x1, int z1)
    {
        foreach (int[,] map in layers)
        {
            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                if (map[z, x] > 0) return true;
            }
        }
        return false;
    }

    // ==================== Target ====================
    private void CreateTarget(Transform root, JournalSubjectProfile profile, int layer, Vector3 position, Quaternion rotation, Vector3 size)
    {
        var go = new GameObject(profile.name);
        go.layer = layer;
        go.tag = photographableTag;
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(position, rotation);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;

        go.AddComponent<PhotoSubject>().profile = profile;
    }
}
