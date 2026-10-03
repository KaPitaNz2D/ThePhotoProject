using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AI;
using UnityEngine;

// เครื่องมือ Editor สร้างสนามทดสอบ NavMesh แบบกดปุ่มเดียว
// พื้นเป็น Terrain จริง มีเนิน/ภูเขาล้อมรอบลานกลางที่เรียบ, มีต้นไม้กระจายเป็นสิ่งกีดขวางทดสอบ, และวางกวาง + Player ให้พร้อมกด Play
// ความชันของเนินอ้างอิงจาก NavMesh Agent ของ Deer (ProjectSettings/NavMeshAreas.asset: agentSlope 48.3°)
// เนินส่วนใหญ่ตั้งใจให้ชันน้อยกว่านั้นเพื่อให้เดินขึ้นได้ ส่วน "ภูเขา" ตัวหนึ่งตั้งใจให้ชันเกิน เพื่อทดสอบว่า NavMesh เจาะรูรอบยอดที่เดินไม่ถึงถูกต้องไหม
public static class NavMeshTestEnvironmentGenerator
{
    private const string RootName = "NavMeshTestEnvironment";
    private const float EnvironmentSize = 90f;
    private const int HeightmapResolution = 257;
    private const float TerrainMaxHeight = 20f;

    // ลานกลางเรียบสำหรับวางตัวละคร (กวาง/Player) ให้สปอนบนพื้นราบ
    private const float FlatRadius = 16f;
    private const float FlatFalloff = 8f; // ระยะเปลี่ยนจากเรียบไปเป็นเนิน (16 -> 24)

    private const string TerrainDataFolder = "Assets/[04]Level/TestingLab/AI_Walking_Test_Navmesh";
    private const string TerrainDataAssetPath = TerrainDataFolder + "/NavMeshTestTerrainData.asset";

    private const int TreeCount = 55;
    private const float TreeClearRadius = 8f; // เว้นพื้นที่กลางไว้ให้ตัวละครสปอน ไม่ให้ต้นไม้ไปโผล่ทับ
    private const float TreeMinSpacing = 2.5f;
    private const float TreeEdgeMargin = 4f;

    private static readonly string[] TreePrefabPaths =
    {
        "Assets/TreeImpostors/Prefabs/Pine_Tree_001_Impostor_20260914_001711_673.prefab",
        "Assets/TreeImpostors/Prefabs/Pine_Tree_002_Impostor_20260914_001626_267.prefab",
        "Assets/TreeImpostors/Prefabs/Pine_Tree_003_Impostor_20260914_000603_877.prefab",
        "Assets/TreeImpostors/Prefabs/Pine_Tree_004_Impostor_20260914_000355_952.prefab",
        "Assets/TreeImpostors/Prefabs/Tarmarack_Tree_001_Impostor_20260913_230836_745.prefab",
    };

    private const string DeerPrefabPath = "Assets/[03]Prefabs/Deer_Testing Variant.prefab";
    private const string PlayerPrefabPath = "Assets/[03]Prefabs/Player.prefab";

    private struct HillDef
    {
        public string Name;
        public float AngleDeg;
        public float DistanceFromCenter;
        public float Amplitude; // ความสูงจากลานกลาง (หน่วย world)
        public float Sigma; // ยิ่งเล็กยิ่งชัน/แคบ ยิ่งใหญ่ยิ่งลาดเอียงเบา
    }

    // Amplitude/Sigma ถูกเลือกให้ slope โดยประมาณ (atan(0.6065 * Amplitude / Sigma)) กระจายจากเดินง่ายไปถึงชันเกินลิมิต
    private static readonly HillDef[] Hills =
    {
        new HillDef { Name = "Hill_Gentle_North", AngleDeg = 0f,   DistanceFromCenter = 26f, Amplitude = 4f,  Sigma = 8f }, // ~17° เดินง่าย
        new HillDef { Name = "Hill_Gentle_East",  AngleDeg = 90f,  DistanceFromCenter = 26f, Amplitude = 5f,  Sigma = 7f }, // ~24° ยังเดินได้สบาย
        new HillDef { Name = "Hill_Steep_South",  AngleDeg = 180f, DistanceFromCenter = 26f, Amplitude = 7f,  Sigma = 6f }, // ~35° เดินได้แต่ใกล้ลิมิต
        new HillDef { Name = "Mountain_West",     AngleDeg = 270f, DistanceFromCenter = 26f, Amplitude = 10f, Sigma = 5f }, // ~50° เกิน agentSlope 48.3° ตั้งใจให้ยอดเดินไม่ถึง
    };

    [MenuItem("Tools/Photo Project/Generate NavMesh Test Environment")]
    public static void Generate()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject(RootName);

        Terrain terrain = CreateTerrainGround(root.transform);
        ScatterTrees(root.transform, terrain);
        PlaceCharacters(root.transform, terrain);

        NavMeshBuilder.BuildNavMesh();

        Selection.activeGameObject = root;
        Debug.Log("[NavMeshTestEnvironmentGenerator] สร้างสนามทดสอบ (เนิน + ต้นไม้ + กวาง/Player) + Bake NavMesh เสร็จแล้ว");
    }

    // Terrain จริง: ลานกลางเรียบ (สำหรับสปอนตัวละคร) + เนิน/ภูเขาล้อมรอบ 4 ลูก ความชันต่างกันตาม Hills[]
    private static Terrain CreateTerrainGround(Transform parent)
    {
        TerrainData terrainData = new TerrainData
        {
            heightmapResolution = HeightmapResolution,
            size = new Vector3(EnvironmentSize, TerrainMaxHeight, EnvironmentSize),
        };
        terrainData.SetHeights(0, 0, GenerateHeights(terrainData.heightmapResolution));
        SaveTerrainDataAsset(terrainData);

        GameObject terrainGO = Terrain.CreateTerrainGameObject(terrainData);
        terrainGO.name = "Ground";
        terrainGO.transform.SetParent(parent);
        terrainGO.transform.position = new Vector3(-EnvironmentSize / 2f, 0f, -EnvironmentSize / 2f);
        MarkNavigationStatic(terrainGO);

        return terrainGO.GetComponent<Terrain>();
    }

    private static float[,] GenerateHeights(int resolution)
    {
        float[,] heights = new float[resolution, resolution];
        float noiseSeedX = Random.Range(0f, 1000f);
        float noiseSeedZ = Random.Range(0f, 1000f);

        for (int zIndex = 0; zIndex < resolution; zIndex++)
        {
            for (int xIndex = 0; xIndex < resolution; xIndex++)
            {
                float worldX = (xIndex / (float)(resolution - 1) - 0.5f) * EnvironmentSize;
                float worldZ = (zIndex / (float)(resolution - 1) - 0.5f) * EnvironmentSize;
                float distFromCenter = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);

                float flatWeight = Mathf.SmoothStep(0f, 1f, (distFromCenter - FlatRadius) / FlatFalloff);

                float height = 0f;
                foreach (HillDef hill in Hills)
                {
                    Vector2 hillCenter = AngleToPosition(hill.AngleDeg, hill.DistanceFromCenter);
                    float dx = worldX - hillCenter.x;
                    float dz = worldZ - hillCenter.y;
                    float distToHill = Mathf.Sqrt(dx * dx + dz * dz);
                    height += hill.Amplitude * Mathf.Exp(-(distToHill * distToHill) / (2f * hill.Sigma * hill.Sigma));
                }

                // noise เบาๆ ให้พื้นผิวเนินดูเป็นธรรมชาติ ไม่ใช่โดมเรียบๆ
                float fineNoise = Mathf.PerlinNoise(noiseSeedX + worldX * 0.15f, noiseSeedZ + worldZ * 0.15f) * 0.6f;

                heights[zIndex, xIndex] = Mathf.Clamp01((height + fineNoise) * flatWeight / TerrainMaxHeight);
            }
        }

        return heights;
    }

    private static Vector2 AngleToPosition(float angleDeg, float distance)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad) * distance, Mathf.Cos(rad) * distance);
    }

    private static void SaveTerrainDataAsset(TerrainData terrainData)
    {
        if (!AssetDatabase.IsValidFolder(TerrainDataFolder))
        {
            Debug.LogWarning($"[NavMeshTestEnvironmentGenerator] ไม่พบโฟลเดอร์ {TerrainDataFolder} จะไม่บันทึก TerrainData เป็น asset (จะยังใช้งานได้ในเซสชันนี้ แต่หายเมื่อปิด Editor)");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataAssetPath) != null)
        {
            AssetDatabase.DeleteAsset(TerrainDataAssetPath);
        }

        AssetDatabase.CreateAsset(terrainData, TerrainDataAssetPath);
        AssetDatabase.SaveAssets();
    }

    // กระจายต้นไม้ (tree impostor ที่มีอยู่ในโปรเจกต์) รอบพื้นที่แบบสุ่ม เว้นลานกลางไว้ให้ตัวละครสปอน
    // แต่ละต้นเป็น billboard ไม่มี collider มาเอง เลยเติม CapsuleCollider เล็กๆ ที่โคนต้นให้เป็นสิ่งกีดขวางสำหรับ NavMesh ด้วย
    private static void ScatterTrees(Transform parent, Terrain terrain)
    {
        GameObject[] treePrefabs = LoadTreePrefabs();
        if (treePrefabs.Length == 0)
        {
            Debug.LogWarning("[NavMeshTestEnvironmentGenerator] ไม่พบ tree prefab ใน Assets/TreeImpostors/Prefabs จะข้ามการกระจายต้นไม้");
            return;
        }

        GameObject treesRoot = new GameObject("Zone_Trees");
        treesRoot.transform.SetParent(parent);

        List<Vector2> placedPositions = new List<Vector2>();
        float halfSize = EnvironmentSize / 2f - TreeEdgeMargin;
        int placedCount = 0;
        int attempts = 0;
        int maxAttempts = TreeCount * 30;

        while (placedCount < TreeCount && attempts < maxAttempts)
        {
            attempts++;
            Vector2 candidate = new Vector2(Random.Range(-halfSize, halfSize), Random.Range(-halfSize, halfSize));
            if (candidate.magnitude < TreeClearRadius)
            {
                continue;
            }

            bool tooClose = false;
            foreach (Vector2 existingPos in placedPositions)
            {
                if (Vector2.Distance(existingPos, candidate) < TreeMinSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose)
            {
                continue;
            }

            placedPositions.Add(candidate);
            placedCount++;

            GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
            GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tree.transform.SetParent(treesRoot.transform);

            Vector3 worldPos = new Vector3(candidate.x, 0f, candidate.y);
            worldPos.y = terrain.SampleHeight(worldPos);
            tree.transform.position = worldPos;

            float scale = Random.Range(0.8f, 1.4f);
            tree.transform.localScale = Vector3.one * scale;

            CapsuleCollider trunk = tree.AddComponent<CapsuleCollider>();
            trunk.radius = 0.35f * scale;
            trunk.height = 3f * scale;
            trunk.center = new Vector3(0f, trunk.height / 2f, 0f);
            MarkNavigationStatic(tree);
        }
    }

    private static GameObject[] LoadTreePrefabs()
    {
        List<GameObject> loaded = new List<GameObject>();
        foreach (string path in TreePrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                loaded.Add(prefab);
            }
        }
        return loaded.ToArray();
    }

    // วางกวาง (ตัวทดสอบ AI) กับ Player ไว้บนลานกลางเรียบ ห่างกันพอสมควร พร้อมกด Play ทดสอบได้เลย
    private static void PlaceCharacters(Transform parent, Terrain terrain)
    {
        SpawnCharacter(parent, terrain, DeerPrefabPath, new Vector2(4f, 4f));
        SpawnCharacter(parent, terrain, PlayerPrefabPath, new Vector2(-4f, -4f));
    }

    private static void SpawnCharacter(Transform parent, Terrain terrain, string prefabPath, Vector2 xz)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[NavMeshTestEnvironmentGenerator] ไม่พบ prefab ที่ '{prefabPath}' ข้ามการวางตัวละครนี้");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent);

        Vector3 pos = new Vector3(xz.x, 0f, xz.y);
        pos.y = terrain.SampleHeight(pos);
        instance.transform.position = pos;
    }

    private static void MarkNavigationStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
    }
}
