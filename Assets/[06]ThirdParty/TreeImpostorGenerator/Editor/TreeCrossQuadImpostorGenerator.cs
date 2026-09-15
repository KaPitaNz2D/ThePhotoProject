using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System;


public class TreeCrossQuadImpostorGenerator : EditorWindow
{
    private GameObject sourceTree;
    private int textureSize = 512;
    private readonly int[] textureSizeOptions = { 128, 256, 512, 1024, 2048 };
    private int selectedTextureSizeIndex = 2; // Default to 512
    private bool createLOD = true;
    private float lodTransitionHeight = 0.3f;
    private Color backgroundColor = new Color(0, 0, 0, 0);

    private const int BakingLayer = 31;

    private Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();

    // Offset controls for each face
    private float frontQuadOffsetX = 0f;
    private float frontQuadOffsetZ = 0f;
    private float backQuadOffsetX = 0f;
    private float backQuadOffsetZ = 0f;
    private float rightQuadOffsetX = 0f;
    private float rightQuadOffsetZ = 0f;
    private float leftQuadOffsetX = 0f;
    private float leftQuadOffsetZ = 0f;

    // Preview system
    private bool isInPreviewMode = false;
    private GameObject previewContainer;
    private Material previewMaterial;
    private GameObject[] previewQuads = new GameObject[4];
    private float previewOffset = 10f; // Default offset
    private bool showSideBySide = true;

    [MenuItem("Tools/Roundy/Tree Cross Quad Impostor Generator")]
    public static void ShowWindow()
    {
        GetWindow<TreeCrossQuadImpostorGenerator>("Tree Cross Quad Impostor Generator v0.2");
    }

    private enum RenderPipelineType
    {
        BuiltIn,
        URP,
        HDRP
    }

    private bool bakeNormalMap = true;
    private bool materialOnly = false;
    private const string NormalCaptureShaderPath = "Hidden/Roundy/ImpostorNormalCapture";

    // Some renderers (seen on trees using GPU-instanced/Shader Graph vegetation materials, likely an
    // interaction with URP's GPU Resident Drawer) stop rendering entirely for the rest of this Editor
    // session once their GameObject.layer is changed by script - even after the layer is restored to
    // its original value, and even to a temp camera whose cullingMask matches. Moving the object's
    // world position (which reliably invalidates whatever cached batch/culling data caused this) does
    // not have that problem, so the tree is temporarily teleported far away and rendered there -
    // isolated from the rest of the scene by distance - instead of relying on layer-based culling alone.
    private static readonly Vector3 IsolatedBakePositionOffset = new Vector3(0f, 50000f, 0f);

    private RenderPipelineType selectedPipeline = RenderPipelineType.BuiltIn;
    private readonly GUIContent pipelineLabel = new("Render Pipeline", "Select the render pipeline used in your project");
    private Vector2 scrollPosition;
    private GUIStyle headerStyle;
    private GUIStyle sectionStyle;
    private Color headerColor = new(0.8f, 0.8f, 0.8f, 1f);

    private void InitializeStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 10)
            };
        }

        if (sectionStyle == null)
        {
            sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 8, 4)
            };
        }
    }


    private void OnGUI()
    {
        InitializeStyles();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Header Section
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("Tree Cross Quad Impostor Generator v0.1", headerStyle);
            GUILayout.FlexibleSpace();
        }
        EditorGUILayout.Space(10);

        // Main Settings Section
        DrawSection("Main Settings", () =>
        {
            selectedPipeline = (RenderPipelineType)EditorGUILayout.EnumPopup(pipelineLabel, selectedPipeline);

            sourceTree = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Source Tree", "The tree prefab to create an impostor for"),
                sourceTree, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Atlas Texture Size", "Size of the output texture atlas"));
            selectedTextureSizeIndex = EditorGUILayout.Popup(selectedTextureSizeIndex,
                System.Array.ConvertAll(textureSizeOptions, x => x.ToString()));

            EditorGUILayout.EndHorizontal();

            bakeNormalMap = EditorGUILayout.Toggle(
                new GUIContent("Bake Normal Map", "Also captures a per-view normal atlas so the impostor material can receive basic directional lighting"),
                bakeNormalMap);

            materialOnly = EditorGUILayout.Toggle(
                new GUIContent("Material Only", "Bakes the atlas(es) and creates/saves only the Material asset - no cross mesh, GameObject, LOD group, or prefab"),
                materialOnly);
        });
        DrawSection("Preview Settings", () =>
        {
            showSideBySide = EditorGUILayout.Toggle(
                new GUIContent("Side by Side View", "Show source tree and impostor side by side"),
                showSideBySide);

            if (showSideBySide)
            {
                previewOffset = EditorGUILayout.Slider(
                    new GUIContent("Preview Offset", "Distance between source tree and impostor"),
                    previewOffset, 0f, 10f);
            }
        });

        // Quad Adjustments Section
        DrawSection("Quad Adjustments", () =>
        {
            DrawQuadAdjustments("Front Face", ref frontQuadOffsetX, ref frontQuadOffsetZ);
            DrawQuadAdjustments("Back Face", ref backQuadOffsetX, ref backQuadOffsetZ);
            DrawQuadAdjustments("Right Face", ref rightQuadOffsetX, ref rightQuadOffsetZ);
            DrawQuadAdjustments("Left Face", ref leftQuadOffsetX, ref leftQuadOffsetZ);
        });

        // LOD Settings Section
        DrawSection("LOD Settings", () =>
        {
            using (new EditorGUI.DisabledScope(materialOnly))
            {
                createLOD = EditorGUILayout.Toggle(
                    new GUIContent("Create LOD", "Creates LOD setup with original tree and impostor"),
                    createLOD);

                if (createLOD)
                {
                    EditorGUI.indentLevel++;
                    lodTransitionHeight = EditorGUILayout.Slider(
                        new GUIContent("LOD Transition Height", "Screen height % when to switch to impostor"),
                        lodTransitionHeight, 0.1f, 0.5f);
                    EditorGUI.indentLevel--;
                }
            }
            if (materialOnly)
            {
                EditorGUILayout.HelpBox("Not used in Material Only mode.", MessageType.None);
            }
        });

        // Action Buttons Section
        EditorGUILayout.Space(20);
        DrawActionButtons();

        EditorGUILayout.EndScrollView();

        if (GUI.changed && isInPreviewMode)
        {
            UpdatePreviewQuads();
        }
    }

    private void DrawSection(string title, System.Action drawContent)
    {
        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, sectionStyle);
            }
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                drawContent();
            }
        }
    }

    private void DrawQuadAdjustments(string faceName, ref float offsetX, ref float offsetZ)
    {
        using (new EditorGUILayout.VerticalScope())
        {
            EditorGUILayout.LabelField(faceName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            offsetX = EditorGUILayout.Slider("X Offset", offsetX, -1f, 1f);
            offsetZ = EditorGUILayout.Slider("Z Offset", offsetZ, -1f, 1f);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawActionButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (!isInPreviewMode)
            {
                if (GUILayout.Button("Preview Impostor", GUILayout.Width(150), GUILayout.Height(30)))
                {
                    if (sourceTree == null)
                    {
                        EditorUtility.DisplayDialog("Error", "Please assign a Source Tree", "OK");
                        return;
                    }
                    StartPreviewMode();
                }
            }
            else
            {
                if (GUILayout.Button(materialOnly ? "Generate Material" : "Generate Final Impostor", GUILayout.Width(150), GUILayout.Height(30)))
                {
                    EndPreviewMode(true);
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(150), GUILayout.Height(30)))
                {
                    EndPreviewMode(false);
                }
            }
            GUILayout.FlexibleSpace();
        }
    }

    private string GetSelectedShaderPath()
    {
        return selectedPipeline switch
        {
            RenderPipelineType.URP => "Roundy/Vegetation/ImpostorCrossURP",
            RenderPipelineType.HDRP => "Roundy/Vegetation/ImpostorCrossHDRP",
            _ => "Roundy/Vegetation/ImpostorCrossBIRP"
        };
    }

    // Modify your CreateImpostorMaterial method to use the selected pipeline
    private Material CreateImpostorMaterial(Texture2D atlas, Texture2D normalAtlas)
    {
        string shaderName = GetSelectedShaderPath();
        Shader shader = Shader.Find(shaderName);

        if (shader == null)
        {
            Debug.LogError($"Could not find shader: {shaderName}. Please ensure the shader is included in your project.");
            return null;
        }

        Material material = new Material(shader);
        material.renderQueue = 2450;
        material.SetTexture("_MainTex", atlas);
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetFloat("_AlphaCoverageStrength", 1.0f);
        material.enableInstancing = true;
        material.doubleSidedGI = true;

        if (normalAtlas != null)
        {
            material.SetTexture("_BumpMap", normalAtlas);
        }

        if (material.mainTexture == null)
        {
            Debug.LogWarning("Manual texture reassignment needed");
            material.mainTexture = atlas;
        }

        return material;
    }

    private Material GetNormalCaptureMaterial()
    {
        Shader shader = Shader.Find(NormalCaptureShaderPath);
        if (shader == null)
        {
            Debug.LogWarning($"Could not find {NormalCaptureShaderPath}; skipping normal map bake for this run.");
            return null;
        }
        return new Material(shader);
    }

    private Dictionary<Renderer, Material[]> SwapMaterials(GameObject obj, Material replacement)
    {
        var saved = new Dictionary<Renderer, Material[]>();
        foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>(true))
        {
            saved[renderer] = renderer.sharedMaterials;
            Material[] replaced = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < replaced.Length; i++)
            {
                replaced[i] = replacement;
            }
            renderer.sharedMaterials = replaced;
        }
        return saved;
    }

    private void RestoreMaterials(Dictionary<Renderer, Material[]> saved)
    {
        foreach (var kvp in saved)
        {
            if (kvp.Key != null)
            {
                kvp.Key.sharedMaterials = kvp.Value;
            }
        }
    }
    private void CleanupHiddenBakingObjects()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var rootObjects = scene.GetRootGameObjects();
        List<GameObject> objectsToDestroy = new List<GameObject>();

        // Skip any objects that belong to our source tree
        HashSet<GameObject> sourceTreeObjects = new HashSet<GameObject>();
        if (sourceTree != null)
        {
            foreach (var transform in sourceTree.GetComponentsInChildren<Transform>(true))
            {
                sourceTreeObjects.Add(transform.gameObject);
            }
        }

        // Collect objects to destroy, excluding source tree objects
        foreach (var root in rootObjects)
        {
            var objects = root.GetComponentsInChildren<Transform>(true);
            foreach (var transform in objects)
            {
                if (transform.gameObject.layer == BakingLayer && !sourceTreeObjects.Contains(transform.gameObject))
                {
                    objectsToDestroy.Add(transform.gameObject);
                }
            }
        }

        foreach (var obj in objectsToDestroy)
        {
            if (obj != null)
            {
                Debug.Log($"Cleaning up leftover baking object: {obj.name}");
                DestroyImmediate(obj);
            }
        }
    }


    private void StartPreviewMode()
    {
        CleanupHiddenBakingObjects();
        isInPreviewMode = true;
        textureSize = textureSizeOptions[selectedTextureSizeIndex];

        StoreOriginalLayers(sourceTree);
        SetLayerRecursively(sourceTree, BakingLayer);

        try
        {
            Texture2D atlas = BakeTreeAtlasIsolated(out Texture2D normalAtlas);

            previewContainer = new GameObject("ImpostorPreview");

            if (showSideBySide)
            {
                // Move impostor to the right of the source tree
                previewContainer.transform.position = sourceTree.transform.position + Vector3.right * previewOffset;
            }
            else
            {
                previewContainer.transform.position = sourceTree.transform.position;
            }

            previewMaterial = CreateImpostorMaterial(atlas, normalAtlas);
            CreatePreviewQuads();

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            EditorApplication.update += OnEditorUpdate;
        }
        finally
        {
            RestoreOriginalLayers();
        }
    }


    private void CreatePreviewQuads()
    {
        Bounds bounds = CalculateBounds(sourceTree);
        Vector3 size = bounds.size;

        string[] quadNames = { "Front", "Back", "Right", "Left" };

        for (int i = 0; i < 4; i++)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"Preview_{quadNames[i]}";
            quad.transform.SetParent(previewContainer.transform);

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = previewMaterial;

            UpdateQuadUVs(quad, i);
            previewQuads[i] = quad;
        }

        UpdatePreviewQuads();
    }
    private void UpdateQuadUVs(GameObject quad, int quadIndex)
    {
        if (!viewBounds.TryGetValue(quadIndex, out ViewData viewData))
            return;

        Mesh mesh = quad.GetComponent<MeshFilter>().sharedMesh;
        Mesh newMesh = new Mesh();
        newMesh.vertices = mesh.vertices;
        newMesh.triangles = mesh.triangles;
        newMesh.normals = mesh.normals;

        float uvLeft = viewData.atlasX * 0.5f + viewData.bounds.x * 0.5f;
        float uvRight = uvLeft + viewData.bounds.width * 0.5f;
        float uvBottom = viewData.atlasY * 0.5f + viewData.bounds.y * 0.5f;
        float uvTop = uvBottom + viewData.bounds.height * 0.5f;

        Vector2[] uvs = new Vector2[4] {
            new Vector2(uvLeft, uvBottom),
            new Vector2(uvRight, uvBottom),
            new Vector2(uvLeft, uvTop),
            new Vector2(uvRight, uvTop)
        };

        newMesh.uv = uvs;
        quad.GetComponent<MeshFilter>().mesh = newMesh;
    }

    private void UpdatePreviewQuads()
    {
        if (previewContainer == null) return;

        Bounds bounds = CalculateBounds(sourceTree);
        Vector3 size = bounds.size;

        for (int i = 0; i < previewQuads.Length; i++)
        {
            GameObject quad = previewQuads[i];
            if (quad == null) continue;

            Vector3 position = previewContainer.transform.position;
            Vector3 rotation = Vector3.zero;
            Vector3 quadScale = Vector3.one;

            switch (i)
            {
                case 0: // Front
                    rotation = new Vector3(0, 0, 0);
                    quadScale = new Vector3(size.x, size.y, 1);
                    position += new Vector3(frontQuadOffsetX * size.x, size.y * 0.5f, frontQuadOffsetZ * size.z);
                    break;

                case 1: // Back
                    rotation = new Vector3(0, 180, 0);
                    quadScale = new Vector3(size.x, size.y, 1);
                    position += new Vector3(backQuadOffsetX * size.x, size.y * 0.5f, backQuadOffsetZ * size.z);
                    break;

                case 2: // Right
                    rotation = new Vector3(0, -90, 0);
                    quadScale = new Vector3(size.z, size.y, 1);
                    position += new Vector3(rightQuadOffsetX * size.x, size.y * 0.5f, rightQuadOffsetZ * size.z);
                    break;

                case 3: // Left
                    rotation = new Vector3(0, 90, 0);
                    quadScale = new Vector3(size.z, size.y, 1);
                    position += new Vector3(leftQuadOffsetX * size.x, size.y * 0.5f, leftQuadOffsetZ * size.z);
                    break;
            }

            quad.transform.position = position;
            quad.transform.rotation = Quaternion.Euler(rotation);
            quad.transform.localScale = quadScale;

            UpdateQuadUVs(quad, i);
        }

        SceneView.RepaintAll();
    }

    private void EndPreviewMode(bool finalize)
    {
        isInPreviewMode = false;
        EditorApplication.update -= OnEditorUpdate;

        if (finalize)
        {
            GenerateImpostor();
        }

        if (previewContainer != null)
        {
            DestroyImmediate(previewContainer);
        }
        if (previewMaterial != null)
        {
            DestroyImmediate(previewMaterial);
        }

        previewContainer = null;
        previewMaterial = null;
        previewQuads = new GameObject[4];

        RestoreOriginalLayers();
    }
    private void OnEditorUpdate()
    {
        if (isInPreviewMode && previewContainer != null)
        {
            UpdatePreviewQuads();
        }
    }



    private void GenerateImpostor()
    {
        // Store original layers before anything else
        StoreOriginalLayers(sourceTree);
        SetLayerRecursively(sourceTree, BakingLayer);

        try
        {
            textureSize = textureSizeOptions[selectedTextureSizeIndex];
            Texture2D atlas = BakeTreeAtlasIsolated(out Texture2D normalAtlas);
            Material impostorMaterial = CreateImpostorMaterial(atlas, normalAtlas);

            if (materialOnly)
            {
                SaveMaterialOnly(atlas, normalAtlas, impostorMaterial);
            }
            else
            {
                Mesh crossMesh = CreateCrossMesh();
                GameObject impostor = CreateImpostorGameObject(crossMesh, impostorMaterial);

                if (createLOD)
                {
                    SetupLODGroup(impostor);
                }

                SaveAssets(atlas, normalAtlas, crossMesh, impostorMaterial, impostor);
            }
        }
        finally
        {
            // Restore original layers in finally block
            RestoreOriginalLayers();
        }
    }




    private class ViewData
    {
        public Rect bounds; // Normalized bounds (0-1) of non-transparent area
        public int atlasX;  // Position in atlas
        public int atlasY;
    }

    private Dictionary<int, ViewData> viewBounds = new Dictionary<int, ViewData>();

    private static readonly Color NeutralNormal = new Color(0.5f, 0.5f, 1f, 1f);

    // Wraps BakeTreeAtlas, temporarily teleporting sourceTree far away (see IsolatedBakePositionOffset)
    // so the bake camera sees only the tree with a plain, reliable cullingMask - working around a case
    // where some renderers stop rendering after a script-side layer change alone. The tree's position is
    // always restored before returning, including on exception.
    private Texture2D BakeTreeAtlasIsolated(out Texture2D normalAtlas)
    {
        Vector3 originalPosition = sourceTree.transform.position;
        sourceTree.transform.position = originalPosition + IsolatedBakePositionOffset;
        try
        {
            return BakeTreeAtlas(out normalAtlas);
        }
        finally
        {
            sourceTree.transform.position = originalPosition;
        }
    }

    private Texture2D BakeTreeAtlas(out Texture2D normalAtlas)
    {
        viewBounds.Clear();

        GameObject cameraGO = new GameObject("TempCamera");
        Camera camera = cameraGO.AddComponent<Camera>();
        ConfigureCamera(camera);

        Material normalCaptureMaterial = bakeNormalMap ? GetNormalCaptureMaterial() : null;
        bool doNormalBake = normalCaptureMaterial != null;

        try
        {
            Bounds treeBounds = CalculateBounds(sourceTree);

            int atlasWidth = textureSize * 2;
            int atlasHeight = textureSize * 2;
            Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
            normalAtlas = doNormalBake ? new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false) : null;

            Color[] clearPixels = new Color[atlasWidth * atlasHeight];
            for (int i = 0; i < clearPixels.Length; i++)
                clearPixels[i] = Color.clear;
            atlas.SetPixels(clearPixels);
            atlas.Apply();

            if (doNormalBake)
            {
                Color[] neutralPixels = new Color[atlasWidth * atlasHeight];
                for (int i = 0; i < neutralPixels.Length; i++)
                    neutralPixels[i] = NeutralNormal;
                normalAtlas.SetPixels(neutralPixels);
                normalAtlas.Apply();
            }

            (Vector3 direction, int x, int y)[] views = new (Vector3, int, int)[]
            {
                (Vector3.forward, 0, 0),
                (Vector3.back, 1, 0),
                (Vector3.left, 1, 1),
                (Vector3.right, 0, 1),
            };

            foreach (var (direction, atlasX, atlasY) in views)
            {
                float distance = CalculateCameraDistance(camera, treeBounds);
                camera.transform.position = treeBounds.center - direction * distance;
                camera.transform.LookAt(treeBounds.center);

                RenderTexture rt = null;
                Texture2D snapshot = null;
                RenderTexture normalRt = null;
                Texture2D normalSnapshot = null;

                try
                {
                    rt = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32);
                    rt.Create();

                    camera.targetTexture = rt;
                    camera.Render();

                    snapshot = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
                    RenderTexture.active = rt;
                    snapshot.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
                    snapshot.Apply();

                    // Clear active render texture before processing
                    RenderTexture.active = null;
                    camera.targetTexture = null;

                    ProcessTextureTransparency(snapshot, backgroundColor);

                    Rect contentBounds = FindTextureContentBounds(snapshot);
                    viewBounds[Array.IndexOf(views, (direction, atlasX, atlasY))] = new ViewData
                    {
                        bounds = contentBounds,
                        atlasX = atlasX,
                        atlasY = atlasY
                    };

                    int xPos = atlasX * textureSize;
                    int yPos = atlasY * textureSize;
                    Color[] snapshotPixels = snapshot.GetPixels();
                    atlas.SetPixels(xPos, yPos, textureSize, textureSize, snapshotPixels);

                    if (doNormalBake)
                    {
                        // Re-render the same view with the normal-capture material swapped onto the
                        // source tree, using the same camera/transform so the silhouette lines up
                        // exactly with the albedo pass above.
                        var savedMaterials = SwapMaterials(sourceTree, normalCaptureMaterial);
                        try
                        {
                            normalRt = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32);
                            normalRt.Create();

                            camera.targetTexture = normalRt;
                            camera.Render();

                            normalSnapshot = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
                            RenderTexture.active = normalRt;
                            normalSnapshot.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
                            normalSnapshot.Apply();

                            RenderTexture.active = null;
                            camera.targetTexture = null;
                        }
                        finally
                        {
                            RestoreMaterials(savedMaterials);
                        }

                        // Fill anything outside the albedo silhouette with a neutral "flat" normal so
                        // bilinear filtering doesn't blend valid normals against the transparent clear
                        // color at the silhouette edge.
                        Color[] normalPixels = normalSnapshot.GetPixels();
                        for (int p = 0; p < normalPixels.Length; p++)
                        {
                            if (snapshotPixels[p].a <= 0.001f)
                                normalPixels[p] = NeutralNormal;
                        }
                        normalSnapshot.SetPixels(normalPixels);
                        normalSnapshot.Apply();

                        normalAtlas.SetPixels(xPos, yPos, textureSize, textureSize, normalPixels);
                    }
                }
                finally
                {
                    // Clear camera target first
                    if (camera != null)
                        camera.targetTexture = null;

                    // Clear active render texture
                    RenderTexture.active = null;

                    // Clean up snapshot
                    if (snapshot != null)
                        DestroyImmediate(snapshot);
                    if (normalSnapshot != null)
                        DestroyImmediate(normalSnapshot);

                    // Clean up render texture
                    if (rt != null)
                    {
                        rt.Release();
                        DestroyImmediate(rt);
                    }
                    if (normalRt != null)
                    {
                        normalRt.Release();
                        DestroyImmediate(normalRt);
                    }
                }
            }

            atlas.Apply();
            if (doNormalBake)
                normalAtlas.Apply();
            return atlas;
        }
        finally
        {
            if (cameraGO != null)
            {
                DestroyImmediate(cameraGO);
            }
            if (normalCaptureMaterial != null)
            {
                DestroyImmediate(normalCaptureMaterial);
            }
        }
    }



    private Rect FindTextureContentBounds(Texture2D texture)
    {
        Color[] pixels = texture.GetPixels();
        int width = texture.width;
        int height = texture.height;

        int xMin = width, xMax = 0;
        int yMin = height, yMax = 0;

        // Find bounds of non-transparent pixels
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = pixels[y * width + x];
                if (pixel.a > 0.01f)
                {
                    xMin = Mathf.Min(xMin, x);
                    xMax = Mathf.Max(xMax, x);
                    yMin = Mathf.Min(yMin, y);
                    yMax = Mathf.Max(yMax, y);
                }
            }
        }



        // Convert to normalized coordinates (0-1)
        return new Rect(
            (float)xMin / width,
            (float)yMin / height,
            (float)(xMax - xMin) / width,
            (float)(yMax - yMin) / height
        );
    }

    private Mesh CreateCrossMesh()
    {
        Bounds bounds = CalculateBounds(sourceTree);
        Vector3 size = bounds.size;
        Vector3 center = bounds.center - sourceTree.transform.position;

        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        List<Vector3> tangentDirs = new List<Vector3>();

        for (int i = 0; i < 4; i++)
        {
            if (!viewBounds.TryGetValue(i, out ViewData viewData))
                continue;

            float width;
            float xOffset = 0f;
            float zOffset = 0f;
            float height = size.y;

            // Get the correct dimensions and offsets for each face
            switch (i)
            {
                case 0: // Front
                    width = size.x;
                    xOffset = frontQuadOffsetX * size.x;
                    zOffset = frontQuadOffsetZ * size.z;
                    break;
                case 1: // Back
                    width = size.x;
                    xOffset = backQuadOffsetX * size.x;
                    zOffset = backQuadOffsetZ * size.z;
                    break;
                case 2: // Right
                    width = size.z;
                    xOffset = rightQuadOffsetX * size.x;
                    zOffset = rightQuadOffsetZ * size.z;
                    break;
                case 3: // Left
                    width = size.z;
                    xOffset = leftQuadOffsetX * size.x;
                    zOffset = leftQuadOffsetZ * size.z;
                    break;
                default:
                    continue;
            }

            float uvLeft = viewData.atlasX * 0.5f + viewData.bounds.x * 0.5f;
            float uvRight = uvLeft + viewData.bounds.width * 0.5f;
            float uvBottom = viewData.atlasY * 0.5f + viewData.bounds.y * 0.5f;
            float uvTop = uvBottom + viewData.bounds.height * 0.5f;

            Vector3[] quadVerts = new Vector3[4];
            Vector2[] quadUVs = new Vector2[4];

            // Define vertices for each face with proper offsets
            switch (i)
            {
                case 0: // Front
                    quadVerts[0] = new Vector3(-width * 0.5f + xOffset, 0, zOffset);
                    quadVerts[1] = new Vector3(width * 0.5f + xOffset, 0, zOffset);
                    quadVerts[2] = new Vector3(-width * 0.5f + xOffset, height, zOffset);
                    quadVerts[3] = new Vector3(width * 0.5f + xOffset, height, zOffset);
                    break;

                case 1: // Back
                    quadVerts[0] = new Vector3(width * 0.5f + xOffset, 0, -zOffset);
                    quadVerts[1] = new Vector3(-width * 0.5f + xOffset, 0, -zOffset);
                    quadVerts[2] = new Vector3(width * 0.5f + xOffset, height, -zOffset);
                    quadVerts[3] = new Vector3(-width * 0.5f + xOffset, height, -zOffset);
                    break;

                case 2: // Right
                    quadVerts[0] = new Vector3(xOffset, 0, -width * 0.5f + zOffset);
                    quadVerts[1] = new Vector3(xOffset, 0, width * 0.5f + zOffset);
                    quadVerts[2] = new Vector3(xOffset, height, -width * 0.5f + zOffset);
                    quadVerts[3] = new Vector3(xOffset, height, width * 0.5f + zOffset);
                    break;

                case 3: // Left
                    quadVerts[0] = new Vector3(-xOffset, 0, width * 0.5f + zOffset);
                    quadVerts[1] = new Vector3(-xOffset, 0, -width * 0.5f + zOffset);
                    quadVerts[2] = new Vector3(-xOffset, height, width * 0.5f + zOffset);
                    quadVerts[3] = new Vector3(-xOffset, height, -width * 0.5f + zOffset);
                    break;
            }

            // Set UVs
            quadUVs[0] = new Vector2(uvLeft, uvBottom);
            quadUVs[1] = new Vector2(uvRight, uvBottom);
            quadUVs[2] = new Vector2(uvLeft, uvTop);
            quadUVs[3] = new Vector2(uvRight, uvTop);

            // Add vertices and UVs
            int baseIndex = vertices.Count;
            vertices.AddRange(quadVerts);
            uvs.AddRange(quadUVs);

            // Add triangles with correct winding order
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 1);

            // Tangent = direction of increasing U across the quad (vert0 -> vert1), which is what
            // the baked normal atlas was captured relative to (see ImpostorNormalCapture.shader).
            Vector3 tangentDir = (quadVerts[1] - quadVerts[0]).normalized;
            for (int v = 0; v < 4; v++)
            {
                tangentDirs.Add(tangentDir);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Build per-vertex tangents (with correct handedness) from the recalculated normals and the
        // per-quad tangent directions above, so the impostor shaders can build a TBN matrix that
        // matches the normal atlas' capture basis (tangent = view right, bitangent = world up).
        Vector3[] finalNormals = mesh.normals;
        Vector4[] tangents = new Vector4[finalNormals.Length];
        for (int v = 0; v < finalNormals.Length; v++)
        {
            Vector3 n = finalNormals[v];
            Vector3 t = tangentDirs[v] - n * Vector3.Dot(n, tangentDirs[v]);
            if (t.sqrMagnitude < 1e-6f)
                t = Vector3.Cross(Vector3.up, n);
            t.Normalize();

            float w = Vector3.Dot(Vector3.Cross(n, t), Vector3.up) >= 0f ? 1f : -1f;
            tangents[v] = new Vector4(t.x, t.y, t.z, w);
        }
        mesh.SetTangents(tangents);

        return mesh;
    }



    private GameObject CreateImpostorGameObject(Mesh mesh, Material material)
    {
        GameObject impostor = new GameObject(sourceTree.name + "_Impostor");
        impostor.transform.position = sourceTree.transform.position;
        impostor.transform.rotation = sourceTree.transform.rotation;

        MeshFilter meshFilter = impostor.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = impostor.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        return impostor;
    }

    private void SetupLODGroup(GameObject impostor)
    {
        LODGroup lodGroup = sourceTree.GetComponent<LODGroup>();
        if (lodGroup == null)
        {
            lodGroup = sourceTree.AddComponent<LODGroup>();
        }

        // Get renderers
        Renderer[] originalRenderers = sourceTree.GetComponentsInChildren<Renderer>();
        Renderer impostorRenderer = impostor.GetComponent<Renderer>();

        // Create LOD levels
        LOD[] lods = new LOD[2];
        lods[0] = new LOD(lodTransitionHeight, originalRenderers);
        lods[1] = new LOD(0.01f, new Renderer[] { impostorRenderer });

        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();

        // Parent impostor to source tree
        impostor.transform.SetParent(sourceTree.transform);
    }

    // Writes an atlas to disk, imports it, and configures import settings. isNormalData selects
    // plain non-sRGB "raw data" import settings (used for the baked normal atlas) instead of the
    // sRGB alpha-cutout settings used for the albedo atlas.
    private Texture2D SaveAtlasTexture(Texture2D atlas, string texturePath, string suffix, string uniqueID, bool isNormalData)
    {
        string assetPath = Path.Combine(texturePath, $"{sourceTree.name}_{suffix}_{uniqueID}.png");
        File.WriteAllBytes(assetPath, atlas.EncodeToPNG());
        AssetDatabase.ImportAsset(assetPath);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.alphaIsTransparency = !isNormalData;
            importer.sRGBTexture = !isNormalData;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private void SaveAssets(Texture2D atlas, Texture2D normalAtlas, Mesh mesh, Material material, GameObject impostor)
    {
        string uniqueID = GenerateUniqueID();

        // Create and setup directories
        string rootPath = "Assets/TreeImpostors";
        string texturePath = Path.Combine(rootPath, "Textures");
        string meshPath = Path.Combine(rootPath, "Meshes");
        string materialPath = Path.Combine(rootPath, "Materials");
        string prefabPath = Path.Combine(rootPath, "Prefabs");

        CreateDirectoryIfNeeded(rootPath);
        CreateDirectoryIfNeeded(texturePath);
        CreateDirectoryIfNeeded(meshPath);
        CreateDirectoryIfNeeded(materialPath);
        CreateDirectoryIfNeeded(prefabPath);

        Texture2D savedAtlas = SaveAtlasTexture(atlas, texturePath, "Atlas", uniqueID, isNormalData: false);

        // Save normal atlas texture, if baked. Imported as plain (non-sRGB, non-"Normal map" type)
        // data since it already stores a raw pre-encoded normal, not Unity's DXT5nm normal format.
        if (normalAtlas != null)
        {
            Texture2D savedNormal = SaveAtlasTexture(normalAtlas, texturePath, "Normal", uniqueID, isNormalData: true);
            material.SetTexture("_BumpMap", savedNormal);
        }

        // Save mesh
        string meshAssetPath = Path.Combine(meshPath, $"{sourceTree.name}_Mesh_{uniqueID}.asset");
        AssetDatabase.CreateAsset(mesh, meshAssetPath);

        // Save material and ensure texture is assigned
        string materialAssetPath = Path.Combine(materialPath, $"{sourceTree.name}_Material_{uniqueID}.mat");
        material.mainTexture = savedAtlas;
        AssetDatabase.CreateAsset(material, materialAssetPath);

        // Save prefab
        string prefabAssetPath = Path.Combine(prefabPath, $"{sourceTree.name}_Impostor_{uniqueID}.prefab");
        PrefabUtility.SaveAsPrefabAsset(impostor, prefabAssetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // Bakes/saves only the atlas texture(s) and Material asset - no cross mesh, GameObject, LOD
    // group, or prefab. Useful for re-baking a material (e.g. to add/update the normal map) for an
    // impostor whose mesh already exists and doesn't need to be regenerated.
    private void SaveMaterialOnly(Texture2D atlas, Texture2D normalAtlas, Material material)
    {
        string uniqueID = GenerateUniqueID();

        string rootPath = "Assets/TreeImpostors";
        string texturePath = Path.Combine(rootPath, "Textures");
        string materialPath = Path.Combine(rootPath, "Materials");

        CreateDirectoryIfNeeded(rootPath);
        CreateDirectoryIfNeeded(texturePath);
        CreateDirectoryIfNeeded(materialPath);

        Texture2D savedAtlas = SaveAtlasTexture(atlas, texturePath, "Atlas", uniqueID, isNormalData: false);
        material.mainTexture = savedAtlas;

        if (normalAtlas != null)
        {
            Texture2D savedNormal = SaveAtlasTexture(normalAtlas, texturePath, "Normal", uniqueID, isNormalData: true);
            material.SetTexture("_BumpMap", savedNormal);
        }

        string materialAssetPath = Path.Combine(materialPath, $"{sourceTree.name}_Material_{uniqueID}.mat");
        AssetDatabase.CreateAsset(material, materialAssetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Saved material-only impostor assets at: {materialAssetPath}");
    }
    private string GenerateUniqueID()
    {
        return DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
    }

    #region Helper Methods



    private void StoreOriginalLayers(GameObject obj)
    {
        originalLayers.Clear();
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
        {
            originalLayers[child.gameObject] = child.gameObject.layer;
        }
    }

    private void RestoreOriginalLayers()
    {
        foreach (var kvp in originalLayers)
        {
            if (kvp.Key != null)
            {
                kvp.Key.layer = kvp.Value;
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private void ProcessTextureTransparency(Texture2D texture, Color bgColor)
    {
        Color[] pixels = texture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            if (ColorMatch(pixel, bgColor))
            {
                pixels[i] = Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
    }

    private bool ColorMatch(Color a, Color b, float tolerance = 0.01f)
    {
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }

    private float CalculateCameraDistance(Camera camera, Bounds bounds)
    {
        // Calculate the size needed to fit the object
        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        camera.orthographicSize = maxSize * 0.5f;

        // Add padding to ensure the whole tree is visible
        return bounds.extents.magnitude * 2.5f;
    }

    private void ConfigureCamera(Camera camera)
    {
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = backgroundColor;
        camera.orthographic = true;
        camera.cullingMask = 1 << BakingLayer;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 1000f;
    }


    private void CreateDirectoryIfNeeded(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    private void OnDestroy()
    {
        if (isInPreviewMode)
        {
            EndPreviewMode(false);
        }

        if (originalLayers != null)
        {
            RestoreOriginalLayers();
        }

        CleanupHiddenBakingObjects();
    }

    #endregion

    #region Preview Handling

    private PreviewRenderUtility previewUtility;
    private Vector2 previewScrollPosition;
    private float previewRotation = 0f;
    private GameObject previewInstance;

    private void OnEnable()
    {
        if (previewUtility == null)
        {
            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.transform.position = new Vector3(0, 3, -5);
            previewUtility.camera.transform.rotation = Quaternion.Euler(30, 0, 0);
            previewUtility.lights[0].intensity = 1f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(30, 30, 0);
            previewUtility.ambientColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        }
    }

    private void OnDisable()
    {
        if (previewUtility != null)
        {
            previewUtility.Cleanup();
            previewUtility = null;
        }

        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
        }
    }

    public bool HasPreviewGUI()
    {
        return sourceTree != null;
    }

    public void OnPreviewGUI(Rect r, GUIStyle background)
    {
        if (sourceTree == null || previewUtility == null)
            return;

        // Handle preview rotation
        previewRotation += HandlePreviewRotation(r, previewRotation);

        // Setup preview
        previewUtility.BeginPreview(r, background);

        // Create preview instance if needed
        if (previewInstance == null)
        {
            previewInstance = Instantiate(sourceTree);
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
        }

        // Update rotation
        previewInstance.transform.rotation = Quaternion.Euler(0, previewRotation, 0);

        // Ensure camera can see the whole tree
        Bounds bounds = CalculateBounds(previewInstance);
        float objectSize = bounds.size.magnitude;
        Vector3 cameraPosition = bounds.center - previewUtility.camera.transform.forward * objectSize * 2f;
        previewUtility.camera.transform.position = cameraPosition;

        // Render preview
        previewUtility.camera.Render();

        // Draw the preview
        Texture resultRender = previewUtility.EndPreview();
        GUI.DrawTexture(r, resultRender, ScaleMode.StretchToFill, false);
    }

    private float HandlePreviewRotation(Rect previewRect, float currentRotation)
    {
        Event evt = Event.current;
        if (evt.type == EventType.MouseDrag && previewRect.Contains(evt.mousePosition))
        {
            if (evt.button == 0)
            {
                return evt.delta.x;
            }
        }
        return 0f;
    }

    public void OnPreviewSettings()
    {
        if (sourceTree == null)
            return;

        GUILayout.Label("Rotate: Left Mouse Button");
    }

    #endregion

    #region Additional Settings Window

    private class AdvancedSettingsWindow : EditorWindow
    {
        public TreeCrossQuadImpostorGenerator parent;

        private void OnGUI()
        {
            if (parent == null)
            {
                Close();
                return;
            }

            GUILayout.Label("Advanced Settings", EditorStyles.boldLabel);

            EditorGUILayout.Space();


        }
    }

    private void ShowAdvancedSettings()
    {
        AdvancedSettingsWindow window = EditorWindow.GetWindow<AdvancedSettingsWindow>("Advanced Settings");
        window.parent = this;
    }

    #endregion

    #region Validation and Error Handling

    private bool ValidateTree(GameObject tree)
    {
        if (tree == null)
            return false;

        // Check for required components
        Renderer[] renderers = tree.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            EditorUtility.DisplayDialog("Invalid Tree",
                "The selected object has no renderers. Please select a valid tree object.", "OK");
            return false;
        }

        // Check for valid materials
        foreach (Renderer renderer in renderers)
        {
            if (renderer.sharedMaterial == null)
            {
                EditorUtility.DisplayDialog("Invalid Materials",
                    "One or more renderers have missing materials. Please check the tree's materials.", "OK");
                return false;
            }
        }

        return true;
    }

    private void HandleError(string message, System.Exception ex = null)
    {
        string errorMessage = message;
        if (ex != null)
        {
            errorMessage += $"\n\nError details: {ex.Message}";
            Debug.LogError($"{message}\n{ex}");
        }
        EditorUtility.DisplayDialog("Error", errorMessage, "OK");
    }

    #endregion
}