using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

// Terrain splat weights must sum to 1 per texel; texels that sum to ~0 render black in URP TerrainLit.
// Something in the paint workflow keeps producing such holes. After every splat edit (stroke or undo)
// this compares against the previous state, logs what changed when a hole appears, and fills the hole
// from the neighbouring texels.
[InitializeOnLoad]
static class TerrainWeightHoleGuard
{
    const float HoleSum = 0.5f;

    static readonly Dictionary<TerrainData, float[,,]> snapshots = new Dictionary<TerrainData, float[,,]>();
    static bool pending, repairing;
    static string trigger;

    static TerrainWeightHoleGuard()
    {
        TerrainCallbacks.textureChanged += (terrain, textureName, region, synched) =>
        {
            if (!repairing && textureName == TerrainData.AlphamapTextureName) Schedule("paint");
        };
        Undo.undoRedoPerformed += () => Schedule("undo/redo");
        EditorApplication.delayCall += () => { foreach (var t in Terrain.activeTerrains) Check(t, "load"); };
    }

    static void Schedule(string what)
    {
        trigger = what;
        if (pending) return;
        pending = true;
        EditorApplication.update += Flush;
    }

    static void Flush()
    {
        // wait for the stroke to end, mid-stroke the CPU copy is stale
        if (GUIUtility.hotControl != 0) return;
        EditorApplication.update -= Flush;
        pending = false;
        foreach (var t in Terrain.activeTerrains) Check(t, trigger);
    }

    [MenuItem("Tools/Terrain/Fix Weight Holes")]
    static void RepairAll()
    {
        foreach (var t in Terrain.activeTerrains) Check(t, "menu");
    }

    static void Check(Terrain terrain, string what)
    {
        var td = terrain.terrainData;
        int n = td.alphamapLayers, w = td.alphamapWidth, h = td.alphamapHeight;
        if (n == 0) return;

        td.SyncTexture(TerrainData.AlphamapTextureName);
        var a = td.GetAlphamaps(0, 0, w, h);
        var sum = new float[h, w];
        int holes = 0, x0 = w, x1 = 0, y0 = h, y1 = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                for (int k = 0; k < n; k++) sum[y, x] += a[y, x, k];
                if (sum[y, x] >= HoleSum) continue;
                holes++;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
            }

        if (holes == 0)
        {
            snapshots[td] = a;
            return;
        }

        Debug.LogWarning(Describe(terrain, a, what, holes, x0, x1, y0, y1));
        Fill(a, sum, w, h, n);

        repairing = true;
        try
        {
            Undo.RegisterCompleteObjectUndo(td.alphamapTextures, "Fix terrain weight holes");
            td.SetAlphamaps(0, 0, a);
            EditorUtility.SetDirty(td);
        }
        finally { repairing = false; }
        snapshots[td] = a;
    }

    // What the edit that produced the hole did to each layer inside the hole's bounding box.
    static string Describe(Terrain terrain, float[,,] now, string what, int holes, int x0, int x1, int y0, int y1)
    {
        var td = terrain.terrainData;
        int n = td.alphamapLayers;
        var sb = new StringBuilder();
        sb.Append($"[TerrainWeightHoleGuard] {holes} empty splat texels on '{terrain.name}' after {what}, alphamap x{x0}..{x1} y{y0}..{y1}. ");
        sb.Append($"undo='{Undo.GetCurrentGroupName()}' tool={ToolManager.activeToolType?.Name} brushStrength={EditorPrefs.GetFloat("TerrainBrushStrength", -1)} ");
        sb.Append($"brushSize={EditorPrefs.GetFloat("TerrainBrushSize", -1)} playing={EditorApplication.isPlaying}\n");

        snapshots.TryGetValue(td, out var before);
        bool comparable = before != null && before.GetLength(2) == n && before.GetLength(0) == now.GetLength(0);
        if (!comparable) sb.Append("(no previous state to compare, layer count or resolution changed)\n");
        for (int k = 0; k < n; k++)
        {
            float was = 0, isNow = 0;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    isNow += now[y, x, k];
                    if (comparable) was += before[y, x, k];
                }
            var layer = td.terrainLayers[k];
            sb.Append($"  layer {k} {(layer ? layer.name : "NULL")}: {(comparable ? was.ToString("0.0") + " -> " : "")}{isNow:0.0}\n");
        }
        return sb.ToString();
    }

    static void Fill(float[,,] a, float[,] sum, int w, int h, int n)
    {
        // grow inward: each pass fills hole texels that touch a valid texel
        for (int pass = 0; pass < w + h; pass++)
        {
            var fills = new List<(int y, int x, float[] v)>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (sum[y, x] >= HoleSum) continue;
                    var v = new float[n];
                    int count = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int yy = y + dy, xx = x + dx;
                            if (yy < 0 || yy >= h || xx < 0 || xx >= w || sum[yy, xx] < HoleSum) continue;
                            for (int k = 0; k < n; k++) v[k] += a[yy, xx, k] / sum[yy, xx];
                            count++;
                        }
                    if (count == 0) continue;
                    for (int k = 0; k < n; k++) v[k] /= count;
                    fills.Add((y, x, v));
                }
            if (fills.Count == 0) break;
            foreach (var f in fills)
            {
                for (int k = 0; k < n; k++) a[f.y, f.x, k] = f.v[k];
                sum[f.y, f.x] = 1;
            }
        }

        // hole rims keep a partial weight, scale those back up to 1
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (sum[y, x] >= HoleSum && sum[y, x] < 0.99f)
                    for (int k = 0; k < n; k++) a[y, x, k] /= sum[y, x];
    }
}
