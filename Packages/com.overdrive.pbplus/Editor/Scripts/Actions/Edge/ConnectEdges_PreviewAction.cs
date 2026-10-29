using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

// TODO: (BUG) don't allow when selected edges don't share a face

namespace Overdrive.ProBuilderPlus
{
    [ProBuilderPlusAction("connect_edges_preview", "Connect",
        Tooltip = "Connect selected edges with live preview and position control",
        Instructions = "Connect edges at specified position (cyan lines show new edges)",
        IconPath = "Icons/Old/Edge_Connect",
        ValidModes = ToolMode.Edge,
        EdgesSelectedRequired = 1,
        Order = 140)]
    public sealed class ConnectEdgesPreviewAction : PreviewMenuAction
    {
        // Settings
        private float m_ConnectionPosition;
        private ConnectionDirection m_Direction;
        private ConnectionMode m_Mode;

        // Cached data for applying changes
        private ProBuilderMesh[] m_CachedMeshes;
        private Edge[][] m_CachedEdges;
        private List<Vector3>[] m_PreviewConnectionPoints;
        private float m_ShortestEdgeLength = 1f;

        public enum ConnectionDirection { FromLeft, FromRight }

        public enum ConnectionMode { Percent, Absolute }

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_ConnectionPosition = UserPreferences.Tools.ConnectionPosition;
            m_Direction = (ConnectionDirection)UserPreferences.Tools.ConnectionDirection;
            m_Mode = (ConnectionMode)UserPreferences.Tools.ConnectionMode;

            // Offset field
            var positionField = new FloatField("Offset");
            positionField.tooltip = "Exact position value";
            positionField.SetValueWithoutNotify(m_ConnectionPosition);
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

                if (m_ConnectionPosition != newValue)
                {
                    m_ConnectionPosition = newValue;
                    UserPreferences.Tools.ConnectionPosition = newValue;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });

            // Direction dropdown
            var directionField = new EnumField("Direction", m_Direction);
            directionField.tooltip = "Whether to measure position from left or right side of the edge";
            directionField.RegisterValueChangedCallback(evt =>
            {
                if (m_Direction != (ConnectionDirection)evt.newValue)
                {
                    m_Direction = (ConnectionDirection)evt.newValue;
                    UserPreferences.Tools.ConnectionDirection = (int)(ConnectionDirection)evt.newValue;
                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });

            // Mode dropdown
            var modeField = new EnumField("Method", m_Mode);
            modeField.tooltip = "Whether position is a percentage (0-1) or absolute distance in world units";
            modeField.RegisterValueChangedCallback(evt =>
            {
                if (m_Mode != (ConnectionMode)evt.newValue)
                {
                    m_Mode = (ConnectionMode)evt.newValue;
                    UserPreferences.Tools.ConnectionMode = (int)(ConnectionMode)evt.newValue;

                    // Update shortest edge length when switching to Absolute mode
                    UpdateShortestEdgeLength();

                    // When switching modes, update the field value display and validation
                    if (m_Mode == ConnectionMode.Percent)
                    {
                        // In Percent mode, ensure value is 0-1 and sync with slider
                        m_ConnectionPosition = Mathf.Clamp01(m_ConnectionPosition);
                        positionField.SetValueWithoutNotify(m_ConnectionPosition);
                    }
                    else
                    {
                        // In Absolute mode, set slider range to shortest edge length
                        m_ConnectionPosition = Mathf.Clamp(m_ConnectionPosition, 0f, m_ShortestEdgeLength);
                        positionField.SetValueWithoutNotify(m_ConnectionPosition);
                    }

                    PreviewActionFramework.RequestPreviewUpdate();
                }
            });

            root.Add(positionField);
            root.Add(directionField);
            root.Add(modeField);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            if (!HasSettingsBeenLoaded())
            {
                m_ConnectionPosition = UserPreferences.Tools.ConnectionPosition;
                m_Direction = (ConnectionDirection)UserPreferences.Tools.ConnectionDirection;
                m_Mode = (ConnectionMode)UserPreferences.Tools.ConnectionMode;
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
            m_CachedEdges = new Edge[selection.Length][];

            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i] != null)
                {
                    var selectedEdges = selection[i].selectedEdges;
                    m_CachedEdges[i] = selectedEdges.ToArray();
                }
                else
                {
                    m_CachedEdges[i] = new Edge[0];
                }
            }

            UpdateShortestEdgeLength();
        }

        private void UpdateShortestEdgeLength()
        {
            m_ShortestEdgeLength = float.MaxValue;

            if (m_CachedMeshes == null || m_CachedEdges == null)
                return;

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var edges = m_CachedEdges[i];

                if (mesh == null || edges == null || edges.Length == 0)
                    continue;

                var vertices = mesh.GetVertices();

                foreach (var edge in edges)
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
                CalculatePreviewConnectionPoints();
                SceneView.RepaintAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error updating connect edges preview: {ex.Message}");
            }
        }

        private void CalculatePreviewConnectionPoints()
        {
            if (m_CachedMeshes == null) return;

            m_PreviewConnectionPoints = new List<Vector3>[m_CachedMeshes.Length];

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var edges = m_CachedEdges[i];

                if (mesh == null || edges == null || edges.Length == 0)
                {
                    m_PreviewConnectionPoints[i] = new List<Vector3>();
                    continue;
                }

                var vertices = mesh.GetVertices();
                var connectionPoints = new List<Vector3>();

                foreach (var edge in edges)
                {
                    if (edge.a < vertices.Length && edge.b < vertices.Length)
                    {
                        var pointA = vertices[edge.a].position;
                        var pointB = vertices[edge.b].position;

                        float t = CalculateInterpolationValue(pointA, pointB);
                        var connectionPoint = Vector3.Lerp(pointA, pointB, t);
                        connectionPoints.Add(connectionPoint);
                    }
                }

                m_PreviewConnectionPoints[i] = connectionPoints;
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

            float t = m_ConnectionPosition;

            if (m_Mode == ConnectionMode.Absolute)
            {
                float edgeLength = Vector3.Distance(pointA, pointB);
                if (edgeLength > 0)
                {
                    t = Mathf.Clamp01(m_ConnectionPosition / edgeLength);
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

            Undo.RecordObjects(m_CachedMeshes, "Connect Edges");

            int successCount = 0;

            var allNewEdges = new List<Edge>();
            var meshesWithNewEdges = new List<ProBuilderMesh>();

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var edges = m_CachedEdges[i];
                var connectionPoints = m_PreviewConnectionPoints?[i];

                if (mesh == null || edges == null || edges.Length == 0 || connectionPoints == null || connectionPoints.Count == 0)
                    continue;

                try
                {
                    // Connect the edges at the calculated positions and get new edges
                    var newEdges = EdgeOperationHelper.ConnectEdgesAtCalculatedPositions(mesh, edges, connectionPoints);

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
                    Debug.LogError($"Failed to connect edges on mesh {i}: {ex.Message}");
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
                return new ActionResult(ActionResult.Status.Success, $"Connected edges on {successCount} mesh(es)");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to connect edges");
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
            m_CachedEdges = null;
            m_PreviewConnectionPoints = null;
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_CachedMeshes == null || m_CachedEdges == null) return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                var edges = m_CachedEdges[i];
                var connectionPoints = m_PreviewConnectionPoints?[i];

                if (mesh == null || edges == null || edges.Length == 0) continue;

                var vertices = mesh.GetVertices();

                // Draw preview connections in cyan
                if (connectionPoints != null && connectionPoints.Count > 1)
                {
                    Handles.color = Color.cyan;

                    for (int j = 0; j < connectionPoints.Count; j++)
                    {
                        var currentPoint = mesh.transform.TransformPoint(connectionPoints[j]);
                        var nextPoint = mesh.transform.TransformPoint(connectionPoints[(j + 1) % connectionPoints.Count]);
                        Handles.DrawAAPolyLine(3f, currentPoint, nextPoint);
                    }
                }
            }
        }
    }
}
