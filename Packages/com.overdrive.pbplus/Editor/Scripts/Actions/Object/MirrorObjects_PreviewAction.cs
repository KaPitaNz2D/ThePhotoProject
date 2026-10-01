using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;
using Overdrive.ProBuilderPlus;
using UHandleUtility = UnityEditor.HandleUtility;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Mirror Objects action with live wireframe preview.
    /// Shows preview of mirror result before applying changes.
    /// </summary>
    [ProBuilderPlusAction("mirror_objects_preview", "Mirror",
        Tooltip = "Mirror objects with live preview - shows wireframe of mirror result",
        Instructions = "Choose mirror axes and duplicate option, then preview the cyan wireframe result.",
        IconPath = "Icons/Old/Object_Mirror",
        ValidModes = ToolMode.Object,
        ObjectsSelectedRequired = 1,
        Order = 52)]
    public sealed class MirrorObjectsPreviewAction : PreviewMenuAction
    {
        // Mirror settings (matching original MirrorObjects)
        [System.Flags]
        enum MirrorSettings
        {
            X = 0x1,
            Y = 0x2,
            Z = 0x4,
            Duplicate = 0x8
        }

        // Settings
        private MirrorSettings m_MirrorSettings = MirrorSettings.X;

        // Preview state
        private ProBuilderMesh[] m_CachedMeshes;
        private Vector3[][] m_OriginalVertices;    // Original vertex positions (world space)
        private Vector3[][] m_MirroredVertices;    // Mirrored vertex positions (world space)
        private Edge[][] m_MeshEdges;              // Edges for each mesh
        
        // Visual settings
        private static readonly Color ORIGINAL_COLOR = Color.white;    // Original position (when duplicating)
        private static readonly Color MIRRORED_COLOR = Color.cyan;    // Mirrored result
        private const float WIREFRAME_THICKNESS = 2.0f;

        

   
        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_MirrorSettings = (MirrorSettings)Overdrive.ProBuilderPlus.UserPreferences.Tools.MirrorSettings;

            bool x = (m_MirrorSettings & MirrorSettings.X) != 0;
            bool y = (m_MirrorSettings & MirrorSettings.Y) != 0;
            bool z = (m_MirrorSettings & MirrorSettings.Z) != 0;

            var xToggle = new Toggle("X Axis") { value = x };
            xToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    m_MirrorSettings |= MirrorSettings.X;
                else
                    m_MirrorSettings &= ~MirrorSettings.X;
                Overdrive.ProBuilderPlus.UserPreferences.Tools.MirrorSettings = (int)m_MirrorSettings;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(xToggle);

            var yToggle = new Toggle("Y Axis") { value = y };
            yToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    m_MirrorSettings |= MirrorSettings.Y;
                else
                    m_MirrorSettings &= ~MirrorSettings.Y;
                Overdrive.ProBuilderPlus.UserPreferences.Tools.MirrorSettings = (int)m_MirrorSettings;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(yToggle);

            var zToggle = new Toggle("Z Axis") { value = z };
            zToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    m_MirrorSettings |= MirrorSettings.Z;
                else
                    m_MirrorSettings &= ~MirrorSettings.Z;
                Overdrive.ProBuilderPlus.UserPreferences.Tools.MirrorSettings = (int)m_MirrorSettings;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(zToggle);

            bool duplicate = (m_MirrorSettings & MirrorSettings.Duplicate) != 0;
            var duplicateToggle = new Toggle("Duplicate") { value = duplicate };
            duplicateToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    m_MirrorSettings |= MirrorSettings.Duplicate;
                else
                    m_MirrorSettings &= ~MirrorSettings.Duplicate;
                Overdrive.ProBuilderPlus.UserPreferences.Tools.MirrorSettings = (int)m_MirrorSettings;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(duplicateToggle);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            m_MirrorSettings = (MirrorSettings)Overdrive.ProBuilderPlus.UserPreferences.Tools.MirrorSettings;

            // Cache the current selection
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_OriginalVertices = new Vector3[selection.Length][];
            m_MirroredVertices = new Vector3[selection.Length][];
            m_MeshEdges = new Edge[selection.Length][];

            // Cache original vertex positions and mesh edges
            for (int i = 0; i < selection.Length; i++)
            {
                var mesh = selection[i];
                var vertices = mesh.GetVertices();
                
                // Convert to world space
                m_OriginalVertices[i] = new Vector3[vertices.Length];
                for (int j = 0; j < vertices.Length; j++)
                {
                    m_OriginalVertices[i][j] = mesh.transform.TransformPoint(vertices[j].position);
                }
                
                // Get mesh edges for wireframe drawing
                m_MeshEdges[i] = GetMeshEdges(mesh);
            }

            // Subscribe to scene GUI for preview visualization
            SceneView.duringSceneGui += OnSceneGUI;

            // Calculate initial preview
            UpdatePreview();

        }

        public override void UpdatePreview()
        {
            if (m_CachedMeshes == null) return;

            // Calculate mirrored positions for all meshes
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var originalVertices = m_OriginalVertices[i];
                
                // Calculate mirror scale vector
                Vector3 scale = new Vector3(
                    (m_MirrorSettings & MirrorSettings.X) > 0 ? -1f : 1f,
                    (m_MirrorSettings & MirrorSettings.Y) > 0 ? -1f : 1f,
                    (m_MirrorSettings & MirrorSettings.Z) > 0 ? -1f : 1f);

                // Calculate mirrored vertex positions
                m_MirroredVertices[i] = new Vector3[originalVertices.Length];
                for (int j = 0; j < originalVertices.Length; j++)
                {
                    // Convert back to local space relative to object
                    Vector3 localPos = mesh.transform.InverseTransformPoint(originalVertices[j]);
                    
                    // Apply mirror scale
                    Vector3 mirroredLocalPos = Vector3.Scale(localPos, scale);
                    
                    // Convert back to world space
                    m_MirroredVertices[i][j] = mesh.transform.TransformPoint(mirroredLocalPos);
                }
            }

            // Repaint to show updated preview
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            
            // Check if any mirror axes are selected
            if ((m_MirrorSettings & (MirrorSettings.X | MirrorSettings.Y | MirrorSettings.Z)) == 0)
            {
                return new ActionResult(ActionResult.Status.Canceled, "No mirror axes selected");
            }

            // Calculate scale vector
            Vector3 scale = new Vector3(
                (m_MirrorSettings & MirrorSettings.X) > 0 ? -1f : 1f,
                (m_MirrorSettings & MirrorSettings.Y) > 0 ? -1f : 1f,
                (m_MirrorSettings & MirrorSettings.Z) > 0 ? -1f : 1f);

            bool duplicate = (m_MirrorSettings & MirrorSettings.Duplicate) != 0;
            var resultMeshes = new List<GameObject>();

            // Apply mirror operation to each mesh using inline implementation
            foreach (var mesh in m_CachedMeshes)
            {
                try
                {
                    var mirroredMesh = MirrorMesh(mesh, scale, duplicate);
                    resultMeshes.Add(mirroredMesh.gameObject);
                    
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to mirror '{mesh.name}': {ex.Message}");
                }
            }

            // Update selection to the result
            if (resultMeshes.Count > 0)
            {
                Selection.objects = resultMeshes.ToArray();
            }

            // Refresh ProBuilder
            ProBuilderEditor.Refresh();

            // Return result
            if (resultMeshes.Count > 0)
            {
                string operation = duplicate ? "Mirrored and duplicated" : "Mirrored";
                string message = resultMeshes.Count == 1 ? 
                    $"{operation} 1 object" : 
                    $"{operation} {resultMeshes.Count} objects";
                return new ActionResult(ActionResult.Status.Success, message);
            }
            else
            {
                return new ActionResult(ActionResult.Status.Failure, "Failed to mirror any objects");
            }
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_OriginalVertices = null;
            m_MirroredVertices = null;
            m_MeshEdges = null;
            SceneView.RepaintAll();
            
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawWireframePreviews();
        }

        private void DrawWireframePreviews()
        {
            if (m_CachedMeshes == null || m_MirroredVertices == null) return;

            // Set z-test to always show wireframes on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            bool duplicate = (m_MirrorSettings & MirrorSettings.Duplicate) != 0;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                if (mesh == null) continue;

                var originalVertices = m_OriginalVertices[meshIndex];
                var mirroredVertices = m_MirroredVertices[meshIndex];
                var edges = m_MeshEdges[meshIndex];

                // Draw white wireframe at original position (when duplicating)
                if (duplicate)
                {
                    Handles.color = ORIGINAL_COLOR;
                    DrawWireframeForVertices(originalVertices, edges);
                }

                // Draw cyan wireframe at mirrored position
                Handles.color = MIRRORED_COLOR;
                DrawWireframeForVertices(mirroredVertices, edges);
            }

            // Reset handles color
            Handles.color = Color.white;
        }

        private void DrawWireframeForVertices(Vector3[] vertices, Edge[] edges)
        {
            foreach (var edge in edges)
            {
                if (edge.a < vertices.Length && edge.b < vertices.Length)
                {
                    Handles.DrawLine(vertices[edge.a], vertices[edge.b], WIREFRAME_THICKNESS);
                }
            }
        }

        /// <summary>
        /// Mirror a ProBuilderMesh (inline implementation based on original MirrorObjects.Mirror)
        /// </summary>
        private ProBuilderMesh MirrorMesh(ProBuilderMesh pb, Vector3 scale, bool duplicate = true)
        {
            ProBuilderMesh mirroredObject;

            if (duplicate)
            {
                mirroredObject = UnityEngine.Object.Instantiate(pb.gameObject, pb.transform.parent, false).GetComponent<ProBuilderMesh>();
                mirroredObject.MakeUnique();
                mirroredObject.transform.parent = pb.transform.parent;
                mirroredObject.transform.localRotation = pb.transform.localRotation;
                Undo.RegisterCreatedObjectUndo(mirroredObject.gameObject, "Mirror Object");
            }
            else
            {
                Undo.RecordObject(pb, "Mirror");
                Undo.RecordObject(pb.transform, "Mirror");
                mirroredObject = pb;
            }

            Vector3 lScale = mirroredObject.gameObject.transform.localScale;
            mirroredObject.transform.localScale = scale;

            // if flipping on an odd number of axes, flip winding order
            if ((scale.x * scale.y * scale.z) < 0)
            {
                foreach (var face in mirroredObject.faces)
                    face.Reverse();
            }

            // Use MeshTransform.FreezeScaleTransform extension method
            UnityEngine.ProBuilder.MeshOperations.MeshTransform.FreezeScaleTransform(mirroredObject);
            mirroredObject.transform.localScale = lScale;

            mirroredObject.ToMesh();
            mirroredObject.Refresh();
            mirroredObject.Optimize();

            return mirroredObject;
        }

        /// <summary>
        /// Get all edges from all faces in the mesh for wireframe drawing
        /// </summary>
        private Edge[] GetMeshEdges(ProBuilderMesh mesh)
        {
            var edges = new List<Edge>();
            var addedEdges = new HashSet<Edge>();

            // Collect all edges from all faces
            foreach (var face in mesh.faces)
            {
                var faceEdges = face.edges;
                foreach (var edge in faceEdges)
                {
                    // Avoid duplicate edges (since faces share edges)
                    if (addedEdges.Add(edge))
                    {
                        edges.Add(edge);
                    }
                }
            }

            return edges.ToArray();
        }
    }
}
