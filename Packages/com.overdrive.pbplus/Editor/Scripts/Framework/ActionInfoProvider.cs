using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Provides Lists of Action Info Instances for use in UI Elements.<br/>
    /// Lists are created once through reflection and are cached and stay immutable during the lifetime of the app domain.
    /// </summary>
    public static class ActionInfoProvider
    {
        private static List<IActionInfo> s_CachedEdgeActions;
        private static List<IActionInfo> s_CachedEditorActions;
        private static List<IActionInfo> s_CachedFaceActions;
        private static List<IActionInfo> s_CachedUvFaceActions;
        private static List<IActionInfo> s_CachedObjectActions;
        private static List<IActionInfo> s_CachedVertexActions;

        public static IReadOnlyList<IActionInfo> GetEditorActions()
        {
            CreateCaches();
            return s_CachedEditorActions;
        }

        public static IReadOnlyList<IActionInfo> GetModeActions(ToolMode toolMode)
        {
            CreateCaches();
            return toolMode switch
            {
                ToolMode.Object => s_CachedObjectActions,
                ToolMode.Face => s_CachedFaceActions,
                ToolMode.Edge => s_CachedEdgeActions,
                ToolMode.Vertex => s_CachedVertexActions,
                ToolMode.UvFace => s_CachedUvFaceActions,
                _ => new List<IActionInfo>(),
            };
        }

        /// <summary>
        /// Helper method to combine auto-discovered and manual actions, removing duplicates.
        /// Auto-discovered actions take precedence over manual ones with the same ID.
        /// </summary>
        private static void AppendManualActions(List<IActionInfo> actions, List<IActionInfo> manualActions)
        {
            var autoIds = new HashSet<string>(actions.Select(static a => a.Id));

            // Add manual actions that don't conflict with auto-discovered ones
            foreach (var manualAction in manualActions)
            {
                if (!autoIds.Contains(manualAction.Id))
                {
                    actions.Add(manualAction);
                }
            }

            // Sort alphabetically by DisplayName
            actions.Sort(Compare);
        }

        public static int Compare(IActionInfo a, IActionInfo b)
        {
            if (a.Order < b.Order)
                return -1;
            else if (a.Order > b.Order)
                return 1;
            else
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static void CreateCaches()
        {
            if (s_CachedEditorActions != null)
                return;

            var typeAttributePairs = ActionAutoDiscovery.DiscoverActions();

            s_CachedEditorActions = new List<IActionInfo>();
            s_CachedObjectActions = new List<IActionInfo>();
            s_CachedFaceActions = new List<IActionInfo>();
            s_CachedEdgeActions = new List<IActionInfo>();
            s_CachedVertexActions = new List<IActionInfo>();
            s_CachedUvFaceActions = new List<IActionInfo>();

            foreach (var valuePair in typeAttributePairs)
            {
                AddIfMatching(ToolMode.Object, ProBuilderPlusActionType.EditorPanel, valuePair, s_CachedEditorActions);
                AddIfMatching(ToolMode.Object, ProBuilderPlusActionType.Action, valuePair, s_CachedObjectActions);
                AddIfMatching(ToolMode.Face, ProBuilderPlusActionType.Action, valuePair, s_CachedFaceActions);
                AddIfMatching(ToolMode.UvFace, ProBuilderPlusActionType.Action, valuePair, s_CachedUvFaceActions);
                AddIfMatching(ToolMode.Edge, ProBuilderPlusActionType.Action, valuePair, s_CachedEdgeActions);
                AddIfMatching(ToolMode.Vertex, ProBuilderPlusActionType.Action, valuePair, s_CachedVertexActions);
            }

            CreateManualActions(
                out List<IActionInfo> manualEditorActions,
                out List<IActionInfo> manualObjectActions,
                out List<IActionInfo> manualEdgeActions,
                out List<IActionInfo> manualFaceActions,
                out List<IActionInfo> manualVertexActions);

            AppendManualActions(s_CachedEditorActions, manualEditorActions);
            AppendManualActions(s_CachedEdgeActions, manualEdgeActions);
            AppendManualActions(s_CachedFaceActions, manualFaceActions);
            AppendManualActions(s_CachedUvFaceActions, manualFaceActions);
            AppendManualActions(s_CachedObjectActions, manualObjectActions);
            AppendManualActions(s_CachedVertexActions, manualVertexActions);
        }

        private static void AddIfMatching(
            ToolMode requestedMode,
            ProBuilderPlusActionType actionType,
            (Type Type, ProBuilderPlusActionAttribute ActionAttribute) valuePair,
            List<IActionInfo> actionsList)
        {
            // Check if this action is valid for the requested mode
            if ((valuePair.ActionAttribute.ValidModes & requestedMode) == 0)
                return;

            // Check if this action is of the requested type
            if (valuePair.ActionAttribute.ActionType != actionType)
                return;




            actionsList.Add(ActionInfo.CreateActionInfoFromAttribute(valuePair.Type, valuePair.ActionAttribute));
            return;
        }

        private static void CreateManualActions(
            out List<IActionInfo> manualEditorActions,
            out List<IActionInfo> manualObjectActions,
            out List<IActionInfo> manualEdgeActions,
            out List<IActionInfo> manualFaceActions,
            out List<IActionInfo> manualVertexActions)
        {
            // ProBuilder Actions can be inserted here as ManuCommands.
            manualEdgeActions = new List<IActionInfo>()
            {
                //// Insert MenuCommands here.
            };

            manualEditorActions = new List<IActionInfo>()
            {
                new MenuCommandActionInfo
                {
                    Id = "lightmap",
                    DisplayName = "Lightmap",
                    Tooltip = "Open Lightmap UV Editor",
                    MenuCommand = "Tools/ProBuilder/Editors/Open Lightmap UV Editor",
                    Icon = ProBuilderPlusActions.LoadIcon( "Icons/Old/Object_LightmapUVs"),
                },
                new MenuCommandActionInfo
                {
                    Id = "material",
                    DisplayName = "Material",
                    Tooltip = "Open Material Editor",
                    MenuCommand = "Tools/ProBuilder/Editors/Open Material Editor",
                    Icon = ProBuilderPlusActions.LoadIcon( "Icons/Old/Panel_Materials"),
                },
                new MenuCommandActionInfo
                {
                    Id = "smoothing",
                    DisplayName = "Smoothing",
                    Tooltip = "Open Smoothing Editor",
                    MenuCommand = "Tools/ProBuilder/Editors/Open Smoothing Editor",
                    Icon = ProBuilderPlusActions.LoadIcon("Icons/Old/Panel_Smoothing"),
                },
                new MenuCommandActionInfo
                {
                    Id = "uv",
                    DisplayName = "UV",
                    Tooltip = "Open UV Editor",
                    MenuCommand = "Tools/ProBuilder/Editors/Open UV Editor",
                    Icon = ProBuilderPlusActions.LoadIcon("Icons/Old/Panel_UVEditor"),
                },
                new MenuCommandActionInfo
                {
                    Id = "color",
                    DisplayName = "Color",
                    Tooltip = "Open Vertex Color Editor",
                    MenuCommand = "Tools/ProBuilder/Editors/Open Vertex Color Editor",
                    Icon = ProBuilderPlusActions.LoadIcon( "Icons/Old/Panel_VertColors"),
                },
                new MenuCommandActionInfo
                {
                    Id = "position",
                    DisplayName = "Position",
                    Tooltip = "Open Vertex Position Editor",
                    MenuCommand = "Tools/ProBuilder/Editors/Open Vertex Position Editor",
                    Icon = ProBuilderPlusActions.LoadIcon("Icons/Old/Panel_Shapes"),
                }
            };

            manualFaceActions = new List<IActionInfo>()
            {
                ////new MenuCommandActionInfo
                ////{
                ////    Id = "extrude",
                ////    DisplayName = "Extrude",
                ////    Tooltip = "Extrude Faces",
                ////    MenuCommand = "Tools/ProBuilder/Geometry/Extrude",
                ////    Icon = ProBuilderPlusActions.LoadIcon("Icons/Old/Face_Extrude.png"),
                ////    IsEnabledCheckFunction = static () => UnityEditor.ProBuilder.MeshSelection.selectedFaceCount > 0,
                ////},
            };

            manualObjectActions = new List<IActionInfo>()
            {
                // Set as CollisionZone
                new MenuCommandActionInfo
                {
                    Id = "setCollisionZone",
                    DisplayName = "To Collider",
                    Tooltip = "Set the selected object(s) as a Collision Zone",
                    MenuCommand = "Tools/ProBuilder/Object/Set Collider",
                    Icon = ProBuilderPlusActions.LoadIcon( "Icons/Old/Entity_Trigger"),
                    IsEnabledCheckFunction = ProBuilderFunctions.IsProBuilderMeshesSelected,
                    Order = 500,
                },

                // Set as TriggerZone
                new MenuCommandActionInfo
                {
                    Id = "setTriggerZone",
                    DisplayName = "To Triggerzone",
                    Tooltip = "Set the selected object(s) as a Trigger Zone",
                    MenuCommand = "Tools/ProBuilder/Object/Set Trigger",
                    Icon = ProBuilderPlusActions.LoadIcon("Icons/Old/Entity_Trigger"),
                    IsEnabledCheckFunction = ProBuilderFunctions.IsProBuilderMeshesSelected,
                    Order = 501,
                }
            };

            manualVertexActions = new List<IActionInfo>()
            {
                //// EXAMPLE how to insert ProBuilder Tools.
                //// new MenuCommandActionInfo
                //// {
                ////     Id = "collapse",
                ////     DisplayName = "Collapse",
                ////     Tooltip = "Collapse Vertices",
                ////     MenuCommand = "Tools/ProBuilder/Geometry/Collapse Vertices",
                ////     Icon = ProBuilderPlusActions.LoadIcon("Assets/ProBuilderPlus/Resources/Icons/Old/Vert_Collapse.png"),
                ////     IsEnabledCheckFunction = static () => UnityEditor.ProBuilder.MeshSelection.selectedVertexCount > 1,
                //// }
            };
        }
    }
}
