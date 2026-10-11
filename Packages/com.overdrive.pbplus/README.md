# ProBuilder Plus

**ProBuilder Plus** is a powerful extension for Unity's ProBuilder that adds advanced modeling tools with live preview functionality, enhanced workflows, and comprehensive mesh manipulation capabilities.

## Features

### Live Preview System
All actions feature a real-time preview system that lets you see changes before committing them. Adjust parameters with instant visual feedback, then confirm or cancel.

**Quick Actions:** Hold `Ctrl` while clicking any action button to execute it instantly with default settings.

### Comprehensive Toolset

#### Face Actions
- **Extrude Faces** - Multiple extrusion methods (Individual, Vertex Normal, Face Normal) with customizable direction and space
- **Inset Faces** - Create inset geometry with adjustable distance
- **Bevel Faces** - Chamfer face edges with distance control
- **Subdivide Faces** - Split faces into smaller segments
- **Separate Faces** - Detach or duplicate selected faces
- **Triangulate Faces** - Convert faces to triangles
- **Flip Face Normals** - Reverse normal direction
- **Flip Face Edge** - Rotate quad/triangle edge orientation
- **Conform Face Normals** - Align normals consistently

#### Edge Actions
- **Extrude Edges** - Pull edges outward with adjustable distance
- **Bridge Edges** - Connect two edge loops with geometry
- **Connect Edges** - Insert edges between selected edges
- **Insert Edge Loop** - Add edge loops across faces
- **Bevel Edges** - Chamfer edges with distance control
- **Fill Hole** - Close open edge loops
- **Offset Edges** - Move edges in various coordinate spaces

#### Vertex Actions
- **Connect Vertices** - Create edges between vertices
- **Weld Vertices** - Merge vertices within distance threshold
- **Collapse Vertices** - Merge selected vertices to a single point
- **Bevel Vertices** - Chamfer vertex corners
- **Offset Vertices** - Move vertices in coordinate space
- **Fill Hole** - Close open vertex loops

#### Object Actions
- **ProBuilderize** - Convert standard meshes to ProBuilder meshes
- **Merge Objects** - Combine multiple objects into one
- **Mirror Objects** - Mirror geometry across X, Y, or Z axes
- **Apply Transform** - Bake position, rotation, and scale
- **Subdivide Objects** - Globally subdivide entire meshes
- **Triangulate Objects** - Convert all faces to triangles
- **Flip Object Normals** - Reverse all normals
- **Conform Object Normals** - Align all normals consistently
- **Set Pivot (Interactive)** - Position object pivot interactively

### Enhanced UI
- **Scene Overlay** - Contextual toolbar that adapts to your current selection mode
- **Inspector Panel** - Detailed face/edge/vertex property editor
- **Editor Panel** - Quick access to ProBuilder editors (UV, Materials, Smoothing, etc.)
- **Icon Mode** - Compact button layout option

### Smart Selection Awareness
Actions automatically enable/disable based on your current selection and mode (Object/Face/Edge/Vertex), showing only relevant tools.

## Requirements

- **Unity 6000.0** or later
- **ProBuilder 6.0.0** or later
- **Overdrive Shared 1.0.0** or later (dependency)

## Quick Start

1. **Enable the Overlay:**
   - Select any GameObject with a ProBuilderMesh component
   - Enter ProBuilder edit mode (any element selection mode)
   - Look for the "PB+" overlay in the Scene View

2. **Use an Action:**
   - Select elements (faces, edges, or vertices)
   - Click an action button in the overlay
   - Adjust settings in the preview overlay
   - Click **Confirm** to apply or **Cancel** (or press `ESC`) to abort

3. **Quick Execute:**
   - Hold `Ctrl` while clicking any action to execute it instantly with saved preferences

4. **Customize Settings:**
   - Go to `Edit > Preferences > Overdrive > ProBuilder Plus`
   - Adjust default values for all actions

## Tips

- **ESC to Cancel:** Press Escape at any time to cancel a preview action
- **Instant Mode:** Ctrl+Click bypasses preview for quick operations
- **Persistent Settings:** Your preferences are saved between sessions
- **Icon Mode:** Right-click the overlay and enable "Icon Mode" for a compact layout

## Documentation

For detailed documentation, tutorials, and updates, visit:
**https://www.overdrivetoolset.com/probuilder-plus**

## Support

Issues, questions, or feature requests? Visit our support page:
**https://www.overdrivetoolset.com/support**

## Author

**Overdrive Toolset**
https://www.overdrivetoolset.com

---

*ProBuilder Plus is an independent extension and is not affiliated with Unity Technologies.*
