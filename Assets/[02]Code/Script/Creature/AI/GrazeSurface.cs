using UnityEngine;

/// <summary>เช็คว่าจุดบน Terrain เป็นพื้นหญ้าไหม โดยดูสัดส่วนสีของ Terrain Layer ที่ชื่อมีคำที่กำหนด (เช่น "Grass")</summary>
public static class GrazeSurface
{
    public static bool IsGrass(Vector3 position, string layerKeyword, float minCoverage)
    {
        if (string.IsNullOrEmpty(layerKeyword)) return true;

        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            TerrainData data = terrain.terrainData;
            if (data == null) continue;

            Vector3 local = position - terrain.transform.position;
            float nx = local.x / data.size.x;
            float nz = local.z / data.size.z;
            if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;

            int x = Mathf.Clamp((int)(nx * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp((int)(nz * data.alphamapHeight), 0, data.alphamapHeight - 1);
            float[,,] weights = data.GetAlphamaps(x, z, 1, 1);

            TerrainLayer[] layers = data.terrainLayers;
            float coverage = 0f;
            for (int i = 0; i < layers.Length && i < data.alphamapLayers; i++)
            {
                if (layers[i] != null && layers[i].name.IndexOf(layerKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    coverage += weights[0, 0, i];
                }
            }
            return coverage >= minCoverage;
        }

        return false;
    }
}
