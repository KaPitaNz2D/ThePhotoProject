using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Overdrive.Framework
{
    /// <summary>
    /// Shared utility for rendering Unity properties via IMGUI within UIToolkit containers.
    /// Ensures consistent behavior, proper context menu support, and opacity handling across packages.
    /// </summary>
    public static class IMGUIPropertyRenderer
    {
        /// <summary>
        /// Creates an IMGUIContainer that renders a single SerializedProperty.
        /// The container maintains proper IMGUI context for right-click menus.
        /// </summary>
        /// <param name="property">The SerializedProperty to render</param>
        /// <param name="serializedObject">The SerializedObject containing the property</param>
        /// <param name="opacity">Optional opacity value (0-1) for rendering</param>
        /// <param name="includeChildren">Whether to include child properties when rendering</param>
        /// <returns>An IMGUIContainer configured to render the property</returns>
        public static IMGUIContainer CreatePropertyContainer(
            SerializedProperty property,
            SerializedObject serializedObject,
            float opacity = 1.0f,
            bool includeChildren = true)
        {
            if (property == null)
            {
                Debug.LogWarning("[IMGUIPropertyRenderer] Cannot create container: property is null");
                return new IMGUIContainer();
            }

            if (serializedObject == null)
            {
                Debug.LogWarning("[IMGUIPropertyRenderer] Cannot create container: serializedObject is null");
                return new IMGUIContainer();
            }

            return new IMGUIContainer(() =>
            {
                DrawProperty(property, serializedObject, opacity, includeChildren);
            });
        }

        /// <summary>
        /// Creates an IMGUIContainer that renders all properties of a Component.
        /// The container maintains proper IMGUI context for right-click menus.
        /// Skips the "m_Script" field by default.
        /// </summary>
        /// <param name="component">The Component to render properties for</param>
        /// <param name="opacity">Optional opacity value (0-1) for rendering</param>
        /// <param name="skipScriptField">Whether to skip the m_Script field</param>
        /// <returns>An IMGUIContainer configured to render all component properties</returns>
        public static IMGUIContainer CreateComponentContainer(
            Component component,
            float opacity = 1.0f,
            bool skipScriptField = true)
        {
            if (component == null)
            {
                Debug.LogWarning("[IMGUIPropertyRenderer] Cannot create container: component is null");
                return new IMGUIContainer();
            }

            return new IMGUIContainer(() =>
            {
                DrawComponentProperties(component, opacity, skipScriptField);
            });
        }

        /// <summary>
        /// Draws a single SerializedProperty with optional opacity.
        /// Use this inside an IMGUIContainer's onGUIHandler.
        /// </summary>
        /// <param name="property">The SerializedProperty to draw</param>
        /// <param name="serializedObject">The SerializedObject containing the property</param>
        /// <param name="opacity">Optional opacity value (0-1)</param>
        /// <param name="includeChildren">Whether to include child properties when drawing</param>
        public static void DrawProperty(
            SerializedProperty property,
            SerializedObject serializedObject,
            float opacity = 1.0f,
            bool includeChildren = true)
        {
            if (property == null || serializedObject == null)
                return;

            var originalColor = GUI.color;
            var originalContentColor = GUI.contentColor;
            var originalBackgroundColor = GUI.backgroundColor;

            try
            {
                ApplyOpacity(opacity);

                serializedObject.Update();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(property, includeChildren);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
            finally
            {
                RestoreColors(originalColor, originalContentColor, originalBackgroundColor);
            }
        }

        /// <summary>
        /// Draws all properties of a Component with optional opacity.
        /// Use this inside an IMGUIContainer's onGUIHandler.
        /// </summary>
        /// <param name="component">The Component to draw properties for</param>
        /// <param name="opacity">Optional opacity value (0-1)</param>
        /// <param name="skipScriptField">Whether to skip the m_Script field</param>
        public static void DrawComponentProperties(
            Component component,
            float opacity = 1.0f,
            bool skipScriptField = true)
        {
            if (component == null)
                return;

            var originalColor = GUI.color;
            var originalContentColor = GUI.contentColor;
            var originalBackgroundColor = GUI.backgroundColor;

            try
            {
                ApplyOpacity(opacity);

                var serializedObject = new SerializedObject(component);
                serializedObject.Update();

                var prop = serializedObject.GetIterator();
                if (prop.NextVisible(true))
                {
                    do
                    {
                        if (skipScriptField && prop.name == "m_Script")
                            continue;

                        EditorGUILayout.PropertyField(prop, true);
                    }
                    while (prop.NextVisible(false));
                }

                serializedObject.ApplyModifiedProperties();
            }
            finally
            {
                RestoreColors(originalColor, originalContentColor, originalBackgroundColor);
            }
        }

        /// <summary>
        /// Draws a GameObject header using Unity's built-in Inspector header drawing.
        /// Useful for custom inspectors that want to show GameObject metadata.
        /// </summary>
        /// <param name="target">The GameObject to draw the header for</param>
        /// <param name="opacity">Optional opacity value (0-1)</param>
        public static void DrawGameObjectHeader(GameObject target, float opacity = 1.0f)
        {
            if (target == null)
                return;

            var originalColor = GUI.color;
            var originalContentColor = GUI.contentColor;
            var originalBackgroundColor = GUI.backgroundColor;

            try
            {
                ApplyOpacity(opacity);

                var serializedObject = new SerializedObject(target);
                serializedObject.Update();

                var editor = Editor.CreateEditor(target);
                if (editor != null)
                {
                    try
                    {
                        editor.DrawHeader();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[IMGUIPropertyRenderer] Error drawing GameObject header: {e.Message}");
                        EditorGUILayout.LabelField($"GameObject: {target.name}");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(editor);
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IMGUIPropertyRenderer] Error in DrawGameObjectHeader: {e.Message}");
            }
            finally
            {
                RestoreColors(originalColor, originalContentColor, originalBackgroundColor);
            }
        }

        /// <summary>
        /// Creates an IMGUIContainer that renders a GameObject header.
        /// </summary>
        /// <param name="target">The GameObject to render header for</param>
        /// <param name="opacity">Optional opacity value (0-1)</param>
        /// <returns>An IMGUIContainer configured to render the GameObject header</returns>
        public static IMGUIContainer CreateGameObjectHeaderContainer(
            GameObject target,
            float opacity = 1.0f)
        {
            return new IMGUIContainer(() =>
            {
                DrawGameObjectHeader(target, opacity);
            });
        }

        private static void ApplyOpacity(float opacity)
        {
            if (opacity >= 1.0f)
                return;

            GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, opacity);
            GUI.contentColor = new Color(GUI.contentColor.r, GUI.contentColor.g, GUI.contentColor.b, opacity);
            GUI.backgroundColor = new Color(GUI.backgroundColor.r, GUI.backgroundColor.g, GUI.backgroundColor.b, opacity);
        }

        private static void RestoreColors(Color color, Color contentColor, Color backgroundColor)
        {
            GUI.color = color;
            GUI.contentColor = contentColor;
            GUI.backgroundColor = backgroundColor;
        }
    }
}
