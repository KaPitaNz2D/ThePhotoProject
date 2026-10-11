using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// ProBuilderize action for converting meshes to ProBuilder meshes with preview functionality.
    /// Allows selective conversion with import settings preview.
    /// </summary>
    [ProBuilderPlusAction("probuilderize", "ProBuilderize",
        Tooltip = "Convert selected meshes to ProBuilder objects with import settings preview",
        Instructions = "Configure import settings and click Apply to convert meshes to ProBuilder objects.",
        IconPath = "Icons/Old/Object_ProBuilderize",
        ValidModes = ToolMode.Object,
        Order = 500)]
    public sealed class ProBuilderizePreviewAction : PreviewMenuAction
    {
        // Import settings
        private bool m_ImportQuads = true;
        private bool m_ImportSmoothing = true;
        private float m_SmoothingAngle = 1f;

        // Cached data
        private List<MeshFilter> m_CachedMeshFilters;
        private List<GameObject> m_OriginalObjects;

        protected override bool IsSpecialConditionsMet()
        {
            return ProBuilderFunctions.IsBasicMeshSelected();
        }

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Import Quads toggle
            var quadsToggle = new Toggle("Import Quads") { value = m_ImportQuads };
            quadsToggle.tooltip = "Create ProBuilder mesh using quads where possible instead of triangles.";
            quadsToggle.RegisterValueChangedCallback(evt =>
            {
                m_ImportQuads = evt.newValue;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(quadsToggle);

            // Import Smoothing toggle
            var smoothingToggle = new Toggle("Import Smoothing") { value = m_ImportSmoothing };
            smoothingToggle.tooltip = "Import smoothing groups by testing adjacent faces against an angle threshold.";
            smoothingToggle.RegisterValueChangedCallback(evt =>
            {
                m_ImportSmoothing = evt.newValue;
                PreviewActionFramework.RequestPreviewUpdate();
            });
            root.Add(smoothingToggle);

            // Smoothing threshold
            var smoothingField = new FloatField("Threshold") { value = m_SmoothingAngle };
            smoothingField.SetEnabled(m_ImportSmoothing);
            smoothingField.RegisterValueChangedCallback(evt =>
            {
                m_SmoothingAngle = Mathf.Clamp(evt.newValue, 0.0001f, 45f);
                UserPreferences.Tools.SmoothingAngle = m_SmoothingAngle;
                PreviewActionFramework.RequestPreviewUpdate();
            });

            // Update slider/field enabled state when smoothing toggle changes
            smoothingToggle.RegisterValueChangedCallback(evt =>
            {
                smoothingField.SetEnabled(evt.newValue);
            });

            root.Add(smoothingField);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            m_ImportQuads = UserPreferences.Tools.ImportQuads;
            m_ImportSmoothing = UserPreferences.Tools.ImportSmoothing;
            m_SmoothingAngle = UserPreferences.Tools.SmoothingAngle;

            // Cache the meshes we'll be converting
            m_CachedMeshFilters = new List<MeshFilter>();
            m_OriginalObjects = new List<GameObject>();

            var selectedTransforms = Selection.transforms;

            // Get all mesh filters from selected objects and their children
            foreach (var transform in selectedTransforms)
            {
                var meshFilters = transform.GetComponentsInChildren<MeshFilter>()
                    .Where(mf => mf != null && mf.sharedMesh != null && mf.GetComponent<ProBuilderMesh>() == null);

                foreach (var mf in meshFilters)
                {
                    m_CachedMeshFilters.Add(mf);
                    m_OriginalObjects.Add(mf.gameObject);
                }
            }
        }

        public override void UpdatePreview()
        {
            // For ProBuilderize, the preview is mainly about showing the settings
            // The actual conversion happens on apply
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshFilters == null || m_CachedMeshFilters.Count == 0)
            {
                return new ActionResult(ActionResult.Status.Canceled, "No meshes to convert");
            }

            // Check for static batched renderers (not supported)
            var staticBatchedObjects = m_CachedMeshFilters
                .Where(mf => mf.GetComponent<MeshRenderer>()?.isPartOfStaticBatch == true)
                .Select(mf => mf.gameObject.name)
                .ToList();

            if (staticBatchedObjects.Any())
            {
                string objectNames = string.Join(", ", staticBatchedObjects.Take(3));
                if (staticBatchedObjects.Count > 3)
                    objectNames += $" and {staticBatchedObjects.Count - 3} more";

                Debug.LogError($"ProBuilderize is not supported for static batched renderers: {objectNames}");
                return new ActionResult(ActionResult.Status.Failure,
                    $"Cannot convert static batched objects: {objectNames}");
            }

            // Create import settings
            var settings = new MeshImportSettings()
            {
                quads = m_ImportQuads,
                smoothing = m_ImportSmoothing,
                smoothingAngle = m_SmoothingAngle
            };

            // Prepare undo
            var undoObjects = m_OriginalObjects.Cast<Object>().ToList();
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), "ProBuilderize");

            int successCount = 0;
            var errors = new List<string>();

            // Convert each mesh filter
            foreach (var meshFilter in m_CachedMeshFilters)
            {
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                var gameObject = meshFilter.gameObject;
                var renderer = gameObject.GetComponent<MeshRenderer>();

                try
                {
                    // Get source mesh and materials
                    var sourceMesh = meshFilter.sharedMesh;
                    var sourceMaterials = renderer?.sharedMaterials;

                    // Add ProBuilderMesh component
                    var destination = Undo.AddComponent<ProBuilderMesh>(gameObject);

                    // Import the mesh
                    var meshImporter = new MeshImporter(sourceMesh, sourceMaterials, destination);
                    meshImporter.Import(settings);

                    // Update the mesh
                    destination.ToMesh();
                    destination.Refresh();
                    destination.Optimize();

                    successCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to ProBuilderize '{gameObject.name}': {ex.Message}");
                    errors.Add(gameObject.name);
                }
            }

            // Refresh ProBuilder and selection
            ProBuilderEditor.Refresh(true);
            ProBuilderPlusCore.UpdateStatus(); //// Required because Unity events do not 'get' the change of the objects internals
            SceneView.RepaintAll();

            // Return result
            if (successCount > 0)
            {
                string message = successCount == 1 ?
                    "ProBuilderized 1 object" :
                    $"ProBuilderized {successCount} objects";

                if (errors.Any())
                {
                    message += $" ({errors.Count} failed)";
                }

                return new ActionResult(ActionResult.Status.Success, message);
            }
            else
            {
                return new ActionResult(ActionResult.Status.Failure, "Failed to ProBuilderize any objects");
            }
        }

        public override void CleanupPreview()
        {
            m_CachedMeshFilters?.Clear();
            m_OriginalObjects?.Clear();
            m_CachedMeshFilters = null;
            m_OriginalObjects = null;
        }
    }
}