using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

// TODO: only draw the cyan lines where from actual point to point

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Advanced inset preview action that leverages InsetFaces_MathHelper for precise calculations.
    /// Shows comprehensive debug visualization including vertex types, edge types, and calculated inset positions.
    /// Uses the preview framework for consistent UI and behavior.
    /// </summary>
    [ProBuilderPlusAction("inset_faces_advanced", "Inset",
        Tooltip = "Advanced inset operation with comprehensive debug visualization",
        Instructions = "Inset selected faces (color-coded debug visualization)",
        IconPath = "Icons/Old/Offset_Elements",
        ValidModes = ToolMode.Face | ToolMode.UvFace,
        FacesSelectedRequired = 1,
        Order = 110)]
    public sealed class InsetFacesPreviewAction : PreviewMenuAction
    {
        // Settings
        private float m_InsetDistance;

        // Cached data for applying changes
        private List<Vector3?> m_CachedInsetPoints;
        private List<int> m_CachedVertexIndices; // Vertex indices corresponding to m_CachedInsetPoints
        private ProBuilderMesh[] m_CachedMeshes;
        private Face[][] m_CachedFaces;

     

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_InsetDistance = UserPreferences.Tools.InsetFacesDistance;

            // Distance field
            var distanceField = new FloatField("Distance");
            distanceField.tooltip = "The distance to inset faces in meters. Must be positive.";
            distanceField.SetValueWithoutNotify(m_InsetDistance);
            distanceField.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                float distance = Mathf.Clamp(evt.newValue, 0.01f, 100f);
                if (distance != evt.newValue)
                {
                    distanceField.SetValueWithoutNotify(distance);
                }
                if (m_InsetDistance != distance)
                {
                    m_InsetDistance = distance;
                    Overdrive.ProBuilderPlus.UserPreferences.Tools.InsetFacesDistance = distance;
                    // Request preview update through framework
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            root.Add(distanceField);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            if (!HasSettingsBeenLoaded())
            {
                m_InsetDistance = Overdrive.ProBuilderPlus.UserPreferences.Tools.InsetFacesDistance;
            }

            // Cache the current selection for later application
            CacheCurrentSelection();

            // Enable debug visualization in InsetFaces_MathHelper
            InsetFaces_MathHelper.SetDebugVisualizationEnabled(true);

            // Update InsetFaces_MathHelper with our current settings
            UpdatePreview();
        }

        private bool HasSettingsBeenLoaded()
        {
            // Settings are loaded in CreateSettingsContent, so check if it's been called
            // For instant actions without UI, we need to load from preferences
            return false; // Always load from preferences for consistency
        }

        /// <summary>
        /// Caches the current selection state for later application.
        /// </summary>
        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedFaces = new Face[selection.Length][];

            for (int i = 0; i < selection.Length; i++)
            {
                m_CachedFaces[i] = selection[i].GetSelectedFaces();
            }
        }

        /// <summary>
        /// Override to handle selection changes during preview by refreshing cache and calculations.
        /// </summary>
        internal override void OnSelectionChangedDuringPreview()
        {
            // Update our cached selection to match the new selection
            CacheCurrentSelection();

            // Update the preview calculations for the new selection
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            try
            {
                // Update the inset amount in InsetFaces_MathHelper and get calculated points
                m_CachedInsetPoints = InsetFaces_MathHelper.UpdateInsetAmountAndRecalculate(m_InsetDistance);

                // Cache the vertex indices that correspond to the inset points
                m_CachedVertexIndices = InsetFaces_MathHelper.GetOrderedVertexIndices();

                // Log preview update information for debugging
                int validPoints = m_CachedInsetPoints?.Count(p => p.HasValue) ?? 0;
                int totalPoints = m_CachedInsetPoints?.Count ?? 0;

                // Force scene view repaint to show updated debug visualization
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error updating inset preview: {ex.Message}");
                m_CachedInsetPoints = null;
                m_CachedVertexIndices = null;
            }
        }

        public override ActionResult ApplyChanges()
        {
            // Validate that we have cached data
            if (m_CachedMeshes == null || m_CachedFaces == null || m_CachedInsetPoints == null || m_CachedVertexIndices == null)
            {
                return new ActionResult(ActionResult.Status.Failure, "No cached inset data available");
            }

            // Record undo for the mesh modifications
            Undo.RecordObjects(m_CachedMeshes, "Advanced Inset Faces");

            int totalInsetsCreated = 0;

            foreach (ProBuilderMesh mesh in m_CachedMeshes)
            {
                var meshIndex = System.Array.IndexOf(m_CachedMeshes, mesh);
                var faces = m_CachedFaces[meshIndex];

                if (faces == null || faces.Length == 0) continue;

                // Apply the inset using the calculated points from InsetFaces_MathHelper
                if (ApplyInsetToMesh(mesh, faces))
                {
                    totalInsetsCreated++;

                    // Finalize the mesh
                    mesh.ToMesh();
                    mesh.Refresh();
                }
            }

            ProBuilderEditor.Refresh();

            if (totalInsetsCreated > 0)
                return new ActionResult(ActionResult.Status.Success, $"Advanced Inset Applied to {totalInsetsCreated} mesh(es)");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to apply inset to any meshes");
        }

        public override void CleanupPreview()
        {
            // Disable debug visualization in InsetFaces_MathHelper
            InsetFaces_MathHelper.SetDebugVisualizationEnabled(false);

            // Clear cached data
            m_CachedInsetPoints = null;
            m_CachedVertexIndices = null;
            m_CachedMeshes = null;
            m_CachedFaces = null;

            // Repaint scene view to hide debug visualization
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Applies inset to a mesh using the calculated inset points from InsetFaces_MathHelper.
        /// Uses the direct mapping approach for more precise application.
        /// </summary>
        private bool ApplyInsetToMesh(ProBuilderMesh mesh, Face[] faces)
        {
            if (m_CachedInsetPoints == null || m_CachedVertexIndices == null || faces == null || faces.Length == 0)
                return false;

            try
            {
                // Convert our cached data to the format expected by InsetFaces_MeshHelper.ApplyInset
                var preCalculatedPositions = ConvertCachedDataToVertexMapping(mesh, faces);

                if (preCalculatedPositions == null || preCalculatedPositions.Count == 0)
                {
                    Debug.LogWarning("No valid inset positions found to apply");
                    return false;
                }

                // Use the proven utility method with the correctly formatted data
                return InsetFaces_MeshHelper.ApplyInset(mesh, faces, preCalculatedPositions);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to apply inset to mesh: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts the cached inset points and vertex indices into the vertex index mapping
        /// format expected by InsetFaces_MeshHelper.ApplyInset.
        /// </summary>
        private Dictionary<int, Vector3> ConvertCachedDataToVertexMapping(ProBuilderMesh mesh, Face[] faces)
        {
            var result = new Dictionary<int, Vector3>();

            if (m_CachedInsetPoints == null || m_CachedVertexIndices == null)
                return result;

            // Validate that our cached data is consistent
            if (m_CachedInsetPoints.Count != m_CachedVertexIndices.Count)
            {
                Debug.LogWarning($"Cached data mismatch: {m_CachedInsetPoints.Count} inset points vs {m_CachedVertexIndices.Count} vertex indices");
                return result;
            }

            // Get all vertices that are part of the selected faces for validation
            var selectedVertexIndices = new HashSet<int>();
            foreach (var face in faces)
            {
                foreach (var vertexIndex in face.distinctIndexes)
                {
                    selectedVertexIndices.Add(vertexIndex);
                }
            }

            // Map cached inset points to vertex indices
            for (int i = 0; i < m_CachedInsetPoints.Count; i++)
            {
                var insetPoint = m_CachedInsetPoints[i];
                var vertexIndex = m_CachedVertexIndices[i];

                // Only process vertices that have valid inset positions and are part of selected faces
                if (insetPoint.HasValue && selectedVertexIndices.Contains(vertexIndex))
                {
                    result[vertexIndex] = insetPoint.Value;
                }
            }

            return result;
        }


    }
}
