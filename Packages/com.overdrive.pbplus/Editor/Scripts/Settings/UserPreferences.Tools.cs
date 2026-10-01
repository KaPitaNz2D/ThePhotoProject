using UnityEditor;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Reads or Writes ProBuilderPlus Settings in/from Unity EditorPrefs. 
    /// </summary>
    public static partial class UserPreferences
    {
        public static class Tools
        {
            // Vertex Action Preferences
            private const string k_CollapseToFirstKey = k_PreferencePrefix + "CollapseToFirst";
            private const string k_WeldDistanceKey = k_PreferencePrefix + "WeldDistance";

            // Edge Action Preferences
            private const string k_ExtrudeDistanceKey = k_PreferencePrefix + "ExtrudeDistance";
            private const string k_ExtrudeAsGroupKey = k_PreferencePrefix + "ExtrudeAsGroup";
            private const string k_LoopPositionKey = k_PreferencePrefix + "LoopPosition";
            private const string k_LoopDirectionKey = k_PreferencePrefix + "LoopDirection";
            private const string k_LoopModeKey = k_PreferencePrefix + "LoopMode";
            private const string k_BridgeRotationOffsetKey = k_PreferencePrefix + "BridgeRotationOffset";
            private const string k_BridgeReverseOrderKey = k_PreferencePrefix + "BridgeReverseOrder";
            private const string k_BridgeUseFullBordersKey = k_PreferencePrefix + "BridgeUseFullBorders";
            private const string k_ConnectionPositionKey = k_PreferencePrefix + "ConnectionPosition";
            private const string k_ConnectionDirectionKey = k_PreferencePrefix + "ConnectionDirection";
            private const string k_ConnectionModeKey = k_PreferencePrefix + "ConnectionMode";

            // Face Action Preferences
            private const string k_ExtrudeFacesDistanceKey = k_PreferencePrefix + "ExtrudeFacesDistance";
            private const string k_ExtrudeFacesMethodKey = k_PreferencePrefix + "ExtrudeFacesMethod";
            private const string k_ExtrudeFacesSpaceKey = k_PreferencePrefix + "ExtrudeFacesSpace";
            private const string k_ExtrudeFacesAxisKey = k_PreferencePrefix + "ExtrudeFacesAxis";
            private const string k_InsetFacesDistanceKey = k_PreferencePrefix + "InsetFacesDistance";
            private const string k_ConformNormalsOtherDirectionKey = k_PreferencePrefix + "ConformNormalsOtherDirection";
            private const string k_SeparateFacesCreateNewObjectKey = k_PreferencePrefix + "SeparateFacesCreateNewObject";
            private const string k_SeparateFacesDuplicateFacesKey = k_PreferencePrefix + "SeparateFacesDuplicateFaces";

            // Shared Action Preferences
            private const string k_RemoveExtrudeDistanceKey = k_PreferencePrefix + "RemoveExtrudeDistance";
            private const string k_FillEntirePathKey = k_PreferencePrefix + "FillEntirePath";
            private const string k_BevelDistanceKey = k_PreferencePrefix + "BevelDistance";
            private const string k_BevelPerimeterOnlyKey = k_PreferencePrefix + "BevelPerimeterOnly";
            private const string k_OffsetCoordinateSpaceKey = k_PreferencePrefix + "OffsetCoordinateSpace";
            private const string k_OffsetVectorXKey = k_PreferencePrefix + "OffsetVectorX";
            private const string k_OffsetVectorYKey = k_PreferencePrefix + "OffsetVectorY";
            private const string k_OffsetVectorZKey = k_PreferencePrefix + "OffsetVectorZ";

            // Object Action Preferences
            private const string k_MirrorSettingsKey = k_PreferencePrefix + "MirrorSettings";
            private const string k_ApplyPositionKey = k_PreferencePrefix + "ApplyPosition";
            private const string k_ApplyRotationKey = k_PreferencePrefix + "ApplyRotation";
            private const string k_ApplyScaleKey = k_PreferencePrefix + "ApplyScale";
            private const string k_ConformObjectNormalsOtherDirectionKey = k_PreferencePrefix + "ConformObjectNormalsOtherDirection";
            private const string k_ImportQuadsKey = k_PreferencePrefix + "ImportQuads";
            private const string k_ImportSmoothingKey = k_PreferencePrefix + "ImportSmoothing";
            private const string k_SmoothingAngleKey = k_PreferencePrefix + "SmoothingAngle";

            // Collapse Vertices Settings
            public static bool CollapseToFirst
            {
                get { return EditorPrefs.GetBool(k_CollapseToFirstKey, false); }
                set { EditorPrefs.SetBool(k_CollapseToFirstKey, value); }
            }

            // Weld Vertices Settings
            public static float WeldDistance
            {
                get { return EditorPrefs.GetFloat(k_WeldDistanceKey, 0.01f); }
                set { EditorPrefs.SetFloat(k_WeldDistanceKey, value); }
            }

            // Extrude Edges Settings
            public static float ExtrudeDistance
            {
                get { return EditorPrefs.GetFloat(k_ExtrudeDistanceKey, 0.5f); }
                set { EditorPrefs.SetFloat(k_ExtrudeDistanceKey, value); }
            }

            public static bool ExtrudeAsGroup
            {
                get { return EditorPrefs.GetBool(k_ExtrudeAsGroupKey, false); }
                set { EditorPrefs.SetBool(k_ExtrudeAsGroupKey, value); }
            }

            // Insert Edge Loop Settings
            public static float LoopPosition
            {
                get { return EditorPrefs.GetFloat(k_LoopPositionKey, 0.5f); }
                set { EditorPrefs.SetFloat(k_LoopPositionKey, value); }
            }

            public static int LoopDirection
            {
                get { return EditorPrefs.GetInt(k_LoopDirectionKey, 0); } // 0 = FromLeft
                set { EditorPrefs.SetInt(k_LoopDirectionKey, value); }
            }

            public static int LoopMode
            {
                get { return EditorPrefs.GetInt(k_LoopModeKey, 0); } // 0 = Percent
                set { EditorPrefs.SetInt(k_LoopModeKey, value); }
            }

            // Bridge Edges Settings
            public static int BridgeRotationOffset
            {
                get { return EditorPrefs.GetInt(k_BridgeRotationOffsetKey, 0); }
                set { EditorPrefs.SetInt(k_BridgeRotationOffsetKey, value); }
            }

            public static bool BridgeReverseOrder
            {
                get { return EditorPrefs.GetBool(k_BridgeReverseOrderKey, false); }
                set { EditorPrefs.SetBool(k_BridgeReverseOrderKey, value); }
            }

            public static bool BridgeUseFullBorders
            {
                get { return EditorPrefs.GetBool(k_BridgeUseFullBordersKey, true); }
                set { EditorPrefs.SetBool(k_BridgeUseFullBordersKey, value); }
            }

            // Connect Edges Settings
            public static float ConnectionPosition
            {
                get { return EditorPrefs.GetFloat(k_ConnectionPositionKey, 0.5f); }
                set { EditorPrefs.SetFloat(k_ConnectionPositionKey, value); }
            }

            public static int ConnectionDirection
            {
                get { return EditorPrefs.GetInt(k_ConnectionDirectionKey, 0); } // 0 = FromLeft
                set { EditorPrefs.SetInt(k_ConnectionDirectionKey, value); }
            }

            public static int ConnectionMode
            {
                get { return EditorPrefs.GetInt(k_ConnectionModeKey, 0); } // 0 = Percent
                set { EditorPrefs.SetInt(k_ConnectionModeKey, value); }
            }

            // Extrude Faces Settings
            public static float ExtrudeFacesDistance
            {
                get { return EditorPrefs.GetFloat(k_ExtrudeFacesDistanceKey, 0.5f); }
                set { EditorPrefs.SetFloat(k_ExtrudeFacesDistanceKey, value); }
            }

            public static int ExtrudeFacesMethod
            {
                get { return EditorPrefs.GetInt(k_ExtrudeFacesMethodKey, 1); } // 1 = VertexNormal
                set { EditorPrefs.SetInt(k_ExtrudeFacesMethodKey, value); }
            }

            public static int ExtrudeFacesSpace
            {
                get { return EditorPrefs.GetInt(k_ExtrudeFacesSpaceKey, 1); } // 1 = Global
                set { EditorPrefs.SetInt(k_ExtrudeFacesSpaceKey, value); }
            }

            public static int ExtrudeFacesAxis
            {
                get { return EditorPrefs.GetInt(k_ExtrudeFacesAxisKey, 2); } // 2 = Z
                set { EditorPrefs.SetInt(k_ExtrudeFacesAxisKey, value); }
            }

            // Inset Faces Settings
            public static float InsetFacesDistance
            {
                get { return EditorPrefs.GetFloat(k_InsetFacesDistanceKey, 0.3f); }
                set { EditorPrefs.SetFloat(k_InsetFacesDistanceKey, value); }
            }

            // Conform Face Normals Settings
            public static bool ConformNormalsOtherDirection
            {
                get { return EditorPrefs.GetBool(k_ConformNormalsOtherDirectionKey, false); }
                set { EditorPrefs.SetBool(k_ConformNormalsOtherDirectionKey, value); }
            }

            // Separate Faces Settings
            public static bool SeparateFacesCreateNewObject
            {
                get { return EditorPrefs.GetBool(k_SeparateFacesCreateNewObjectKey, false); }
                set { EditorPrefs.SetBool(k_SeparateFacesCreateNewObjectKey, value); }
            }

            public static bool SeparateFacesDuplicateFaces
            {
                get { return EditorPrefs.GetBool(k_SeparateFacesDuplicateFacesKey, false); }
                set { EditorPrefs.SetBool(k_SeparateFacesDuplicateFacesKey, value); }
            }

            // Remove Elements Settings
            public static float RemoveExtrudeDistance
            {
                get { return EditorPrefs.GetFloat(k_RemoveExtrudeDistanceKey, 1.0f); }
                set { EditorPrefs.SetFloat(k_RemoveExtrudeDistanceKey, value); }
            }

            // Fill Hole Settings
            public static bool FillEntirePath
            {
                get { return EditorPrefs.GetBool(k_FillEntirePathKey, true); }
                set { EditorPrefs.SetBool(k_FillEntirePathKey, value); }
            }

            // Bevel Elements Settings
            public static float BevelDistance
            {
                get { return EditorPrefs.GetFloat(k_BevelDistanceKey, 0.2f); }
                set { EditorPrefs.SetFloat(k_BevelDistanceKey, value); }
            }

            public static bool BevelPerimeterOnly
            {
                get { return EditorPrefs.GetBool(k_BevelPerimeterOnlyKey, false); }
                set { EditorPrefs.SetBool(k_BevelPerimeterOnlyKey, value); }
            }

            // Offset Elements Settings
            public static int OffsetCoordinateSpace
            {
                get { return EditorPrefs.GetInt(k_OffsetCoordinateSpaceKey, 0); } // 0 = World
                set { EditorPrefs.SetInt(k_OffsetCoordinateSpaceKey, value); }
            }

            public static UnityEngine.Vector3 OffsetVector
            {
                get
                {
                    return new UnityEngine.Vector3(
                        EditorPrefs.GetFloat(k_OffsetVectorXKey, 0f),
                        EditorPrefs.GetFloat(k_OffsetVectorYKey, 1f),
                        EditorPrefs.GetFloat(k_OffsetVectorZKey, 0f)
                    );
                }
                set
                {
                    EditorPrefs.SetFloat(k_OffsetVectorXKey, value.x);
                    EditorPrefs.SetFloat(k_OffsetVectorYKey, value.y);
                    EditorPrefs.SetFloat(k_OffsetVectorZKey, value.z);
                }
            }

            // Mirror Objects Settings
            public static int MirrorSettings
            {
                get { return EditorPrefs.GetInt(k_MirrorSettingsKey, 1); } // 1 = X axis default
                set { EditorPrefs.SetInt(k_MirrorSettingsKey, value); }
            }

            // Apply Transform Settings
            public static bool ApplyPosition
            {
                get { return EditorPrefs.GetBool(k_ApplyPositionKey, true); }
                set { EditorPrefs.SetBool(k_ApplyPositionKey, value); }
            }

            public static bool ApplyRotation
            {
                get { return EditorPrefs.GetBool(k_ApplyRotationKey, true); }
                set { EditorPrefs.SetBool(k_ApplyRotationKey, value); }
            }

            public static bool ApplyScale
            {
                get { return EditorPrefs.GetBool(k_ApplyScaleKey, true); }
                set { EditorPrefs.SetBool(k_ApplyScaleKey, value); }
            }

            // Conform Object Normals Settings
            public static bool ConformObjectNormalsOtherDirection
            {
                get { return EditorPrefs.GetBool(k_ConformObjectNormalsOtherDirectionKey, false); }
                set { EditorPrefs.SetBool(k_ConformObjectNormalsOtherDirectionKey, value); }
            }

            // ProBuilderize Settings
            public static bool ImportQuads
            {
                get { return EditorPrefs.GetBool(k_ImportQuadsKey, true); }
                set { EditorPrefs.SetBool(k_ImportQuadsKey, value); }
            }

            public static bool ImportSmoothing
            {
                get { return EditorPrefs.GetBool(k_ImportSmoothingKey, true); }
                set { EditorPrefs.SetBool(k_ImportSmoothingKey, value); }
            }

            public static float SmoothingAngle
            {
                get { return EditorPrefs.GetFloat(k_SmoothingAngleKey, 1f); }
                set { EditorPrefs.SetFloat(k_SmoothingAngleKey, value); }
            }
        }
    }
}