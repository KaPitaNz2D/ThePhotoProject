using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ReferenceColors = FaceColorBaker.ReferenceColors;

/// <summary>
/// Selection-driven front end for <see cref="FaceColorBaker"/>. Bakes to new mesh assets and
/// repoints the MeshFilters at them - the source FBX meshes are never touched, so a re-import
/// of the model cannot silently wipe the baked colors.
/// </summary>
public class FaceColorBakerWindow : EditorWindow
{
    private FaceColorBaker.Settings settings = FaceColorBaker.Settings.Default;
    private string outputFolder = "Assets/[01]Art/Meshes/Baked";
    private bool assignToRenderers = true;

    [MenuItem("Tools/Photo Project/Bake Per-Face Vertex Colors")]
    private static void Open()
    {
        GetWindow<FaceColorBakerWindow>(true, "Face Color Baker").minSize = new Vector2(360f, 380f);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Select prefabs, scene objects, or mesh assets, then Bake.\n\n" +
            "R = per-leaf random (base color lerp)\n" +
            "G = per-leaf random (wind phase offset)\n" +
            "B = height within the leaf, 0 base -> 1 tip (tip mask)",
            MessageType.Info);

        EditorGUILayout.Space();
        settings.scope = (FaceColorBaker.Scope)EditorGUILayout.EnumPopup(
            new GUIContent("Scope", "PerIsland keeps a whole leaf card one color and does not add verts. " +
                                    "PerTriangle unwelds the mesh (3x vertex count)."), settings.scope);

        if (settings.scope == FaceColorBaker.Scope.PerTriangle)
        {
            EditorGUILayout.HelpBox(
                "PerTriangle triples the vertex count and gives each triangle of a leaf quad a " +
                "different color. Use PerIsland for leaf cards.", MessageType.Warning);
        }

        settings.seed = EditorGUILayout.IntField("Seed", settings.seed);
        settings.positionQuantize = EditorGUILayout.FloatField(
            new GUIContent("Position Quantize", "Grid the island key snaps to before hashing, to absorb " +
                                                "float drift between re-exports. Keep well below leaf size."),
            settings.positionQuantize);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Channels", EditorStyles.boldLabel);
        settings.writeR = EditorGUILayout.Toggle("R  Random A", settings.writeR);
        settings.writeG = EditorGUILayout.Toggle("G  Random B", settings.writeG);
        settings.writeB = EditorGUILayout.Toggle("B  Tip Mask", settings.writeB);
        using (new EditorGUI.DisabledScope(!settings.writeB))
        {
            EditorGUI.indentLevel++;
            settings.heightAxis = (FaceColorBaker.Axis)EditorGUILayout.EnumPopup("Height Axis", settings.heightAxis);
            EditorGUI.indentLevel--;
        }
        settings.writeA = EditorGUILayout.Toggle("A  Force 1", settings.writeA);

        EditorGUILayout.Space();
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        assignToRenderers = EditorGUILayout.Toggle(
            new GUIContent("Assign To Renderers", "Repoint MeshFilters on the selection at the baked meshes."),
            assignToRenderers);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Selection.objects.Length == 0))
        {
            if (GUILayout.Button("Bake Selection", GUILayout.Height(30f))) Bake();
        }
    }

    private void Bake()
    {
        EnsureFolder(outputFolder);

        // One baked asset per unique source mesh, so a prefab whose LODs share a mesh does not
        // produce duplicates - and every reference points at the same result.
        var baked = new Dictionary<Mesh, Mesh>();
        int prefabCount = 0;

        foreach (var obj in Selection.objects)
        {
            if (obj is Mesh sourceMesh)
            {
                GetOrBake(sourceMesh, baked);
                continue;
            }

            var go = obj as GameObject;
            if (go == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(go);
            bool isPrefabAsset = !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab");

            if (isPrefabAsset)
            {
                var contents = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    int changed = ProcessHierarchy(contents, baked);
                    if (changed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                        prefabCount++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            else
            {
                ProcessHierarchy(go, baked);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FaceColorBaker] Baked {baked.Count} mesh(es) into '{outputFolder}'" +
                  (prefabCount > 0 ? $", updated {prefabCount} prefab(s)." : "."));
    }

    private int ProcessHierarchy(GameObject root, Dictionary<Mesh, Mesh> baked)
    {
        int changed = 0;

        // LOD0 must be baked first so the lower levels can inherit its tints; hashing each level
        // independently makes them disagree and the canopy visibly reshuffles at every transition.
        var ordered = OrderByLod(root, out int lod0Count);
        ReferenceColors reference = null;

        for (int i = 0; i < ordered.Count; i++)
        {
            var filter = ordered[i];
            var source = filter.sharedMesh;
            if (source == null) continue;

            // Everything is compared in root space, since LOD children need not share a transform.
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;

            if (i == lod0Count && lod0Count > 0)
            {
                reference = new ReferenceColors(Mathf.Max(settings.positionQuantize, 1e-4f));
                for (int j = 0; j < lod0Count; j++)
                {
                    if (ordered[j].sharedMesh == null) continue;
                    if (!baked.TryGetValue(ordered[j].sharedMesh, out var refMesh)) continue;
                    reference.Add(refMesh, root.transform.worldToLocalMatrix *
                                           ordered[j].transform.localToWorldMatrix);
                }
            }

            var result = GetOrBake(source, baked, i >= lod0Count ? reference : null, toRoot);
            if (result == null) continue;

            if (assignToRenderers && filter.sharedMesh != result)
            {
                Undo.RecordObject(filter, "Assign Baked Mesh");
                filter.sharedMesh = result;
                EditorUtility.SetDirty(filter);
                changed++;
            }
        }
        return changed;
    }

    /// <summary>
    /// Returns the hierarchy's MeshFilters with every LOD0 renderer first, and reports how many
    /// that is. Without a LODGroup everything counts as LOD0 and no inheritance happens.
    /// </summary>
    private static List<MeshFilter> OrderByLod(GameObject root, out int lod0Count)
    {
        var all = new List<MeshFilter>(root.GetComponentsInChildren<MeshFilter>(true));
        var group = root.GetComponentInChildren<LODGroup>(true);
        if (group == null)
        {
            lod0Count = all.Count;
            return all;
        }

        var lods = group.GetLODs();
        var rank = new Dictionary<MeshFilter, int>();
        for (int level = 0; level < lods.Length; level++)
        {
            foreach (var r in lods[level].renderers)
            {
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                // First level wins if a renderer is listed in several.
                if (mf != null && !rank.ContainsKey(mf)) rank[mf] = level;
            }
        }

        // Renderers outside the LODGroup have no level; treat them as LOD0 so they bake normally.
        var ordered = new List<MeshFilter>(all.Count);
        foreach (var mf in all) if (!rank.TryGetValue(mf, out int l) || l == 0) ordered.Add(mf);
        lod0Count = ordered.Count;

        var rest = new List<MeshFilter>();
        foreach (var mf in all) if (rank.TryGetValue(mf, out int l) && l > 0) rest.Add(mf);
        rest.Sort((a, b) => rank[a].CompareTo(rank[b]));
        ordered.AddRange(rest);
        return ordered;
    }

    private Mesh GetOrBake(Mesh source, Dictionary<Mesh, Mesh> baked,
                           ReferenceColors reference = null, Matrix4x4 toRoot = default)
    {
        if (baked.TryGetValue(source, out var existing)) return existing;

        // Re-baking an already-baked mesh would compound the split and drift the colors.
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (!string.IsNullOrEmpty(sourcePath) && sourcePath.StartsWith(outputFolder))
        {
            baked[source] = source;
            return source;
        }

        var result = FaceColorBaker.Bake(source, settings, reference, toRoot);
        if (result == null) return null;

        // Always the same path for a given source mesh: re-baking then overwrites in place, keeping
        // the asset GUID stable so existing prefab references survive.
        string path = $"{outputFolder}/{source.name}_FC.asset";
        var prior = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (prior != null)
        {
            EditorUtility.CopySerialized(result, prior);
            EditorUtility.SetDirty(prior);
            Object.DestroyImmediate(result);
            baked[source] = prior;
            return prior;
        }

        AssetDatabase.CreateAsset(result, path);
        baked[source] = result;
        return result;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        var parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
