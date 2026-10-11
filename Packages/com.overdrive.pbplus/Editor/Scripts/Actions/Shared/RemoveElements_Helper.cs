using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Core helper class for Remove Interior Elements functionality.
    /// Contains all the shared logic for processing vertex, edge, and face selections
    /// to remove interior elements and fill resulting holes.
    /// </summary>
    public static class RemoveElements_Helper
    {
        /// <summary>
        /// Converts vertex selection to face selection, accounting for coincident vertices
        /// </summary>
        public static Face[] ConvertVertexSelectionToFaces(ProBuilderMesh mesh)
        {
            var selectedVertices = mesh.selectedVertices;
            if (selectedVertices == null || selectedVertices.Count == 0)
                return new Face[0];

            // Get coincident vertex mapping
            var sharedVertices = mesh.sharedVertices;
            var selectedSharedIndices = new HashSet<int>();

            // Find all shared vertex indices for selected vertices
            foreach (var vertexIndex in selectedVertices)
            {
                foreach (var sharedGroup in sharedVertices)
                {
                    if (sharedGroup.Contains(vertexIndex))
                    {
                        foreach (var coincidentIndex in sharedGroup)
                        {
                            selectedSharedIndices.Add(coincidentIndex);
                        }
                        break;
                    }
                }
            }

            // Find all faces that contain any of the selected vertices (including coincident ones)
            var affectedFaces = new List<Face>();
            foreach (var face in mesh.faces)
            {
                bool containsSelectedVertex = false;
                foreach (var vertexIndex in face.distinctIndexes)
                {
                    if (selectedSharedIndices.Contains(vertexIndex))
                    {
                        containsSelectedVertex = true;
                        break;
                    }
                }

                if (containsSelectedVertex)
                {
                    affectedFaces.Add(face);
                }
            }

            return affectedFaces.ToArray();
        }

        /// <summary>
        /// Converts edge selection to face selection
        /// </summary>
        public static Face[] ConvertEdgeSelectionToFaces(ProBuilderMesh mesh)
        {
            var selectedEdges = mesh.selectedEdges;
            if (selectedEdges == null || selectedEdges.Count == 0)
                return new Face[0];

            var affectedFaces = new HashSet<Face>(); // Use HashSet to avoid duplicates
            var neighborFaces = new List<Face>(); // Temp list for GetNeighborFaces
            
            // Find all faces that contain any of the selected edges
            // Using ProBuilder's built-in method to get neighbor faces for each edge
            foreach (var selectedEdge in selectedEdges)
            {
                neighborFaces.Clear();
                ElementSelection.GetNeighborFaces(mesh, selectedEdge, neighborFaces);
                
                // Add all neighbor faces to our result set
                foreach (var face in neighborFaces)
                {
                    affectedFaces.Add(face);
                }
            }

            return affectedFaces.ToArray();
        }

        /// <summary>
        /// Main processing method that handles the complete remove interior elements operation
        /// </summary>
        public static ActionResult ProcessRemoveInteriorElements(ProBuilderMesh[] meshes, Face[][] targetFaces, float extrudeDistance)
        {
            if (meshes == null || targetFaces == null || meshes.Length != targetFaces.Length)
                return new ActionResult(ActionResult.Status.Failure, "Invalid mesh or face data");

            // Record undo for the actual mesh modification
            Undo.RecordObjects(meshes, "Remove Interior Elements");

            int totalGroupsProcessed = 0;
            int totalNewFaces = 0;
            
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                var mesh = meshes[meshIndex];
                var faces = targetFaces[meshIndex];
                
                if (faces == null || faces.Length == 0) continue;

                
                // Group faces into connected components - each group processed separately
                var faceGroups = GroupConnectedFaces(mesh, faces);
                
                var newlyCreatedFaces = new List<Face>();
                
                // Process each face group independently
                for (int groupIndex = 0; groupIndex < faceGroups.Count; groupIndex++)
                {
                    var faceGroup = faceGroups[groupIndex].ToArray();
                    
                    var newFace = ProcessFaceGroup(mesh, faceGroup, extrudeDistance);
                    if (newFace != null)
                    {
                        newlyCreatedFaces.Add(newFace);
                        totalNewFaces++;
                    }
                    else
                    {
                        Debug.LogWarning($"Group {groupIndex + 1}: Failed to process");
                    }
                    
                    totalGroupsProcessed++;
                }
                
                // Select all newly created faces for this mesh
                if (newlyCreatedFaces.Count > 0)
                {
                    mesh.SetSelectedFaces(newlyCreatedFaces.ToArray());
                }
                
                // Finalize the mesh
                FinalizeMesh(mesh);
            }

            ProBuilderEditor.Refresh();

            if (totalNewFaces > 0)
                return new ActionResult(ActionResult.Status.Success, $"Remove Interior Elements applied to {totalGroupsProcessed} face group(s), created {totalNewFaces} new face(s). New faces selected.");
            else
                return new ActionResult(ActionResult.Status.Failure, "Failed to process any face groups");
        }

        /// <summary>
        /// Groups selected faces into connected components based on shared vertices.
        /// Each group represents faces that are connected to each other.
        /// </summary>
        public static List<List<Face>> GroupConnectedFaces(ProBuilderMesh mesh, Face[] faces)
        {
            if (faces == null || faces.Length == 0)
                return new List<List<Face>>();

            var groups = new List<List<Face>>();
            var processedFaces = new HashSet<Face>();
            var allVertices = mesh.GetVertices();

            foreach (var face in faces)
            {
                if (processedFaces.Contains(face))
                    continue;

                // Start a new group with this face
                var group = new List<Face>();
                var facesToCheck = new Queue<Face>();
                facesToCheck.Enqueue(face);

                while (facesToCheck.Count > 0)
                {
                    var currentFace = facesToCheck.Dequeue();
                    
                    if (processedFaces.Contains(currentFace))
                        continue;

                    processedFaces.Add(currentFace);
                    group.Add(currentFace);

                    // Get vertex positions for this face
                    var currentFacePositions = new HashSet<Vector3>();
                    foreach (var vertexIndex in currentFace.distinctIndexes)
                    {
                        var worldPos = mesh.transform.TransformPoint(allVertices[vertexIndex].position);
                        currentFacePositions.Add(worldPos);
                    }

                    // Find other unprocessed faces that share vertices with this face
                    foreach (var otherFace in faces)
                    {
                        if (processedFaces.Contains(otherFace))
                            continue;

                        // Check if this face shares any vertex positions with the current face
                        bool sharesVertex = false;
                        foreach (var vertexIndex in otherFace.distinctIndexes)
                        {
                            var worldPos = mesh.transform.TransformPoint(allVertices[vertexIndex].position);
                            const float POSITION_TOLERANCE = 0.0001f;
                            
                            foreach (var currentPos in currentFacePositions)
                            {
                                if (Vector3.Distance(worldPos, currentPos) <= POSITION_TOLERANCE)
                                {
                                    sharesVertex = true;
                                    break;
                                }
                            }
                            
                            if (sharesVertex) break;
                        }

                        if (sharesVertex)
                        {
                            facesToCheck.Enqueue(otherFace);
                        }
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// Processes a single face group - either simple deletion or scaffolding approach
        /// Returns the newly created face if successful, null if failed
        /// </summary>
        private static Face ProcessFaceGroup(ProBuilderMesh mesh, Face[] faceGroup, float extrudeDistance)
        {
            // Get connected open edges for this specific face group
            var connectedOpenEdges = GetAllConnectedOpenEdges(mesh, faceGroup);
            
            if (connectedOpenEdges.Count == 0)
            {
                // No open edges - simple case: just delete faces and fill the hole
                return ProcessSimpleFaceGroup(mesh, faceGroup);
            }
            else
            {
                // Complex case with open edges - use scaffolding approach
                return ProcessComplexFaceGroup(mesh, faceGroup, connectedOpenEdges, extrudeDistance);
            }
        }

        /// <summary>
        /// Handles simple face groups with no open edges - direct deletion and filling
        /// </summary>
        private static Face ProcessSimpleFaceGroup(ProBuilderMesh mesh, Face[] faceGroup)
        {
            
            // Get one vertex from the perimeter for hole filling
            var originalPerimeterEdges = ElementSelection.GetPerimeterEdges(mesh, faceGroup).ToList();
            var fillVertexPosition = GetFillVertexPosition(mesh, originalPerimeterEdges);
            
            // Delete the faces in this group
            mesh.DeleteFaces(faceGroup);
            
            // Fill the hole using the tracked vertex position
            var newFace = FillHoleUsingVertexPosition(mesh, fillVertexPosition);
            if (newFace != null)
            {
                return newFace;
            }
            else
            {
                Debug.LogWarning($"Failed to fill hole after deleting faces");
                return null;
            }
        }

        /// <summary>
        /// Handles complex face groups with open edges - scaffolding approach
        /// </summary>
        private static Face ProcessComplexFaceGroup(ProBuilderMesh mesh, Face[] faceGroup, List<Edge> connectedOpenEdges, float extrudeDistance)
        {
            
            // Get one vertex position from the original perimeter for hole filling
            var originalPerimeterEdges = ElementSelection.GetPerimeterEdges(mesh, faceGroup).ToList();
            var fillVertexPosition = GetFillVertexPosition(mesh, originalPerimeterEdges);

            // Track faces before extrusion to identify scaffolding later
            var facesBefore = mesh.faces.ToList();
            
            // Apply the extrusion to connected open edges for this group
            if (!ExtrudePerimeterEdges(mesh, connectedOpenEdges, extrudeDistance))
            {
                Debug.LogWarning($"Failed to extrude perimeter edges");
                return null;
            }

            // Identify the newly created scaffolding faces
            var facesAfter = mesh.faces.ToList();
            var scaffoldingFaces = facesAfter.Skip(facesBefore.Count).ToArray();
            
            
            // Delete the faces in this group after successful extrusion
            mesh.DeleteFaces(faceGroup);
            
            // Fill the hole using the single tracked vertex position
            var newFace = FillHoleUsingVertexPosition(mesh, fillVertexPosition);
            if (newFace == null)
            {
                Debug.LogWarning($"Failed to fill hole");
                return null;
            }

            // Remove the scaffolding faces now that hole is filled
            if (RemoveScaffoldingFaces(mesh, scaffoldingFaces))
            {
                return newFace;
            }
            else
            {
                Debug.LogWarning($"Filled hole but failed to remove scaffolding");
                return newFace; // Still return the face even if scaffolding removal failed
            }
        }

        /// <summary>
        /// Get all open edges that are directly connected to the selected faces by vertex position
        /// This includes ALL open edges in the mesh that share vertex positions with the selected faces
        /// </summary>
        public static List<Edge> GetAllConnectedOpenEdges(ProBuilderMesh mesh, Face[] faces)
        {
            if (mesh == null || faces == null || faces.Length == 0)
                return new List<Edge>();

            var allVertices = mesh.GetVertices();
            
            // Step 1: Get all open edges on the entire mesh
            var allWingedEdges = WingedEdge.GetWingedEdges(mesh);
            var openEdges = allWingedEdges.Where(x => x.opposite == null).Select(y => y.edge.local).ToList();
            
            // Step 2: Create array for edges to extrude
            var edgesToExtrude = new List<Edge>();
            
            // Step 3: Get all vertex positions from the selected faces
            var selectedFaceVertexPositions = new HashSet<Vector3>();
            foreach (var face in faces)
            {
                foreach (var vertexIndex in face.distinctIndexes)
                {
                    var worldPos = mesh.transform.TransformPoint(allVertices[vertexIndex].position);
                    selectedFaceVertexPositions.Add(worldPos);
                }
            }
            
            // Step 4: Search in openEdges for any that share at least 1 vertex position with selected faces
            const float POSITION_TOLERANCE = 0.0001f;
            
            foreach (var openEdge in openEdges)
            {
                var edgeStartPos = mesh.transform.TransformPoint(allVertices[openEdge.a].position);
                var edgeEndPos = mesh.transform.TransformPoint(allVertices[openEdge.b].position);
                
                // Check if either vertex of this open edge shares a position with any selected face vertex
                bool sharesVertex = false;
                foreach (var selectedPos in selectedFaceVertexPositions)
                {
                    if (Vector3.Distance(edgeStartPos, selectedPos) <= POSITION_TOLERANCE ||
                        Vector3.Distance(edgeEndPos, selectedPos) <= POSITION_TOLERANCE)
                    {
                        sharesVertex = true;
                        break;
                    }
                }
                
                // Step 5: Add to edges to extrude if it shares a vertex position
                if (sharesVertex)
                {
                    edgesToExtrude.Add(openEdge);
                }
            }
            
            return edgesToExtrude;
        }

        /// <summary>
        /// Convert edge indices to world positions for preview drawing
        /// </summary>
        public static List<(Vector3, Vector3)> GetEdgePositions(ProBuilderMesh mesh, List<Edge> edges)
        {
            var edgePositions = new List<(Vector3, Vector3)>();
            var allVertices = mesh.GetVertices();

            foreach (var edge in edges)
            {
                Vector3 startPos = mesh.transform.TransformPoint(allVertices[edge.a].position);
                Vector3 endPos = mesh.transform.TransformPoint(allVertices[edge.b].position);
                edgePositions.Add((startPos, endPos));
            }

            return edgePositions;
        }

        /// <summary>
        /// Gets interior perimeter edges - edges that are between selected faces and non-selected faces.
        /// These are different from exterior perimeter edges which go from selected faces to empty space.
        /// </summary>
        public static List<Edge> GetInteriorPerimeterEdges(ProBuilderMesh mesh, Face[] faceGroup, Face[] allSelectedFaces)
        {
            var interiorEdges = new List<Edge>();
            var allVertices = mesh.GetVertices();
            var allFaces = mesh.faces;
            var selectedFaceSet = new HashSet<Face>(allSelectedFaces);
            var faceGroupSet = new HashSet<Face>(faceGroup);
            
            // For each face in this group
            foreach (var face in faceGroup)
            {
                // Get all edges of this face
                var faceEdges = face.edges;
                
                foreach (var edge in faceEdges)
                {
                    // Find all faces that share this edge
                    var facesWithThisEdge = new List<Face>();
                    foreach (var otherFace in allFaces)
                    {
                        if (otherFace.edges.Any(e => e.Equals(edge)))
                        {
                            facesWithThisEdge.Add(otherFace);
                        }
                    }
                    
                    // Check if this edge is between a face in our group and a selected face not in our group
                    bool hasGroupFace = false;
                    bool hasOtherSelectedFace = false;
                    
                    foreach (var sharedFace in facesWithThisEdge)
                    {
                        if (faceGroupSet.Contains(sharedFace))
                            hasGroupFace = true;
                        else if (selectedFaceSet.Contains(sharedFace))
                            hasOtherSelectedFace = true;
                    }
                    
                    // If this edge connects our group to other selected faces, it's an interior perimeter edge
                    if (hasGroupFace && hasOtherSelectedFace)
                    {
                        if (!interiorEdges.Contains(edge))
                        {
                            interiorEdges.Add(edge);
                        }
                    }
                }
            }
            
            return interiorEdges;
        }

        /// <summary>
        /// Extrude perimeter edges outward by the specified distance using ProBuilder's built-in ExtrudeElements
        /// Uses grouped extrude for consistent results across all connected edges
        /// </summary>
        private static bool ExtrudePerimeterEdges(ProBuilderMesh mesh, List<Edge> perimeterEdges, float extrudeDistance)
        {
            if (perimeterEdges.Count == 0) return false;

            try
            {
                // Use ProBuilder's built-in ExtrudeElements.Extrude functionality with grouped extrude
                var extrudedElements = ExtrudeElements.Extrude(mesh, perimeterEdges, extrudeDistance, true, true);
                
                // Check if extrusion was successful
                if (extrudedElements != null && extrudedElements.Length > 0)
                {
                    // Select the new extruded edges
                    mesh.SetSelectedEdges(extrudedElements);
                    return true;
                }
                
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to extrude perimeter edges: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the world position of the first vertex from the perimeter edges.
        /// Simple and reliable - just pick any vertex from the original perimeter.
        /// </summary>
        private static Vector3 GetFillVertexPosition(ProBuilderMesh mesh, List<Edge> perimeterEdges)
        {
            if (perimeterEdges.Count == 0)
                return Vector3.zero;

            var allVertices = mesh.GetVertices();
            var firstEdge = perimeterEdges[0];
            
            // Just use the first vertex of the first perimeter edge
            var localPos = allVertices[firstEdge.a].position;
            var worldPos = mesh.transform.TransformPoint(localPos);
            
            return worldPos;
        }

        /// <summary>
        /// Finds any vertex in the mesh with the same world position and uses it for hole filling.
        /// Returns the newly created face if successful, null if failed.
        /// </summary>
        private static Face FillHoleUsingVertexPosition(ProBuilderMesh mesh, Vector3 targetWorldPosition)
        {
            const float POSITION_TOLERANCE = 0.0001f;

            // Refresh mesh to ensure we have current vertices
            mesh.ToMesh();
            var allVertices = mesh.GetVertices();

            // Find any vertex at the target position
            for (int i = 0; i < allVertices.Length; i++)
            {
                var vertexWorldPos = mesh.transform.TransformPoint(allVertices[i].position);
                var distance = Vector3.Distance(vertexWorldPos, targetWorldPosition);
                
                if (distance <= POSITION_TOLERANCE)
                {
                    
                    // Track faces before fill to identify the new one
                    var facesBefore = mesh.faces.ToList();
                    
                    // Use the vertex-based FillHole method
                    bool success = MeshHelpers_General.FillHole(mesh, i);
                    if (success)
                    {
                        // Find the newly created face
                        var facesAfter = mesh.faces.ToList();
                        var newFaces = facesAfter.Skip(facesBefore.Count).ToArray();
                        
                        if (newFaces.Length > 0)
                        {
                            return newFaces[0]; // Return the first new face
                        }
                        else
                        {
                            Debug.LogWarning($"FillHole succeeded but no new face detected");
                            return null;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"FillHole failed with vertex {i}");
                        return null;
                    }
                }
            }

            Debug.LogWarning($"No vertex found at target position {targetWorldPosition}");
            return null;
        }

        /// <summary>
        /// Removes the scaffolding faces that were created during extrusion.
        /// These faces are no longer needed after the hole has been filled.
        /// </summary>
        private static bool RemoveScaffoldingFaces(ProBuilderMesh mesh, Face[] scaffoldingFaces)
        {
            if (scaffoldingFaces == null || scaffoldingFaces.Length == 0)
            {
                return true;
            }

            try
            {
                // Get current faces to find which ones still exist
                var currentFaces = mesh.faces.ToList();
                var facesToDelete = new List<Face>();
                
                // Find scaffolding faces that still exist in the mesh
                foreach (var scaffoldFace in scaffoldingFaces)
                {
                    // Check if this face still exists by comparing face properties
                    var existingFace = currentFaces.FirstOrDefault(f => 
                        f.indexes.SequenceEqual(scaffoldFace.indexes) ||
                        f.distinctIndexes.SequenceEqual(scaffoldFace.distinctIndexes));
                    
                    if (existingFace != null)
                    {
                        facesToDelete.Add(existingFace);
                    }
                }
                
                if (facesToDelete.Count > 0)
                {
                    mesh.DeleteFaces(facesToDelete);
                    return true;
                }
                else
                {
                    Debug.LogWarning("No matching scaffolding faces found to delete");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to remove scaffolding faces: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finalizes mesh operations - converts to mesh, refreshes, and optimizes
        /// </summary>
        private static void FinalizeMesh(ProBuilderMesh mesh)
        {
            mesh.ToMesh();
            mesh.Refresh();
            mesh.Optimize();
        }
    }
}
