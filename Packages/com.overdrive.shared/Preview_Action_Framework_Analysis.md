# Preview Action Framework - Analysis & Migration Plan

**Date:** 2025-09-30
**Source:** PB Plus (`C:/_git/com.overdrive.pbplus`)
**Target:** Overdrive Shared Resources (`C:/_git/com.overdrive.shared`)

---

## Executive Summary

The Preview Action Framework from PB Plus provides a robust pattern for "preview-then-confirm" operations with live visual feedback. It's an excellent candidate for moving to the shared package as it would benefit multiple Overdrive toolsets (Multiverse, Collections, Pins, Scene2.0, etc.).

**Core Value:** Provides consistent UX for operations that need user preview/adjustment before committing changes.

---

## Current Architecture

### Core Components

#### 1. PreviewActionFramework.cs (Static Orchestrator)
**Location:** `Editor/Scripts/Framework/PreviewActionFramework.cs`

**Responsibilities:**
- Central lifecycle management (start/update/end preview)
- Overlay creation and destruction
- Event coordination (selection changes, tool changes, ESC key)
- Auto-cancellation on context changes
- Single active preview enforcement

**Key Methods:**
```csharp
HandleAction(PreviewMenuAction action)      // Entry point
StartPreview(PreviewMenuAction action)      // Initialize preview
ConfirmAction()                             // Apply changes
CancelAction()                              // Discard changes
EndCurrentPreview(bool apply)               // Cleanup
RequestPreviewUpdate()                      // Trigger refresh
```

**Event Subscriptions:**
- `Selection.selectionChanged`
- `ProBuilderEditor.selectionUpdated`
- `ToolManager.activeToolChanged`
- `ToolManager.activeContextChanged`
- `ProBuilderEditor.selectModeChanged`
- `SceneView.duringSceneGui` (ESC key handling)

---

#### 2. PreviewMenuAction.cs (Abstract Base Class)
**Location:** `Editor/Scripts/Framework/PreviewMenuAction.cs`

**Inheritance:** `MenuAction` (ProBuilder)

**Lifecycle Hooks (must implement):**
```csharp
StartPreview()                              // Setup initial preview state
UpdatePreview()                             // Recalculate when parameters change
ApplyChanges() → ActionResult               // Commit changes to mesh
CleanupPreview()                            // Cleanup resources
OnSelectionChangedDuringPreview()           // Handle selection updates (optional)
CreateSettingsContent() → VisualElement     // Build settings UI
```

**Auto-reads from Attribute:**
- `iconPath`, `icon`, `tooltip`, `menuTitle`, `Instructions`

**Sealed Pattern:**
- `PerformActionImplementation()` is sealed - routes to framework

---

#### 3. PreviewActionOverlay.cs (UI Overlay)
**Location:** `Editor/Scripts/Framework/PreviewActionOverlay.cs`

**Inherits:** `Overlay` (Unity)

**UI Elements:**
- Instructions label (from action attribute)
- Settings container (from action's `CreateSettingsContent()`)
- Apply button (green) → calls `ConfirmAction()`
- Cancel button (red) → calls `CancelAction()`

**UXML Template:** `Editor/Resources/UXML/PreviewOverlayUI.uxml`

**Features:**
- ESC key handling
- Auto-focusing for keyboard input
- Dynamic content from active action

---

#### 4. ProBuilderPlusActionAttribute.cs (Metadata)
**Location:** `Editor/Scripts/Framework/ProBuilderPlusActionAttribute.cs`

**Properties:**
```csharp
string Id                           // Unique identifier
string DisplayName                  // UI display name
string Tooltip                      // Button tooltip
string Instructions                 // Overlay instructions text
string IconPath                     // Resource path to icon
string MenuCommand                  // ProBuilder menu command (optional)
SelectMode ValidModes               // Face/Edge/Vertex flags
int Order                           // Toolbar ordering
ProBuilderPlusActionType ActionType // Action vs EditorPanel
bool SupportsInstantMode            // CTRL+click execution
```

**Usage:**
```csharp
[ProBuilderPlusAction("extrude_faces_preview", "Extrude",
    Tooltip = "Extrude faces with live preview",
    Instructions = "Extrude selected faces (cyan lines show new geometry)",
    IconPath = "Icons/Old/Face_Extrude",
    ValidModes = SelectMode.Face,
    Order = 100)]
public class ExtrudeFacesPreviewAction : PreviewMenuAction
```

---

#### 5. ActionAutoDiscovery.cs (Reflection System)
**Location:** `Editor/Scripts/Framework/ActionAutoDiscovery.cs`

**Purpose:** Automatic action registration via reflection

**Discovery Methods:**
```csharp
GetEditorActions()      // EditorPanel actions
GetObjectModeActions()  // Object selection mode
GetFaceModeActions()    // Face selection mode
GetEdgeModeActions()    // Edge selection mode
GetVertexModeActions()  // Vertex selection mode
```

**Process:**
1. Scans all assemblies for classes with `ProBuilderPlusActionAttribute`
2. Filters by `ValidModes` and `ActionType`
3. Creates `ActionInfo` instances with execution delegates
4. Caches results per mode
5. Sorts by Order/DisplayName

**Supports:**
- `IProBuilderPlusAction` implementations
- `MenuAction` implementations (including `PreviewMenuAction`)
- Creates factory methods for preview actions

---

#### 6. ActionInfo.cs (Runtime Descriptor)
**Location:** `Editor/Scripts/Overlay and Panel/ProBuilderPlus_ActionInfo.cs`

**Properties:**
```csharp
string Id, DisplayName, Tooltip, MenuCommand, IconPath
Func<bool> IsEnabled                                    // Availability check
Action CustomAction                                     // Execution delegate
bool SupportsInstantMode
Type ActionType                                         // Reflection type
Func<PreviewMenuAction> CreatePreviewActionInstance     // Factory for previews
```

**Used by:** UI panels to display and invoke actions

---

### Example Implementation: ExtrudeFacesPreviewAction

**File:** `Editor/Scripts/Actions/Face/ExtrudeFaces_PreviewAction.cs`

**Pattern:**
1. **Settings fields** (e.g., `m_ExtrudeDistance`, `m_CustomExtrudeMethod`)
2. **Cache data** (e.g., `m_CachedMeshes`, `m_CachedFaces`, preview geometry)
3. **CreateSettingsContent()** - Build UI with callbacks to `RequestPreviewUpdate()`
4. **StartPreview()** - Cache selection, subscribe to `SceneView.duringSceneGui`, calculate
5. **UpdatePreview()** - Recalculate preview geometry, repaint scene
6. **ApplyChanges()** - Record undo, apply actual operation, return result
7. **CleanupPreview()** - Unsubscribe events, clear cache, repaint
8. **OnSceneGUI()** - Draw preview visualization (cyan lines via Handles API)

---

## ProBuilder-Specific Dependencies

### Hard Dependencies:
- ❌ `MenuAction` base class (ProBuilder)
- ❌ `SelectMode` enum (ProBuilder)
- ❌ `ProBuilderEditor` events (`selectionUpdated`, `selectModeChanged`)
- ❌ `ProBuilderMesh` API (element selection: faces/edges/vertices)
- ❌ `MeshSelection` static class
- ✅ Unity's `Overlay` system (universal)
- ✅ Unity's `ToolManager` events (universal)
- ✅ Unity's `Selection` events (universal)
- ✅ Unity's `SceneView` API (universal)

### What's Reusable:
✅ Preview lifecycle pattern (Start/Update/Apply/Cleanup)
✅ Overlay UI pattern with Apply/Cancel
✅ Auto-discovery via attributes
✅ ESC key cancellation
✅ Selection change tracking concept
✅ Framework orchestration pattern
✅ Settings UI pattern (VisualElement-based)

---

## Migration Strategy to Shared Package

### Phase 1: Create Generic Framework Base

**New Files in `com.overdrive.shared`:**

```
Editor/
  Framework/
    PreviewAction/
      PreviewActionFramework.cs          // Generic orchestrator
      PreviewActionBase.cs               // Generic base (replaces PreviewMenuAction)
      PreviewActionOverlay.cs            // UI overlay (reuse as-is)
      PreviewActionAttribute.cs          // Generic metadata attribute
      PreviewActionAutoDiscovery.cs      // Generic discovery system
      PreviewActionInfo.cs               // Generic runtime descriptor
      IPreviewActionContext.cs           // Interface for selection context
  Resources/
    UXML/
      PreviewOverlayUI.uxml              // Copy from PB Plus
```

---

### Phase 2: Abstract Selection System

**Create pluggable selection context:**

```csharp
public interface IPreviewActionContext
{
    event Action SelectionChanged;
    event Action SelectionModeChanged;
    event Action ToolChanged;

    bool HasValidSelection { get; }
    void RecordUndo(string operationName);
}
```

**PB Plus would provide:**
```csharp
public class ProBuilderPreviewContext : IPreviewActionContext
{
    // Wraps ProBuilderEditor events
    // Wraps ProBuilder selection API
}
```

**Multiverse would provide:**
```csharp
public class MultiversePreviewContext : IPreviewActionContext
{
    // Wraps custom selection system
    // Wraps custom undo system
}
```

---

### Phase 3: Generic Attribute System

**Replace ProBuilderPlusActionAttribute with:**

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class PreviewActionAttribute : Attribute
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Tooltip { get; set; }
    public string Instructions { get; set; }
    public string IconPath { get; set; }
    public int Order { get; set; } = 100;
    public bool SupportsInstantMode { get; set; } = true;

    // Remove: SelectMode ValidModes (toolset-specific)
    // Remove: ProBuilderPlusActionType (toolset-specific)
}
```

**Toolsets add their own filtering:**
```csharp
// PB Plus
[PreviewAction("extrude_faces", "Extrude")]
[ProBuilderValidModes(SelectMode.Face)]  // Additional attribute
public class ExtrudeFacesPreviewAction : ProBuilderPreviewActionBase

// Multiverse
[PreviewAction("transform_objects", "Transform")]
[MultiverseValidContexts(ContextType.Prefab3D)]  // Additional attribute
public class TransformObjectsPreviewAction : MultiversePreviewActionBase
```

---

### Phase 4: Generic Base Class

**PreviewActionBase.cs:**

```csharp
public abstract class PreviewActionBase
{
    // Metadata (auto-read from attribute)
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public virtual string Tooltip => DisplayName;
    public virtual string Instructions => "Configure settings and click Apply.";
    public virtual Texture2D Icon => null;

    // Lifecycle (must implement)
    public abstract void StartPreview(IPreviewActionContext context);
    public abstract void UpdatePreview();
    public abstract bool ApplyChanges();
    public abstract void CleanupPreview();

    // Optional overrides
    public virtual void OnSelectionChangedDuringPreview()
    {
        RefreshPreviewForNewSelection();
    }

    public virtual void RefreshPreviewForNewSelection()
    {
        StartPreview(Context);
    }

    // UI
    public abstract VisualElement CreateSettingsContent();

    // Context
    protected IPreviewActionContext Context { get; private set; }
}
```

---

### Phase 5: PB Plus Adapter Layer

**PB Plus creates thin wrappers:**

```csharp
// New base in PB Plus
public abstract class ProBuilderPreviewActionBase : PreviewActionBase
{
    protected ProBuilderMesh[] CachedMeshes;
    protected ProBuilderPreviewContext PBContext => Context as ProBuilderPreviewContext;

    // Helpers for ProBuilder-specific operations
    protected void CacheCurrentSelection() { /* ... */ }
    protected void SubscribeToSceneGUI() { /* ... */ }
}

// Existing actions inherit from new base
[PreviewAction("extrude_faces", "Extrude")]
[ProBuilderValidModes(SelectMode.Face)]
public class ExtrudeFacesPreviewAction : ProBuilderPreviewActionBase
{
    // Implementation stays mostly the same
}
```

---

## Benefits of Migration

### For PB Plus:
- Reduced codebase (framework moves to shared)
- Easier maintenance (bug fixes in one place)
- Consistent with other Overdrive tools

### For Other Tools:
- **Multiverse:** Preview transforms, mesh edits, prefab operations
- **Collections:** Preview collection operations
- **Scene2.0:** Preview scene block operations
- **Pins:** Preview pin placements

### For Overdrive Ecosystem:
- Consistent UX across all tools
- Reduced code duplication
- Easier to add preview functionality to new tools
- Shared testing and QA

---

## Implementation Checklist

### Shared Package (`com.overdrive.shared`):
- [ ] Create `Editor/Framework/PreviewAction/` folder
- [ ] Implement `PreviewActionFramework.cs` (generic)
- [ ] Implement `PreviewActionBase.cs` (abstract base)
- [ ] Implement `PreviewActionAttribute.cs` (generic attribute)
- [ ] Implement `PreviewActionOverlay.cs` (copy from PB+)
- [ ] Implement `PreviewActionAutoDiscovery.cs` (generic)
- [ ] Implement `PreviewActionInfo.cs` (generic)
- [ ] Implement `IPreviewActionContext.cs` (interface)
- [ ] Copy `PreviewOverlayUI.uxml` to Resources
- [ ] Update assembly definition dependencies

### PB Plus Package (`com.overdrive.pbplus`):
- [ ] Create `ProBuilderPreviewContext.cs` implementation
- [ ] Create `ProBuilderPreviewActionBase.cs` adapter
- [ ] Add `[ProBuilderValidModes]` attribute (optional filtering)
- [ ] Update existing preview actions to inherit from adapter
- [ ] Remove old framework files (move to shared)
- [ ] Update `package.json` dependency version
- [ ] Test all preview actions still work

### Testing:
- [ ] Test ESC cancellation
- [ ] Test selection changes during preview
- [ ] Test tool/context changes auto-cancel
- [ ] Test Apply/Cancel buttons
- [ ] Test instant mode (CTRL+click)
- [ ] Test auto-discovery finds all actions
- [ ] Test multiple sequential previews
- [ ] Test undo/redo integration

---

## File Mapping

### Current (PB Plus) → Target (Shared)

| Current File | New Location | Changes |
|-------------|--------------|---------|
| `Framework/PreviewActionFramework.cs` | `Shared/Editor/Framework/PreviewAction/PreviewActionFramework.cs` | Remove ProBuilder dependencies |
| `Framework/PreviewMenuAction.cs` | `Shared/Editor/Framework/PreviewAction/PreviewActionBase.cs` | Replace MenuAction inheritance, add IPreviewActionContext |
| `Framework/PreviewActionOverlay.cs` | `Shared/Editor/Framework/PreviewAction/PreviewActionOverlay.cs` | Minimal changes (already generic) |
| `Framework/ProBuilderPlusActionAttribute.cs` | `Shared/Editor/Framework/PreviewAction/PreviewActionAttribute.cs` | Remove SelectMode, ActionType |
| `Framework/ActionAutoDiscovery.cs` | `Shared/Editor/Framework/PreviewAction/PreviewActionAutoDiscovery.cs` | Generalize discovery logic |
| `Overlay and Panel/ProBuilderPlus_ActionInfo.cs` | `Shared/Editor/Framework/PreviewAction/PreviewActionInfo.cs` | Remove ProBuilder references |
| `Framework/IProBuilderPlusAction.cs` | *(Keep in PB Plus)* | PB-specific interface |
| `Resources/UXML/PreviewOverlayUI.uxml` | `Shared/Editor/Resources/UXML/PreviewOverlayUI.uxml` | Copy as-is |

---

## API Examples

### Using in PB Plus (After Migration):

```csharp
[PreviewAction("extrude_faces", "Extrude",
    Tooltip = "Extrude faces with live preview",
    Instructions = "Extrude selected faces",
    IconPath = "Icons/Old/Face_Extrude")]
public class ExtrudeFacesPreviewAction : ProBuilderPreviewActionBase
{
    private float m_Distance;

    public override void StartPreview(IPreviewActionContext context)
    {
        base.StartPreview(context);
        CacheCurrentSelection();
        SubscribeToSceneGUI();
        UpdatePreview();
    }

    public override void UpdatePreview()
    {
        CalculatePreviewGeometry();
        SceneView.RepaintAll();
    }

    public override bool ApplyChanges()
    {
        Context.RecordUndo("Extrude Faces");
        // Apply actual operation
        return true;
    }

    public override void CleanupPreview()
    {
        UnsubscribeFromSceneGUI();
        ClearCache();
    }

    public override VisualElement CreateSettingsContent()
    {
        var root = new VisualElement();
        var distanceField = new FloatField("Distance");
        distanceField.RegisterCallback<ChangeEvent<float>>(evt => {
            m_Distance = evt.newValue;
            PreviewActionFramework.RequestPreviewUpdate();
        });
        root.Add(distanceField);
        return root;
    }
}
```

### Using in Multiverse (New):

```csharp
[PreviewAction("move_objects", "Move Objects",
    Instructions = "Preview object movement")]
public class MoveObjectsPreviewAction : MultiversePreviewActionBase
{
    private Vector3 m_Offset;

    public override void StartPreview(IPreviewActionContext context)
    {
        base.StartPreview(context);
        CacheSelectedTransforms();
        UpdatePreview();
    }

    public override void UpdatePreview()
    {
        // Draw gizmos showing new positions
    }

    public override bool ApplyChanges()
    {
        Context.RecordUndo("Move Objects");
        // Apply movement
        return true;
    }

    // ... etc
}
```

---

## Notes & Considerations

### Backwards Compatibility:
- Keep PB Plus working during migration
- Consider deprecation warnings for old API
- Provide migration guide for any custom actions users created

### Version Strategy:
- Bump `com.overdrive.shared` to `1.1.0` (new feature)
- Bump `com.overdrive.pbplus` to `1.1.0` (dependency update)
- Update all toolsets to use new shared version

### Documentation Needed:
- Framework architecture overview
- How to create a preview action
- How to implement IPreviewActionContext for new toolsets
- Migration guide from old to new API

### Future Enhancements:
- Async preview support (for heavy calculations)
- Multiple preview modes (wireframe, solid, etc.)
- Preview comparison (before/after slider)
- Preview settings persistence
- Preview action chaining (one preview leads to another)

---

## Questions to Resolve

1. **Naming:** "PreviewAction" vs "PreviewOperation" vs something else?
2. **Namespace:** `Overdrive.Shared.Framework.PreviewAction`?
3. **Assembly:** Create separate asmdef or use existing `Overdrive.Shared.asmdef`?
4. **Icon Loading:** Keep tool-specific or provide shared icon loader?
5. **Undo System:** Require `IPreviewActionContext.RecordUndo()` or keep flexible?
6. **Error Handling:** Standardize error reporting across toolsets?

---

## Timeline Estimate

- **Phase 1-2:** 1-2 days (create generic framework base + selection abstraction)
- **Phase 3-4:** 1 day (attribute system + base class)
- **Phase 5:** 1-2 days (PB Plus adapter + migration)
- **Testing:** 1 day
- **Documentation:** 0.5 day

**Total:** ~5-6 days

---

## Conclusion

The Preview Action Framework is well-architected and ready for extraction to the shared package. The main work is abstracting ProBuilder-specific dependencies (selection modes, MenuAction base class, event systems) into interfaces that each toolset can implement.

The resulting shared framework will provide significant value across the Overdrive ecosystem while maintaining the robust functionality that makes it great in PB Plus.
