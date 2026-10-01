using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

// TODO: Preview doesn't work correctly with multiple edges selected (although the action itself does)

namespace Overdrive.ProBuilderPlus
{
    [ProBuilderPlusAction("insert_edge_loop_preview", "Add Loop",
        Tooltip = "Insert edge loops with live preview and position control",
        Instructions = "Insert edge loop at specified position (cyan loop, white ring)",
        IconPath = "Icons/Old/Edge_InsertLoop",
        ValidModes = ToolMode.Edge,
        EdgesSelectedRequired = 1,
        Order = 150)]
    public sealed class InsertEdgeLoopPreviewAction : PreviewMenuAction
    {
        // Settings
        private float m_LoopPosition;
        private ConnectionDirection m_Direction;
        private ConnectionMode m_Mode;

        // Cached data for applying changes
        private ProBuilderMesh[] m_CachedMeshes;
        private Edge[][] m_CachedEdgeRings;
        private Edge[][] m_CachedExpandedEdgeRings;
        private List<Vector3>[] m_PreviewLoopPoints;
        private float m_ShortestEdgeLength = 1f;

        public enum ConnectionDirection { FromLeft, FromRight }
        public enum ConnectionMode { Percent, Absolute }


        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_LoopPosition = UserPreferences.Tools.LoopPosition;
            m_Direction = (ConnectionDirection)UserPreferences.Tools.LoopDirection;
            m_Mode = (ConnectionMode)UserPreferences.Tools.LoopMode;

            // Instructions are now handled by the framework via the Instructions attribute

            // Loop Position - Container for slider and field
            //var positionContainer = new VisualElement();
            //positionContainer.style.flexDirection = FlexDirection.Row;
            //positionContainer.AddToClassList("stack-label-above");

            // Declare field first so it can be referenced in slider callback
            var positionField = new FloatField("Offset");

            // Configure the field
            positionField.tooltip = "Exact position value";
            positionField.SetValueWithoutNotify(m_LoopPosition);
            positionField.RegisterValueChangedCallback(evt =>
            {
                float newValue = evt.newValue;

                // In Percent mode, clamp to 0-1. In Absolute mode, clamp to shortest edge length
                if (m_Mode == ConnectionMode.Percent)
                {
                    newValue = Mathf.Clamp01(newValue);
                }
                else // Absolute mode
                {
                    newValue = Mathf.Clamp(newValue, 0f, m_ShortestEdgeLength);
                }

                if (m_LoopPosition != newValue)
                {
                    m_LoopPosition = newValue;
                    UserPreferences.Tools.LoopPosition = newValue;
                    //positionSlider.SetValueWithoutNotify(newValue);
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });

            // Row for enums
            //var enumContainer = new VisualElement();
            //enumContainer.style.flexDirection = FlexDirection.Row;
            //enumContainer.style.justifyContent = Justify.Center;

            // Direction dropdown
            var directionField = new EnumField("Direction", m_Direction);
            directionField.tooltip = "Whether to measure position from left or right side of the edge";
            directionField.RegisterValueChangedCallback(evt =>
            {
                if (m_Direction != (ConnectionDirection)evt.newValue)
                {
                    m_Direction = (ConnectionDirection)evt.newValue;
                    UserPreferences.Tools.LoopDirection = (int)(ConnectionDirection)evt.newValue;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            //enumContainer.Add(directionField);

            // Mode dropdown
            var modeField = new EnumField("Method", m_Mode);
            modeField.tooltip = "Whether position is a percentage (0-1) or absolute distance in world units";
            modeField.RegisterValueChangedCallback(evt =>
            {
                if (m_Mode != (ConnectionMode)evt.newValue)
                {
                    m_Mode = (ConnectionMode)evt.newValue;
                    UserPreferences.Tools.LoopMode = (int)(ConnectionMode)evt.newValue;

                    // Update shortest edge length when switching to Absolute mode
                    UpdateShortestEdgeLength();

                    // When switching modes, update the field value display and validation
                    if (m_Mode == ConnectionMode.Percent)
                    {
                        // In Percent mode, ensure value is 0-1 and sync with slider
                        //positionSlider.highValue = 1f;
                        m_LoopPosition = Mathf.Clamp01(m_LoopPosition);
                        //positionSlider.SetValueWithoutNotify(m_LoopPosition);
                        positionField.SetValueWithoutNotify(m_LoopPosition);
                    }
                    else
                    {
                        // In Absolute mode, set slider range to shortest edge length
                        //positionSlider.highValue = m_ShortestEdgeLength;
                        m_LoopPosition = Mathf.Clamp(m_LoopPosition, 0f, m_ShortestEdgeLength);
                        //positionSlider.SetValueWithoutNotify(m_LoopPosition);
                        positionField.SetValueWithoutNotify(m_LoopPosition);
                    }

                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });
            //enumContainer.Add(modeField);

            // root.Add(positionSlider);
            root.Add(positionField);
            root.Add(directionField);
            root.Add(modeField);
            //root.Add(enumContainer);
            //root.Add(positionContainer);
            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            if (!HasSettingsBeenLoaded())
            {
                m_LoopPosition = UserPreferences.Tools.LoopPosition;
                m_Direction = (ConnectionDirection)UserPreferences.Tools.LoopDirection;
                m_Mode = (ConnectionMode)UserPreferences.Tools.LoopMode;
            }

            CacheCurrentSelection();
            UpdatePreview();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private bool HasSettingsBeenLoaded()
        {
            // Settings are loaded in CreateSettingsContent, so check if it's been called
            // For instant actions without UI, we need to load from preferences
            return false; // Always load from preferences for consistency
        }

        private void CacheCurrentSelection()
        {
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedEdgeRings = new Edge[selection.Length][];
            m_CachedExpandedEdgeRings = new Edge[selection.Length][];

            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] != null)
                {
                    var selectedEdges = selection[i].selectedEdges;
                    m_CachedEdgeRings[i] = selectedEdges.ToArray();

                    // Cache the expanded edge ring for preview display
                    m_CachedExpandedEdgeRings[i] = EdgeOperationHelper.GetEdgeRing(selection[i], selectedEdges.ToArray());
                }
                else
                {
                    m_CachedEdgeRings[i] = new Edge[0];
                    m_CachedExpandedEdgeRings[i] = new Edge[0];
                }
            }

            UpdateShortestEdgeLength();
        }

        private void UpdateShortestEdgeLength()
        {
            m_ShortestEdgeLength = float.MaxValue;

            if (m_CachedMeshes == null || m_CachedExpandedEdgeRings == null)
                return;

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var ring = m_CachedExpandedEdgeRings[i];

                if (mesh == null || ring == null || ring.Length == 0)
                    continue;

                var vertices = mesh.GetVertices();

                foreach (var edge in ring)
                {
                    if (edge.a < vertices.Length && edge.b < vertices.Length)
                    {
                        var pointA = vertices[edge.a].position;
                        var pointB = vertices[edge.b].position;
                        var edgeLength = Vector3.Distance(pointA, pointB);

                        if (edgeLength < m_ShortestEdgeLength)
                        {
                            m_ShortestEdgeLength = edgeLength;
                        }
                    }
                }
            }

            // Ensure we have a reasonable minimum
            if (m_ShortestEdgeLength == float.MaxValue || m_ShortestEdgeLength <= 0f)
            {
                m_ShortestEdgeLength = 1f;
            }
        }

        internal override void OnSelectionChangedDuringPreview()
        {
            CacheCurrentSelection();
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            try
            {
                CalculatePreviewLoopPoints();
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error updating insert edge loop preview: {ex.Message}");
            }
        }

        private void CalculatePreviewLoopPoints()
        {
            if (m_CachedMeshes == null) return;

            m_PreviewLoopPoints = new List<Vector3>[m_CachedMeshes.Length];

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var ring = m_CachedExpandedEdgeRings[i];

                if (mesh == null || ring == null || ring.Length == 0)
                {
                    m_PreviewLoopPoints[i] = new List<Vector3>();
                    continue;
                }

                var vertices = mesh.GetVertices();
                var loopPoints = new List<Vector3>();

                foreach (var edge in ring)
                {
                    if (edge.a < vertices.Length && edge.b < vertices.Length)
                    {
                        var pointA = vertices[edge.a].position;
                        var pointB = vertices[edge.b].position;

                        float t = CalculateInterpolationValue(pointA, pointB);
                        var loopPoint = Vector3.Lerp(pointA, pointB, t);
                        loopPoints.Add(loopPoint);
                    }
                }

                m_PreviewLoopPoints[i] = loopPoints;
            }
        }

        private float CalculateInterpolationValue(Vector3 pointA, Vector3 pointB)
        {
            // Determine dominant axis and ensure consistent direction in object space
            var edgeVector = pointB - pointA;
            bool shouldFlip = false;

            // Find the dominant axis (largest absolute component)
            if (Mathf.Abs(edgeVector.x) > Mathf.Abs(edgeVector.y) && Mathf.Abs(edgeVector.x) > Mathf.Abs(edgeVector.z))
            {
                // X is dominant - flip if going in negative X direction
                shouldFlip = edgeVector.x < 0;
            }
            else if (Mathf.Abs(edgeVector.y) > Mathf.Abs(edgeVector.z))
            {
                // Y is dominant - flip if going in negative Y direction
                shouldFlip = edgeVector.y < 0;
            }
            else
            {
                // Z is dominant - flip if going in negative Z direction
                shouldFlip = edgeVector.z < 0;
            }

            float t = m_LoopPosition;

            if (m_Mode == ConnectionMode.Absolute)
            {
                float edgeLength = Vector3.Distance(pointA, pointB);
                if (edgeLength > 0)
                {
                    t = Mathf.Clamp01(m_LoopPosition / edgeLength);
                }
            }

            // Apply user direction preference
            if (m_Direction == ConnectionDirection.FromRight)
            {
                t = 1f - t;
            }

            // Apply consistent object space direction
            if (shouldFlip)
            {
                t = 1f - t;
            }

            return t;
        }

        public override ActionResult ApplyChanges()
        {
            if (m_CachedMeshes == null)
            {
                return new ActionResult(ActionResult.Status.Failure, "No cached edge ring data available");
            }

            Undo.RecordObjects(m_CachedMeshes, "Insert Edge Loop");

            int successCount = 0;

            var allNewEdges = new List<Edge>();
            var meshesWithNewEdges = new List<ProBuilderMesh>();

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var ring = m_CachedExpandedEdgeRings[i];
                var loopPoints = m_PreviewLoopPoints?[i];

                if (mesh == null || ring == null || ring.Length == 0 || loopPoints == null || loopPoints.Count == 0)
                    continue;

                try
                {
                    // Insert edge loop at the calculated positions and get new edges (ring is already expanded)
                    var newEdges = EdgeOperationHelper.ConnectEdgesAtCalculatedPositions(mesh, ring, loopPoints);

                    if (newEdges != null && newEdges.Count > 0)
                    {
                        mesh.ToMesh();
                        mesh.Refresh();
                        mesh.Optimize();
                        successCount++;

                        // Collect new edges for selection
                        allNewEdges.AddRange(newEdges);
                        meshesWithNewEdges.Add(mesh);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to insert edge loop on mesh {i}: {ex.Message}");
                }
            }

            // Select the new edges
            if (allNewEdges.Count > 0 && meshesWithNewEdges.Count > 0)
            {
                // Clear current element selection but keep object selection
                MeshSelection.ClearElementSelection();

                // Select the new edges
                foreach (var mesh in meshesWithNewEdges)
                {
                    var meshSpecificEdges = allNewEdges.Where(e => IsEdgeInMesh(mesh, e)).ToArray();
                    if (meshSpecificEdges.Length > 0)
                    {
                        mesh.SetSelectedEdges(meshSpecificEdges);
                    }
                }
            }

            ProBuilderEditor.Refresh();

            if (successCount > 0)
                return new ActionResult(ActionResult.Status.Success, $"Inserted edge loops on {successCount} mesh(es)");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to insert edge loops");
        }


        private bool IsEdgeInMesh(ProBuilderMesh mesh, Edge edge)
        {
            var vertices = mesh.GetVertices();
            return edge.a < vertices.Length && edge.b < vertices.Length;
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedEdgeRings = null;
            m_CachedExpandedEdgeRings = null;
            m_PreviewLoopPoints = null;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_CachedExpandedEdgeRings == null) return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var expandedRing = m_CachedExpandedEdgeRings[i];
                var loopPoints = m_PreviewLoopPoints?[i];

                if (mesh == null || expandedRing == null || expandedRing.Length == 0) continue;

                var vertices = mesh.GetVertices();

                // Draw complete edge ring in white (shows all edges that will be affected)
                Handles.color = Color.white;
                foreach (var edge in expandedRing)
                {
                    if (edge.a < vertices.Length && edge.b < vertices.Length)
                    {
                        var startPos = mesh.transform.TransformPoint(vertices[edge.a].position);
                        var endPos = mesh.transform.TransformPoint(vertices[edge.b].position);
                        Handles.DrawAAPolyLine(8f, startPos, endPos);
                    }
                }

                // Draw all resulting connected edges in cyan (shows the complete edge loop that will be created)
                if (loopPoints != null && loopPoints.Count > 1)
                {
                    Handles.color = Color.cyan;

                    // Draw connections between consecutive loop points to show the complete edge loop
                    for (int j = 0; j < loopPoints.Count; j++)
                    {
                        var currentPoint = mesh.transform.TransformPoint(loopPoints[j]);
                        var nextPoint = mesh.transform.TransformPoint(loopPoints[(j + 1) % loopPoints.Count]);
                        Handles.DrawAAPolyLine(3f, currentPoint, nextPoint);
                    }
                }
            }
        }
    }
}