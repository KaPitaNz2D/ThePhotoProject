using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

// TODO: Better visuals

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Preview action that detects and highlights holes in the mesh.
    /// Shows all holes in orange, and selected holes in cyan.
    /// </summary>
    [ProBuilderPlusAction("fill_hole_preview", "Poly Fill",
        Tooltip = "Preview and fill holes. Orange lines show all holes, colored lines show selected holes. Click vertices or edges to select holes, then Apply to fill them.",
        Instructions = "Select open edges or vertices to form the new polygon face",
        IconPath = "Icons/Old/FillHole",
        ValidModes = ToolMode.Edge | ToolMode.Vertex,
        Order = 120)]
    public sealed class FillHolePreviewAction : PreviewMenuAction
    {
        // Cached selection for consistent preview and application
        private ProBuilderMesh[] m_CachedMeshes;
        private Edge[][] m_CachedSelectedEdges;
        private int[][] m_CachedSelectedVertices;

        // Preview state calculated from cached selection
        private Dictionary<ProBuilderMesh, List<List<Edge>>> m_AllHoles;
        private Dictionary<ProBuilderMesh, List<List<Edge>>> m_SelectedHoles;

        // Fill hole settings
        private bool m_FillEntirePath = true;

        protected override bool IsSpecialConditionsMet()
        {
            // Must have at least one selected mesh. 
            // Attribute ObjectsSelectedRequired doe snot apply because it's not in object mode.
            if (MeshSelection.selectedObjectCount == 0)
                return false;

            // Get the current selection mode
            var currentSelectMode = ProBuilderEditor.selectMode;
            var selection = MeshSelection.top;

            // Check each selected mesh
            foreach (var mesh in selection)
            {
                if (mesh == null) continue;

                // If edge mode
                if (currentSelectMode == SelectMode.Edge)
                {
                    // Check if any selected edge is an open edge (part of a hole)
                    var selectedEdges = mesh.selectedEdges;
                    if (selectedEdges.Any(edge => MeshHelpers_General.IsEdgeOpen(mesh, edge)))
                        return true;
                }
                else if (currentSelectMode == SelectMode.Vertex)
                {
                    // Check if any selected vertex is on an open edge (part of a hole)
                    var selectedVertices = mesh.selectedVertices;
                    if (selectedVertices.Any(vertex => MeshHelpers_General.IsVertexOnOpenEdge(mesh, vertex)))
                        return true;
                }
            }

            // No valid open edges or vertices found
            return false;
        }

        public override VisualElement CreateSettingsContent()
        {
            var root = new VisualElement();

            // Load from preferences
            m_FillEntirePath = UserPreferences.Tools.FillEntirePath;

            // Fill hole settings
            var fillEntirePathToggle = new Toggle("Use Entire Border");
            fillEntirePathToggle.tooltip = "When enabled, fills entire border. When disabled, creates polygon from selected vertices only.";
            fillEntirePathToggle.value = m_FillEntirePath;
            fillEntirePathToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                m_FillEntirePath = evt.newValue;
                UserPreferences.Tools.FillEntirePath = evt.newValue;
                UpdatePreview();
            });
            root.Add(fillEntirePathToggle);

            return root;
        }

        public override void StartPreview()
        {
            // Load from preferences if not already loaded
            m_FillEntirePath = UserPreferences.Tools.FillEntirePath;

            // Cache the current selection for consistent preview and application
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedSelectedEdges = new Edge[selection.Length][];
            m_CachedSelectedVertices = new int[selection.Length][];

            // Cache the selected elements for each mesh
            for (int i = 0; i < selection.Length; i++)
            {
                m_CachedSelectedEdges[i] = selection[i].selectedEdges.ToArray();
                m_CachedSelectedVertices[i] = selection[i].selectedVertices.ToArray();
            }

            // Initialize preview state
            m_AllHoles = new Dictionary<ProBuilderMesh, List<List<Edge>>>();
            m_SelectedHoles = new Dictionary<ProBuilderMesh, List<List<Edge>>>();

            // Subscribe to scene GUI for preview drawing
            SceneView.duringSceneGui += OnSceneGUI;

            // Calculate initial preview
            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            if (m_CachedMeshes == null) return;

            // Recalculate holes based on cached selection
            DetectAllHoles();
            UpdateSelectedHoles();
            SceneView.RepaintAll();
        }

        public override ActionResult ApplyChanges()
        {
            // Validate cached data
            if (m_CachedMeshes == null || m_CachedSelectedEdges == null || m_CachedSelectedVertices == null)
                return new ActionResult(ActionResult.Status.Failure, "No cached selection data available");

            if (m_SelectedHoles == null || m_SelectedHoles.Values.Sum(holes => holes.Count) == 0)
                return new ActionResult(ActionResult.Status.Failure, "No holes selected for filling");

            // Record undo for the mesh modifications
            Undo.RecordObjects(m_CachedMeshes, "Fill Holes");

            int totalHolesFilled = 0;
            int totalHolesAttempted = 0;

            // Process each mesh that has selected holes
            foreach (var kvp in m_SelectedHoles)
            {
                var mesh = kvp.Key;
                var selectedHoles = kvp.Value;

                if (mesh == null || selectedHoles.Count == 0) continue;

                // Fill each selected hole in this mesh
                foreach (var hole in selectedHoles)
                {
                    totalHolesAttempted++;

                    // Validate hole is fillable before attempting
                    if (!IsHoleFillable(mesh, hole))
                    {
                        Debug.LogWarning($"Skipping hole with {hole.Count} edges - not fillable");
                        continue;
                    }

                    bool success = false;

                    if (m_FillEntirePath)
                    {
                        // Fill entire hole using first edge as starting point
                        if (hole.Count > 0)
                        {
                            Edge holeEdge = hole[0];
                            success = MeshHelpers_General.FillHole(mesh, holeEdge);
                        }
                    }
                    else
                    {
                        // Fill only selected vertices using AppendElements.CreatePolygon
                        // Create separate polygons for vertices in each hole
                        var meshIndex = System.Array.IndexOf(m_CachedMeshes, mesh);
                        if (meshIndex >= 0)
                        {
                            var selectedVertices = m_CachedSelectedVertices[meshIndex];
                            if (selectedVertices.Length >= 3)
                            {
                                // Find which selected vertices belong to this specific hole
                                var verticesInThisHole = selectedVertices.Where(vertex =>
                                    hole.Any(edge => edge.a == vertex || edge.b == vertex)).ToArray();

                                if (verticesInThisHole.Length >= 3)
                                {
                                    // Create polygon from vertices that belong to this hole only
                                    var vertexList = verticesInThisHole.ToList();
                                    var newFace = AppendElements.CreatePolygon(mesh, vertexList, true); // true = reverseOrder for correct normals
                                    success = newFace != null;

                                    if (success && newFace != null)
                                    {
                                        // Copy properties from adjacent face and fix normals
                                        mesh.ToMesh();
                                        var wings = WingedEdge.GetWingedEdges(mesh);
                                        var wing = wings.FirstOrDefault(x => x.face == newFace);

                                        if (wing?.opposite?.face != null)
                                        {
                                            // Copy properties from adjacent face
                                            newFace.submeshIndex = wing.opposite.face.submeshIndex;
                                            newFace.uv = new AutoUnwrapSettings(wing.opposite.face.uv);
                                        }

                                        // Find all faces that touch this new face
                                        var facesToConform = new List<Face> { newFace };
                                        var newFaceWings = wings.Where(w => w.face == newFace);

                                        foreach (var newWing in newFaceWings)
                                        {
                                            if (newWing.opposite?.face != null && !facesToConform.Contains(newWing.opposite.face))
                                            {
                                                facesToConform.Add(newWing.opposite.face);
                                            }
                                        }

                                        // Fix normals using ConformNormals on new face + adjacent faces
                                        mesh.ConformNormals(facesToConform);

                                    }
                                }
                                else
                                {
                                }
                            }
                        }
                    }

                    if (success)
                    {
                        totalHolesFilled++;
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to fill hole with {hole.Count} edges");
                    }
                }

                // Finalize the mesh after processing all holes
                mesh.ToMesh();
                mesh.Refresh();
                mesh.Optimize();
            }

            ProBuilderEditor.Refresh();

            if (totalHolesFilled > 0)
            {
                string selectionMode = m_FillEntirePath ? "any-selection" : "majority-selection";
                return new ActionResult(ActionResult.Status.Success,
                    $"Filled {totalHolesFilled}/{totalHolesAttempted} holes successfully ({selectionMode} mode)");
            }
            else
                return new ActionResult(ActionResult.Status.Failure,
                    $"Failed to fill any of {totalHolesAttempted} holes attempted");
        }



        /// <summary>
        /// Override to handle selection changes during preview by updating the cached selection.
        /// </summary>
        internal override void OnSelectionChangedDuringPreview()
        {
            // Update cached selection to match the new selection
            var selection = MeshSelection.top.ToArray();
            m_CachedMeshes = selection;
            m_CachedSelectedEdges = new Edge[selection.Length][];
            m_CachedSelectedVertices = new int[selection.Length][];

            // Cache the selected elements for each mesh
            for (int i = 0; i < selection.Length; i++)
            {
                m_CachedSelectedEdges[i] = selection[i].selectedEdges.ToArray();
                m_CachedSelectedVertices[i] = selection[i].selectedVertices.ToArray();
            }

            // Update preview based on new cached selection
            UpdatePreview();
        }

        public override void CleanupPreview()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            m_CachedMeshes = null;
            m_CachedSelectedEdges = null;
            m_CachedSelectedVertices = null;
            m_AllHoles = null;
            m_SelectedHoles = null;
            SceneView.RepaintAll();
        }

        private void DetectAllHoles()
        {
            if (m_AllHoles == null || m_CachedMeshes == null) return;

            m_AllHoles.Clear();

            // Use cached meshes for hole detection
            foreach (var mesh in m_CachedMeshes)
            {
                if (mesh == null) continue;

                // Use ProBuilder's WingedEdge system with proper hole detection algorithm
                mesh.ToMesh();
                List<WingedEdge> wings = WingedEdge.GetWingedEdges(mesh);

                // Find all boundary edges first (edges with no opposite)
                var boundaryEdges = wings.Where(w => w.opposite == null).ToList();

                // Get vertices that are part of boundary edges only
                var boundaryVertices = new HashSet<int>();
                foreach (var edge in boundaryEdges)
                {
                    boundaryVertices.Add(edge.edge.local.a);
                    boundaryVertices.Add(edge.edge.local.b);
                }

                // Convert boundary vertices to shared vertex handles
                HashSet<int> common = MeshHelpers_General.GetSharedVertexHandles(mesh, boundaryVertices);
                var holes = MeshHelpers_General.FindHolesProper(wings, common);

                if (holes.Count > 0)
                {
                    m_AllHoles[mesh] = holes;
                }
            }
        }

        private void UpdateSelectedHoles()
        {
            if (m_SelectedHoles == null || m_AllHoles == null || m_CachedMeshes == null)
                return;

            var prevSelectedCount = m_SelectedHoles.Values.Sum(holes => holes.Count);
            m_SelectedHoles.Clear();

            // Use cached selection data
            for (int i = 0; i < m_CachedMeshes.Length; i++)
            {
                var mesh = m_CachedMeshes[i];
                if (mesh == null || !m_AllHoles.ContainsKey(mesh)) continue;

                var selectedVertices = m_CachedSelectedVertices?[i] ?? new int[0];
                var selectedEdges = m_CachedSelectedEdges?[i] ?? new Edge[0];
                var holes = m_AllHoles[mesh];

                // Get all coincident vertices for the selected vertices
                var allRelatedVertices = new HashSet<int>();
                foreach (var vertex in selectedVertices)
                {
                    // Find the shared vertex group this vertex belongs to
                    foreach (var sharedGroup in mesh.sharedVertices)
                    {
                        if (sharedGroup.Contains(vertex))
                        {
                            // Add all vertices in this shared group
                            foreach (var coincidentVertex in sharedGroup)
                            {
                                allRelatedVertices.Add(coincidentVertex);
                            }
                            break;
                        }
                    }
                }

                // Find holes that contain selected vertices or edges
                foreach (var hole in holes)
                {
                    bool holeIsSelected = false;

                    // Check if any related vertex is part of this hole
                    foreach (var vertex in allRelatedVertices)
                    {
                        if (hole.Any(edge => edge.a == vertex || edge.b == vertex))
                        {
                            holeIsSelected = true;
                            break;
                        }
                    }

                    // Check if any selected edge is part of this hole
                    if (!holeIsSelected)
                    {
                        foreach (var selectedEdge in selectedEdges)
                        {
                            if (hole.Any(edge =>
                                (edge.a == selectedEdge.a && edge.b == selectedEdge.b) ||
                                (edge.a == selectedEdge.b && edge.b == selectedEdge.a)))
                            {
                                holeIsSelected = true;
                                break;
                            }
                        }
                    }

                    if (holeIsSelected)
                    {
                        if (!m_SelectedHoles.ContainsKey(mesh))
                            m_SelectedHoles[mesh] = new List<List<Edge>>();

                        m_SelectedHoles[mesh].Add(hole);
                    }
                }
            }

            var newSelectedCount = m_SelectedHoles.Values.Sum(holes => holes.Count);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawPreviewLines();
        }



        private void DrawPreviewLines()
        {
            if (m_AllHoles == null) return;

            // Draw selected holes in cyan FIRST when Fill Entire is ON
            if (m_FillEntirePath && m_SelectedHoles != null)
            {
                Handles.color = Color.cyan;
                foreach (var kvp in m_SelectedHoles)
                {
                    var mesh = kvp.Key;
                    var holes = kvp.Value;
                    if (mesh == null) continue;

                    var vertices = mesh.GetVertices();
                    foreach (var hole in holes)
                    {
                        var points = new List<Vector3>();
                        foreach (var edge in hole)
                        {
                            if (edge.a < vertices.Length && edge.b < vertices.Length)
                            {
                                if (points.Count == 0)
                                    points.Add(mesh.transform.TransformPoint(vertices[edge.a].position));
                                points.Add(mesh.transform.TransformPoint(vertices[edge.b].position));
                            }
                        }
                        if (points.Count > 1)
                            Handles.DrawAAPolyLine(10f, points.ToArray()); // Thick cyan for selected holes
                    }
                }
            }

            // Draw all holes in orange AFTER (so cyan selected holes show prominently)
            Handles.color = new Color(1f, 0.5f, 0f, 1f); // Orange
            foreach (var kvp in m_AllHoles)
            {
                var mesh = kvp.Key;
                var holes = kvp.Value;
                if (mesh == null) continue;

                var vertices = mesh.GetVertices();
                foreach (var hole in holes)
                {
                    // Skip drawing orange if this hole is selected and Fill Entire is ON
                    bool isSelected = m_FillEntirePath && m_SelectedHoles != null &&
                                     m_SelectedHoles.ContainsKey(mesh) &&
                                     m_SelectedHoles[mesh].Contains(hole);

                    if (!isSelected)
                    {
                        var points = new List<Vector3>();
                        foreach (var edge in hole)
                        {
                            if (edge.a < vertices.Length && edge.b < vertices.Length)
                            {
                                if (points.Count == 0)
                                    points.Add(mesh.transform.TransformPoint(vertices[edge.a].position));
                                points.Add(mesh.transform.TransformPoint(vertices[edge.b].position));
                            }
                        }
                        if (points.Count > 1)
                            Handles.DrawAAPolyLine(6f, points.ToArray()); // Thinner orange for unselected holes
                    }
                }
            }
        }

        /// <summary>
        /// Validates if a hole can be filled based on various criteria.
        /// </summary>
        private bool IsHoleFillable(ProBuilderMesh mesh, List<Edge> hole)
        {
            if (hole == null || hole.Count < 3)
            {
                // Need at least 3 edges to form a valid polygon
                return false;
            }

            if (hole.Count > 100)
            {
                // Prevent attempting to fill extremely complex holes that might cause performance issues
                Debug.LogWarning($"Hole has {hole.Count} edges - may be too complex to fill reliably");
                return false;
            }

            // Validate that all edges in the hole are valid
            var vertices = mesh.GetVertices();
            foreach (var edge in hole)
            {
                if (edge.a < 0 || edge.a >= vertices.Length ||
                    edge.b < 0 || edge.b >= vertices.Length)
                {
                    Debug.LogWarning($"Hole contains invalid edge: {edge.a}-{edge.b}");
                    return false;
                }
            }

            // Check for degenerate edges (edges with zero length)
            const float MIN_EDGE_LENGTH = 0.001f;
            foreach (var edge in hole)
            {
                var vertA = vertices[edge.a].position;
                var vertB = vertices[edge.b].position;
                if (Vector3.Distance(vertA, vertB) < MIN_EDGE_LENGTH)
                {
                    Debug.LogWarning($"Hole contains degenerate edge: {edge.a}-{edge.b}");
                    return false;
                }
            }

            return true;
        }
    }
}
