using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bakes per-face / per-island random values into a mesh's vertex colors so a shader can
/// tint each leaf (or each face) differently without any per-object material work.
///
/// The fragment stage interpolates everything, so a "constant per face" value only survives
/// if it is identical on all verts of that face. Two ways to guarantee that:
///   PerIsland   - every vertex of a connected triangle island gets the same value.
///                 No vertex duplication, so this is free. Correct choice for leaf cards,
///                 where a quad is 2 triangles that must share one color.
///   PerTriangle - requires unwelding (vertex count becomes 3x triangle count) because
///                 neighbouring faces would otherwise blend across shared verts.
/// </summary>
public static class FaceColorBaker
{
    public enum Scope
    {
        PerIsland,
        PerTriangle
    }

    public enum Axis
    {
        X,
        Y,
        Z
    }

    public struct Settings
    {
        public Scope scope;
        public int seed;

        // R: primary random 0..1 - drives the base color lerp.
        public bool writeR;
        // G: second, uncorrelated random 0..1 - wind phase offset, so leaves don't sway in sync.
        public bool writeG;
        // B: normalized position along heightAxis *within the island* - 0 at the leaf base,
        //    1 at the tip. Feeds a tip mask so bend scales from the stem outward.
        public bool writeB;
        public Axis heightAxis;
        // A: forced to 1. Off by default because alpha often already carries authored data.
        public bool writeA;

        /// <summary>
        /// Object-space grid the island key snaps to before hashing, purely to absorb float
        /// drift between re-exports. Keep it well below leaf size - it is NOT the LOD-stability
        /// mechanism (see <see cref="IslandKey"/> for that), and raising it only starts merging
        /// genuinely distinct leaves onto the same tint.
        /// </summary>
        public float positionQuantize;

        public static Settings Default => new Settings
        {
            scope = Scope.PerIsland,
            seed = 0,
            writeR = true,
            writeG = true,
            writeB = true,
            heightAxis = Axis.Y,
            writeA = false,
            positionQuantize = 0.001f
        };
    }

    /// <summary>
    /// Position -> baked color lookup harvested from an already-baked mesh, used to carry LOD0's
    /// tints down to the lower LODs.
    ///
    /// Hashing each LOD independently does NOT produce matching tints: LOD decimation changes the
    /// island decomposition (measured on SugarMaple: 648 islands at LOD0 vs 210 at LOD1), so no
    /// position-derived key identifies "the same leaf" across levels - centroid keying agreed ~5%
    /// of the time, extreme-vertex keying ~17%. What IS reliable is that decimated LODs reuse the
    /// exact vertex positions of LOD0, so the color can be looked up rather than recomputed.
    /// </summary>
    public sealed class ReferenceColors
    {
        private readonly Dictionary<Vector3Int, Color> map = new Dictionary<Vector3Int, Color>();
        private readonly float quantize;

        public ReferenceColors(float quantize)
        {
            this.quantize = Mathf.Max(quantize, 1e-5f);
        }

        public int Count => map.Count;

        private Vector3Int Cell(Vector3 p) => new Vector3Int(
            Mathf.RoundToInt(p.x / quantize),
            Mathf.RoundToInt(p.y / quantize),
            Mathf.RoundToInt(p.z / quantize));

        public void Add(Mesh baked, Matrix4x4 toRoot)
        {
            if (baked == null) return;
            var colors = baked.colors;
            if (colors == null || colors.Length != baked.vertexCount) return;
            var positions = baked.vertices;
            for (int v = 0; v < positions.Length; v++)
            {
                // Distinct leaves genuinely share corner positions, so a cell can be claimed by
                // more than one island. First writer wins: the result is then independent of
                // vertex order instead of silently depending on it.
                var cell = Cell(toRoot.MultiplyPoint3x4(positions[v]));
                if (!map.ContainsKey(cell)) map[cell] = colors[v];
            }
        }

        public bool TryGet(Vector3 rootSpacePosition, out Color color) =>
            map.TryGetValue(Cell(rootSpacePosition), out color);
    }

    /// <summary>
    /// Returns a new mesh with vertex colors baked. The source mesh is never modified.
    /// </summary>
    /// <param name="reference">
    /// When supplied, each island takes the most common color found among its vertices in the
    /// reference instead of a fresh hash, keeping lower LODs consistent with LOD0. Islands with no
    /// match fall back to hashing. Voting (rather than per-vertex copy) preserves the flat-per-leaf
    /// guarantee even where a decimated island straddles two source leaves.
    /// </param>
    public static Mesh Bake(Mesh source, Settings settings,
                            ReferenceColors reference = null, Matrix4x4 toRoot = default)
    {
        if (source == null) return null;
        if (toRoot == default) toRoot = Matrix4x4.identity;

        // PerTriangle needs every face to own its verts; PerIsland can bake in place because
        // islands are defined by index connectivity and therefore never share vertices.
        var mesh = settings.scope == Scope.PerTriangle ? Unweld(source) : Clone(source);

        int vertexCount = mesh.vertexCount;
        var positions = mesh.vertices;
        var islandOf = BuildIslands(mesh, out int islandCount);

        // Island bounds along the height axis (for the B channel) and the island key (for the
        // random channels).
        var minH = new float[islandCount];
        var maxH = new float[islandCount];
        var key = new Vector3[islandCount];
        var memberCount = new int[islandCount];
        for (int i = 0; i < islandCount; i++)
        {
            minH[i] = float.PositiveInfinity;
            maxH[i] = float.NegativeInfinity;
        }

        for (int v = 0; v < vertexCount; v++)
        {
            int island = islandOf[v];
            if (island < 0) continue;
            float h = Component(positions[v], settings.heightAxis);
            if (h < minH[island]) minH[island] = h;
            if (h > maxH[island]) maxH[island] = h;

            if (memberCount[island] == 0 || IslandKey(positions[v], key[island]))
                key[island] = positions[v];
            memberCount[island]++;
        }

        var randR = new float[islandCount];
        var randG = new float[islandCount];
        for (int i = 0; i < islandCount; i++)
        {
            if (memberCount[i] == 0) continue;
            randR[i] = Hash(key[i], settings.seed, settings.positionQuantize);
            randG[i] = Hash(key[i], settings.seed + 8675309, settings.positionQuantize);
        }

        if (reference != null)
            InheritFromReference(reference, toRoot, positions, islandOf, islandCount, randR, randG);

        var existing = mesh.colors;
        bool hasExisting = existing != null && existing.Length == vertexCount;
        var colors = new Color[vertexCount];

        for (int v = 0; v < vertexCount; v++)
        {
            Color c = hasExisting ? existing[v] : Color.white;
            int island = islandOf[v];

            if (island >= 0)
            {
                if (settings.writeR) c.r = randR[island];
                if (settings.writeG) c.g = randG[island];
                if (settings.writeB)
                {
                    float span = maxH[island] - minH[island];
                    // A perfectly flat island has no gradient to describe; 0 keeps it unbent.
                    c.b = span > 1e-6f
                        ? Mathf.Clamp01((Component(positions[v], settings.heightAxis) - minH[island]) / span)
                        : 0f;
                }
            }

            if (settings.writeA) c.a = 1f;
            colors[v] = c;
        }

        mesh.colors = colors;
        mesh.name = source.name + "_FC";
        return mesh;
    }

    /// <summary>
    /// Replaces each island's hashed values with the modal color of its vertices in the reference.
    /// </summary>
    private static void InheritFromReference(ReferenceColors reference, Matrix4x4 toRoot,
                                             Vector3[] positions, int[] islandOf, int islandCount,
                                             float[] randR, float[] randG)
    {
        var votes = new Dictionary<int, Color>[islandCount];
        var tally = new Dictionary<int, int>[islandCount];

        for (int v = 0; v < positions.Length; v++)
        {
            int island = islandOf[v];
            if (island < 0) continue;
            if (!reference.TryGet(toRoot.MultiplyPoint3x4(positions[v]), out var c)) continue;

            // Bucket on the quantized R/G pair so votes for one source leaf collapse together.
            int bucket = Mathf.RoundToInt(c.r * 4096f) * 8192 + Mathf.RoundToInt(c.g * 4096f);
            votes[island] = votes[island] ?? new Dictionary<int, Color>();
            tally[island] = tally[island] ?? new Dictionary<int, int>();
            votes[island][bucket] = c;
            tally[island].TryGetValue(bucket, out int n);
            tally[island][bucket] = n + 1;
        }

        for (int i = 0; i < islandCount; i++)
        {
            if (tally[i] == null) continue; // no match anywhere -> keep the hashed fallback
            int bestBucket = 0, bestCount = -1;
            foreach (var kv in tally[i])
            {
                // Ties resolve on the lower bucket id so the result does not depend on hash order.
                if (kv.Value > bestCount || (kv.Value == bestCount && kv.Key < bestBucket))
                {
                    bestCount = kv.Value;
                    bestBucket = kv.Key;
                }
            }
            var winner = votes[i][bestBucket];
            randR[i] = winner.r;
            randG[i] = winner.g;
        }
    }

    /// <summary>
    /// Picks the island's identity vertex: the lexicographically greatest position it contains.
    /// Preferred over the centroid because it does not move when decimation deletes a triangle
    /// from inside the island, so re-baking after a mesh tweak reshuffles fewer tints.
    ///
    /// This is only a stability nicety, NOT the fix for LOD tint popping - no position-derived key
    /// survives LOD decimation reliably (see <see cref="ReferenceColors"/>).
    /// </summary>
    private static bool IslandKey(Vector3 candidate, Vector3 current)
    {
        if (candidate.x != current.x) return candidate.x > current.x;
        if (candidate.y != current.y) return candidate.y > current.y;
        return candidate.z > current.z;
    }

    /// <summary>
    /// Union-find over triangle connectivity. Vertices reachable through shared triangle
    /// indices land in the same island. Loose vertices touched by no triangle get -1.
    /// </summary>
    private static int[] BuildIslands(Mesh mesh, out int islandCount)
    {
        int vertexCount = mesh.vertexCount;
        var parent = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++) parent[i] = i;

        var used = new bool[vertexCount];

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
            var tris = mesh.GetTriangles(sub);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                used[a] = used[b] = used[c] = true;
                Union(parent, a, b);
                Union(parent, b, c);
            }
        }

        var remap = new Dictionary<int, int>();
        var islandOf = new int[vertexCount];
        islandCount = 0;
        for (int v = 0; v < vertexCount; v++)
        {
            if (!used[v]) { islandOf[v] = -1; continue; }
            int root = Find(parent, v);
            if (!remap.TryGetValue(root, out int id))
            {
                id = islandCount++;
                remap[root] = id;
            }
            islandOf[v] = id;
        }
        return islandOf;
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]]; // path halving
            i = parent[i];
        }
        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int ra = Find(parent, a), rb = Find(parent, b);
        if (ra != rb) parent[ra] = rb;
    }

    /// <summary>Duplicates every vertex per triangle so each face owns its three verts.</summary>
    private static Mesh Unweld(Mesh src)
    {
        var srcPos = src.vertices;
        var srcNrm = src.normals;
        var srcTan = src.tangents;
        var srcCol = src.colors;
        var srcUv = new List<Vector4>[4];
        for (int ch = 0; ch < 4; ch++)
        {
            var list = new List<Vector4>();
            src.GetUVs(ch, list);
            srcUv[ch] = list.Count == src.vertexCount ? list : null;
        }

        int total = 0;
        for (int sub = 0; sub < src.subMeshCount; sub++) total += (int)src.GetIndexCount(sub);

        var pos = new Vector3[total];
        var nrm = srcNrm.Length == src.vertexCount ? new Vector3[total] : null;
        var tan = srcTan.Length == src.vertexCount ? new Vector4[total] : null;
        var col = srcCol.Length == src.vertexCount ? new Color[total] : null;
        var uv = new List<Vector4>[4];
        for (int ch = 0; ch < 4; ch++) uv[ch] = srcUv[ch] != null ? new List<Vector4>(total) : null;

        var subTris = new int[src.subMeshCount][];
        int write = 0;
        for (int sub = 0; sub < src.subMeshCount; sub++)
        {
            var tris = src.GetTriangles(sub);
            var newTris = new int[tris.Length];
            for (int i = 0; i < tris.Length; i++)
            {
                int s = tris[i];
                pos[write] = srcPos[s];
                if (nrm != null) nrm[write] = srcNrm[s];
                if (tan != null) tan[write] = srcTan[s];
                if (col != null) col[write] = srcCol[s];
                for (int ch = 0; ch < 4; ch++) uv[ch]?.Add(srcUv[ch][s]);
                newTris[i] = write;
                write++;
            }
            subTris[sub] = newTris;
        }

        var mesh = new Mesh
        {
            name = src.name,
            indexFormat = total > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        mesh.vertices = pos;
        if (nrm != null) mesh.normals = nrm;
        if (tan != null) mesh.tangents = tan;
        if (col != null) mesh.colors = col;
        for (int ch = 0; ch < 4; ch++) if (uv[ch] != null) mesh.SetUVs(ch, uv[ch]);

        mesh.subMeshCount = src.subMeshCount;
        for (int sub = 0; sub < src.subMeshCount; sub++) mesh.SetTriangles(subTris[sub], sub, false);

        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh Clone(Mesh src)
    {
        var mesh = Object.Instantiate(src);
        mesh.name = src.name;
        return mesh;
    }

    private static float Component(Vector3 v, Axis axis)
    {
        switch (axis)
        {
            case Axis.X: return v.x;
            case Axis.Z: return v.z;
            default: return v.y;
        }
    }

    /// <summary>Deterministic 3D -> 1D hash (Dave Hoskins' hash13), matched by the shader side.</summary>
    private static float Hash(Vector3 p, int seed, float quantize)
    {
        // Snap to a grid so tiny float drift between re-exports - and the small vertex
        // displacement between LOD levels - cannot flip a leaf to a different color.
        float q = Mathf.Max(quantize, 1e-5f);
        p = new Vector3(
            Mathf.Round(p.x / q) * q,
            Mathf.Round(p.y / q) * q,
            Mathf.Round(p.z / q) * q) + Vector3.one * (seed * 0.7548777f);

        Vector3 p3 = new Vector3(Frac(p.x * 0.1031f), Frac(p.y * 0.1030f), Frac(p.z * 0.0973f));
        float d = p3.x * (p3.z + 31.32f) + p3.y * (p3.y + 31.32f) + p3.z * (p3.x + 31.32f);
        p3 += Vector3.one * d;
        return Frac((p3.x + p3.y) * p3.z);
    }

    private static float Frac(float f) => f - Mathf.Floor(f);
}
