using UnityEngine;
using UnityEngine.UIElements;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Extensions methods that allow to set visual elements optionally as mixed.<br/>
    /// How Mixed is visualized is specific to the individual type.
    /// </summary>
    public static partial class UiExtensions
    {
        // Helper methods for mixed value display

        public static void SetFloatFieldMixed(this FloatField field, bool showMixed, float value, ref bool mixedFlag)
        {
            if (field == null)
            {
                Debug.LogWarning("SetFloatFieldMixed: FloatField is null!");
                return;
            }

            if (showMixed)
            {
                if (!mixedFlag)
                {
                    var textElement = field.Q(className: "unity-text-element__selectable");
                    if (textElement == null)
                    {
                        Debug.LogWarning("SetFloatFieldMixed: Could not find text element in FloatField!");
                        return;
                    }

                    if (textElement is TextElement te)
                    {
                        te.text = "-";
                    }
                    else
                    {
                        Debug.LogWarning($"SetFloatFieldMixed: Element is not TextElement, it's {textElement.GetType().Name}!");
                        return;
                    }

                    mixedFlag = true;
                }
            }
            else
            {
                if (mixedFlag)
                {
                    mixedFlag = false;
                    // Restore the text element to show the actual value
                    var textElement = field.Q(className: "unity-text-element__selectable");
                    if (textElement is TextElement te)
                    {
                        te.text = value.ToString();
                    }
                }
                field.value = value;
            }
        }

        public static void SetVector2FieldMixed(this Vector2Field field, bool showMixed, Vector2 value, ref bool mixedFlag)
        {
            if (field == null)
            {
                Debug.LogWarning("SetVector2FieldMixed: Vector2Field is null!");
                return;
            }

            if (showMixed)
            {
                if (!mixedFlag)
                {
                    var xInput = field.Q(name: "unity-x-input");
                    var yInput = field.Q(name: "unity-y-input");

                    if (xInput == null)
                    {
                        Debug.LogWarning("SetVector2FieldMixed: Could not find unity-x-input!");
                        return;
                    }
                    if (yInput == null)
                    {
                        Debug.LogWarning("SetVector2FieldMixed: Could not find unity-y-input!");
                        return;
                    }

                    var xTextElement = xInput.Q(className: "unity-text-element__selectable");
                    var yTextElement = yInput.Q(className: "unity-text-element__selectable");

                    if (xTextElement == null)
                    {
                        Debug.LogWarning("SetVector2FieldMixed: Could not find X text element!");
                        return;
                    }
                    if (yTextElement == null)
                    {
                        Debug.LogWarning("SetVector2FieldMixed: Could not find Y text element!");
                        return;
                    }

                    if (xTextElement is TextElement xTE && yTextElement is TextElement yTE)
                    {
                        xTE.text = "-";
                        yTE.text = "-";
                    }
                    else
                    {
                        Debug.LogWarning($"SetVector2FieldMixed: Elements are not TextElements! X: {xTextElement.GetType().Name}, Y: {yTextElement.GetType().Name}");
                        return;
                    }

                    mixedFlag = true;
                }
            }
            else
            {
                if (mixedFlag)
                {
                    mixedFlag = false;
                    // Restore the text elements to show the actual values
                    var xInput = field.Q(name: "unity-x-input");
                    var yInput = field.Q(name: "unity-y-input");
                    if (xInput != null && yInput != null)
                    {
                        var xTextElement = xInput.Q(className: "unity-text-element__selectable");
                        var yTextElement = yInput.Q(className: "unity-text-element__selectable");
                        if (xTextElement is TextElement xTE && yTextElement is TextElement yTE)
                        {
                            xTE.text = value.x.ToString();
                            yTE.text = value.y.ToString();
                        }
                    }
                }
                field.value = value;
            }
        }

        public static void SetColorFieldMixed(this UnityEditor.UIElements.ColorField field, bool showMixed, Color value, ref bool mixedFlag)
        {
            if (field == null)
            {
                Debug.LogWarning("SetColorFieldMixed: ColorField is null!");
                return;
            }

            if (showMixed)
            {
                if (!mixedFlag)
                {
                    field.label = "(mixed)";
                    mixedFlag = true;
                }
            }
            else
            {
                if (mixedFlag)
                {
                    mixedFlag = false;
                    field.label = "Vertex Color";
                }
                field.value = value;
            }
        }

        public static void SetObjectFieldMixed(this UnityEditor.UIElements.ObjectField field, bool showMixed, UnityEngine.Object value, ref bool mixedFlag)
        {
            if (field == null)
            {
                Debug.LogWarning("SetObjectFieldMixed: ObjectField is null!");
                return;
            }

            if (showMixed)
            {
                if (!mixedFlag)
                {
                    field.label = "(mixed)";
                    mixedFlag = true;
                }
            }
            else
            {
                if (mixedFlag)
                {
                    mixedFlag = false;
                    field.label = "Material";
                }
                field.value = value;
            }
        }

        public static void SetIntegerFieldMixed(this IntegerField field, bool showMixed, int value, ref bool mixedFlag)
        {
            if (field == null)
            {
                Debug.LogWarning("SetIntegerFieldMixed: IntegerField is null!");
                return;
            }

            if (showMixed)
            {
                if (!mixedFlag)
                {
                    var textElement = field.Q(className: "unity-text-element__selectable");
                    if (textElement == null)
                    {
                        Debug.LogWarning("SetIntegerFieldMixed: Could not find text element in IntegerField!");
                        return;
                    }

                    if (textElement is TextElement te)
                    {
                        te.text = "-";
                    }
                    else
                    {
                        Debug.LogWarning($"SetIntegerFieldMixed: Element is not TextElement, it's {textElement.GetType().Name}!");
                        return;
                    }

                    mixedFlag = true;
                }
            }
            else
            {
                if (mixedFlag)
                {
                    mixedFlag = false;
                    // Restore the text element to show the actual value
                    var textElement = field.Q(className: "unity-text-element__selectable");
                    if (textElement is TextElement te)
                    {
                        te.text = value.ToString();
                    }
                }
                field.value = value;
            }
        }

        public static void SetEnumFieldMixed(this EnumField field, bool showMixed, System.Enum value, ref bool mixedFlag)
        {
            if (field == null)
            {
                Debug.LogWarning("SetEnumFieldMixed: EnumField is null!");
                return;
            }

            if (showMixed)
            {
                if (!mixedFlag)
                {
                    // Clear the field value so no option appears selected in dropdown
                    field.value = null;

                    // Override the display text to show "-"
                    var textElement = field.Q(className: "unity-enum-field__text");
                    if (textElement == null)
                    {
                        Debug.LogWarning("SetEnumFieldMixed: Could not find text element in EnumField!");
                        return;
                    }

                    if (textElement is TextElement te)
                    {
                        te.text = "-";
                    }
                    else
                    {
                        Debug.LogWarning($"SetEnumFieldMixed: Element is not TextElement, it's {textElement.GetType().Name}!");
                        return;
                    }

                    mixedFlag = true;
                }
            }
            else
            {
                if (mixedFlag)
                {
                    mixedFlag = false;
                }
                field.value = value;
            }
        }

        public static void ClearMixedStateOnFocus(this VisualElement field, ref bool mixedFlag)
        {
            if (mixedFlag)
            {
                Debug.LogWarning("UiExtensions: ClearMixedStateOnFocus. This is never called!");
                mixedFlag = false;
                // Field will get updated on next value change
            }
        }
    }
}
