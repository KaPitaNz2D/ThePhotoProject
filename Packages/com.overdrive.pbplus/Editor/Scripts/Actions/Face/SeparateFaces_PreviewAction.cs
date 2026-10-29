using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Separate Faces preview action that combines DetachFaces and DuplicateFaces functionality
    /// with live preview showing plus symbol and colored perimeter outline.
    /// </summary>
    [ProBuilderPlusAction("separate-faces", "Separate",
        Tooltip = "Separate selected faces with options for new object and duplication",
        Instructions = "Separate selected faces (plus symbol shows separation)",
        IconPath = "Icons/Old/Face_Detach",
        ValidModes = ToolMode.Face,
        FacesSelectedRequired = 1,
        Order = 155)]
    public sealed class SeparateFacesPreviewAction : PreviewMenuAction
    {
        // Settings
        private bool m_CreateNewObject;  // New Object checkbox
        private bool m_DuplicateFaces;  // Duplicate checkbox
        
        // Preview state
        private ProBuilderMesh[] m_CachedMeshes;
        private Face[][] m_CachedSelectedFaces;
        private Vector3[][] m_IndividualFaceCenters; // Center of each individual face
        private List<(Vector3, Vector3)>[] m_PerimeterEdges;

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_CreateNewObject = UserPreferences.Tools.SeparateFacesCreateNewObject;
            m_DuplicateFaces = UserPreferences.Tools.SeparateFacesDuplicateFaces;

            // New Object checkbox
            var newObjectToggle = new Toggle("New Object");
            newObjectToggle.tooltip = "Create a new GameObject for the separated faces. Otherwise, faces become a new submesh in the same object.";
            newObjectToggle.SetValueWithoutNotify(m_CreateNewObject);
            newObjectToggle.RegisterValueChangedCallback(evt =>
            {
                m_CreateNewObject = evt.newValue;
                UserPreferences.Tools.SeparateFacesCreateNewObject = evt.newValue;
                UpdatePreview();
            });
            root.Add(newObjectToggle);

            // Duplicate checkbox
            var duplicateToggle = new Toggle("Duplicate");
            duplicateToggle.tooltip = "Keep the original faces in place. Otherwise, original faces are removed (detached).";
            duplicateToggle.SetValueWithoutNotify(m_DuplicateFaces);
            duplicateToggle.RegisterValueChangedCallback(evt =>
            {
                m_DuplicateFaces = evt.newValue;
                Overdrive.ProBuilderPlus.UserPreferences.Tools.SeparateFacesDuplicateFaces = evt.newValue;
                UpdatePreview();
            });
            root.Add(duplicateToggle);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            if (!HasSettingsBeenLoaded())
            {
                m_CreateNewObject = Overdrive.ProBuilderPlus.UserPreferences.Tools.SeparateFacesCreateNewObject;
                m_DuplicateFaces = Overdrive.ProBuilderPlus.UserPreferences.Tools.SeparateFacesDuplicateFaces;
            }

            // Cache the current selection for preview
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedSelectedFaces = new Face[selection.Length][];
            m_IndividualFaceCenters = new Vector3[selection.Length][];
            m_PerimeterEdges = new List<(Vector3, Vector3)>[selection.Length];

            for (int i = 0; i < selection.Length; i++)
            {
                var mesh = selection[i];
                var selectedFaces = mesh.GetSelectedFaces();
                m_CachedSelectedFaces[i] = selectedFaces.ToArray();

                // Calculate individual face centers
                m_IndividualFaceCenters[i] = CalculateIndividualFaceCenters(mesh, selectedFaces);

                // Calculate perimeter edges for outline
                m_PerimeterEdges[i] = CalculatePerimeterEdges(mesh, selectedFaces);
            }

            // Subscribe to scene GUI for preview drawing
            SceneView.duringSceneGui += OnSceneGUI;
            UpdatePreview();
        }

        private bool HasSettingsBeenLoaded()
        {
            // Settings are loaded in CreateSettingsContent, so check if it's been called
            // For instant actions without UI, we need to load from preferences
            return false; // Always load from preferences for consistency
        }

        public override void UpdatePreview()
        {
            // Just trigger a repaint - preview data is calculated in StartPreview
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshes == null || m_CachedMeshes.Length == 0)
                return ActionResult.NoSelection;

            // Record undo for the operation
            Undo.RecordObjects(m_CachedMeshes, "Separate Face(s)");

            if (m_CreateNewObject)
            {
                if (m_DuplicateFaces)
                    return DuplicateFacesToObject();
                else
                    return DetachFacesToObject();
            }
            else
            {
                if (m_DuplicateFaces)
                    return DuplicateFacesToSubmesh();
                else
                    return DetachFacesToSubmesh();
            }
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedSelectedFaces = null;
            m_IndividualFaceCenters = null;
            m_PerimeterEdges = null;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_IndividualFaceCenters == null) return;

            // Set z-test to always show visuals on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (int meshIndex = 0; meshIndex < m_CachedMeshes.Length; meshIndex++)
            {
                var mesh = m_CachedMeshes[meshIndex];
                if (mesh == null) continue;

                // Draw green spheres at face centers when creating new object
                if (m_CreateNewObject && m_IndividualFaceCenters[meshIndex] != null)
                {
                    DrawGreenSpheres(m_IndividualFaceCenters[meshIndex]);
                }

                // Draw perimeter outline with color based on duplicate mode
                if (m_PerimeterEdges != null && m_PerimeterEdges.Length > meshIndex && m_PerimeterEdges[meshIndex] != null)
                {
                    DrawPerimeterOutline(m_PerimeterEdges[meshIndex]);
                }
            }
        }

        private void DrawGreenSpheres(Vector3[] faceCenters)
        {
            var originalColor = Handles.color;
            Handles.color = Color.green;

            foreach (var center in faceCenters)
            {
                // Draw a green sphere at each face center
                float handleSize = UnityEditor.HandleUtility.GetHandleSize(center);
                float sphereSize = handleSize * 0.1f;
                Handles.SphereHandleCap(0, center, Quaternion.identity, sphereSize, EventType.Repaint);
            }

            Handles.color = originalColor;
        }

        private void DrawPerimeterOutline(List<(Vector3, Vector3)> edges)
        {
            if (edges == null || edges.Count == 0) return;

            // Choose color based on duplicate mode
            // Red by default, green if either new object or duplicate is enabled
            Color outlineColor = (m_CreateNewObject || m_DuplicateFaces) ? Color.green : Color.red;
            Handles.color = outlineColor;

            // Draw all perimeter edges
            foreach (var edge in edges)
            {
                Handles.DrawAAPolyLine(8f, edge.Item1, edge.Item2);
            }
        }

        private void CalculateFaceCenterAndNormal(ProBuilderMesh mesh, IList<Face> faces, out Vector3 center, out Vector3 normal)
        {
            var positions = mesh.positions;
            center = Vector3.zero;
            normal = Vector3.zero;
            int vertexCount = 0;

            foreach (var face in faces)
            {
                foreach (var index in face.indexes)
                {
                    center += positions[index];
                    vertexCount++;
                }
                normal += Math.Normal(mesh, face);
            }

            if (vertexCount > 0)
                center /= vertexCount;
            
            if (faces.Count > 0)
                normal = (normal / faces.Count).normalized;
        }

                private Vector3[] CalculateIndividualFaceCenters(ProBuilderMesh mesh, IList<Face> faces)
        {
            var centers = new Vector3[faces.Count];
            var positions = mesh.positions;
            
            for (int i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                var faceCenter = Vector3.zero;
                
                foreach (int index in face.indexes)
                {
                    faceCenter += positions[index];
                }
                
                faceCenter /= face.indexes.Count;
                centers[i] = mesh.transform.TransformPoint(faceCenter);
            }
            
            return centers;
        }

        private List<(Vector3, Vector3)> CalculatePerimeterEdges(ProBuilderMesh mesh, IList<Face> faces)
        {
            var edges = new List<(Vector3, Vector3)>();
            var positions = mesh.positions;
            var transform = mesh.transform;
            
            // Use ProBuilder's built-in API to get perimeter edges
            var perimeterEdges = mesh.GetPerimeterEdges(faces);
            
            foreach (var edge in perimeterEdges)
            {
                Vector3 start = transform.TransformPoint(positions[edge.a]);
                Vector3 end = transform.TransformPoint(positions[edge.b]);
                edges.Add((start, end));
            }

            return edges;
        }

        // Separate action implementations based on DetachFaces and DuplicateFaces
        private ActionResult DuplicateFacesToObject()
        {
            int duplicatedFaceCount = 0;
            List<GameObject> duplicated = new List<GameObject>();

            foreach (var mesh in m_CachedMeshes)
            {
                if (mesh.selectedFaceCount < 1) continue;

                var primary = mesh.selectedFaceIndexes;
                duplicatedFaceCount += primary.Count;

                List<int> inverse = new List<int>();
                for (int i = 0; i < mesh.faces.Count; i++)
                    if (!primary.Contains(i))
                        inverse.Add(i);

                ProBuilderMesh copy = Object.Instantiate(mesh.gameObject, mesh.transform.parent).GetComponent<ProBuilderMesh>();
                copy.MakeUnique();
                UnityEditor.ProBuilder.EditorUtility.SynchronizeWithMeshFilter(copy);

                if (copy.transform.childCount > 0)
                {
                    for (int i = copy.transform.childCount - 1; i > -1; i--)
                        Object.DestroyImmediate(copy.transform.GetChild(i).gameObject);

                    foreach (var child in mesh.transform.GetComponentsInChildren<ProBuilderMesh>())
                        UnityEditor.ProBuilder.EditorUtility.SynchronizeWithMeshFilter(child);
                }

                Undo.RegisterCreatedObjectUndo(copy.gameObject, "Duplicate Selection");

                copy.DeleteFaces(inverse);
                copy.ToMesh();
                copy.Refresh();
                copy.Optimize();
                mesh.ClearSelection();
                copy.ClearSelection();
                copy.SetSelectedFaces(copy.faces);

                copy.gameObject.name = GameObjectUtility.GetUniqueNameForSibling(mesh.transform.parent, mesh.gameObject.name);
                duplicated.Add(copy.gameObject);
            }

            Selection.objects = duplicated.ToArray();
            ProBuilderEditor.Refresh();

            if (duplicatedFaceCount > 0)
                return new ActionResult(ActionResult.Status.Success, "Duplicate " + duplicatedFaceCount + " faces to new Object");

            return new ActionResult(ActionResult.Status.Failure, "No Faces Selected");
        }

        private ActionResult DetachFacesToObject()
        {
            int detachedFaceCount = 0;
            List<GameObject> detached = new List<GameObject>();

            foreach (var mesh in m_CachedMeshes)
            {
                if (mesh.selectedFaceCount < 1 || mesh.selectedFaceCount == mesh.faces.Count)
                    continue;

                var primary = mesh.selectedFaceIndexes;
                detachedFaceCount += primary.Count;

                List<int> inverse = new List<int>();
                for (int i = 0; i < mesh.faces.Count; i++)
                    if (!primary.Contains(i))
                        inverse.Add(i);

                ProBuilderMesh copy = Object.Instantiate(mesh.gameObject, mesh.transform.parent).GetComponent<ProBuilderMesh>();
                copy.MakeUnique();
                UnityEditor.ProBuilder.EditorUtility.SynchronizeWithMeshFilter(copy);

                if (copy.transform.childCount > 0)
                {
                    for (int i = copy.transform.childCount - 1; i > -1; i--)
                        Object.DestroyImmediate(copy.transform.GetChild(i).gameObject);

                    foreach (var child in mesh.transform.GetComponentsInChildren<ProBuilderMesh>())
                        UnityEditor.ProBuilder.EditorUtility.SynchronizeWithMeshFilter(child);
                }

                Undo.RecordObject(mesh.GetComponent<Renderer>(), "Update Renderer");

                mesh.DeleteFaces(primary);
                copy.DeleteFaces(inverse);

                mesh.ToMesh();
                mesh.Refresh();
                copy.ToMesh();
                copy.Refresh();

                mesh.Optimize();
                copy.Optimize();

                mesh.ClearSelection();
                copy.ClearSelection();

                copy.SetSelectedFaces(copy.faces);

                Undo.RegisterCreatedObjectUndo(copy.gameObject, "Detach Selection");

                copy.gameObject.name = GameObjectUtility.GetUniqueNameForSibling(mesh.transform.parent, mesh.gameObject.name);
                detached.Add(copy.gameObject);
            }

            Selection.objects = detached.ToArray();
            ProBuilderEditor.Refresh();

            if (detachedFaceCount > 0)
                return new ActionResult(ActionResult.Status.Success, "Detach " + detachedFaceCount + " faces to new Object");

            return new ActionResult(ActionResult.Status.Failure, "No Faces Selected");
        }

        private ActionResult DuplicateFacesToSubmesh()
        {
            int count = 0;

            foreach (var pb in m_CachedMeshes)
            {
                pb.ToMesh();
                List<Face> res = pb.DetachFaces(pb.GetSelectedFaces(), false);
                pb.Refresh();
                pb.Optimize();

                pb.SetSelectedFaces(res.ToArray());
                count += pb.selectedFaceCount;
            }

            ProBuilderEditor.Refresh();

            if (count > 0)
                return new ActionResult(ActionResult.Status.Success, "Duplicate " + count + (count > 1 ? " Faces" : " Face"));

            return new ActionResult(ActionResult.Status.Success, "Duplicate Faces");
        }

        private ActionResult DetachFacesToSubmesh()
        {
            int count = 0;

            foreach (var pb in m_CachedMeshes)
            {
                pb.ToMesh();
                List<Face> res = pb.DetachFaces(pb.GetSelectedFaces());
                pb.Refresh();
                pb.Optimize();

                pb.SetSelectedFaces(res.ToArray());
                count += pb.selectedFaceCount;
            }

            ProBuilderEditor.Refresh();

            if (count > 0)
                return new ActionResult(ActionResult.Status.Success, "Detach " + count + (count > 1 ? " Faces" : " Face"));

            return new ActionResult(ActionResult.Status.Success, "Detach Faces");
        }
    }
}
