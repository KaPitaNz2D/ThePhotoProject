/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

#if YS_USE_TEXT_ANIMATOR_2 || YS_USE_TEXT_ANIMATOR_3
#define USE_TEXT_ANIMATOR
#endif

namespace Yarn.Unity.Addons.TextAnimatorIntegration
{
#nullable enable
    using Yarn.Markup;

    public partial class TextAnimatorMarkupManager
    {
#if USE_TEXT_ANIMATOR
        private enum MarkupType
        {
            Unknown, Behaviour, Action, Appearances,
        }

        private const string disappearanceAttributeKey = "disappearance";
        private const string typeAttributeKey = "type";
        private const string trimAttributeKey = "trimwhitespace";
        private const string behaviourValue = "behaviour";
        private const string actionValue = "action";
        private const string appearanceValue = "appearance";

        // how do I handle playback parameters (aka <wave once> sorta things?), I think we just ignore that and in the docs go something like:
        // "if you have very complex ones we suggest creating them as a palette markup which already handles direct replacement"

        // converts a Yarn Spinner marker into Text Animator form
        public static bool ConvertedForm(MarkupAttribute attribute, out string frontReplacement, out string backReplacement)
        {
            MarkupType markupType = MarkupType.Unknown;
            if (attribute.TryGetProperty(typeAttributeKey, out string? result))
            {
                switch (result.ToLower())
                {
                    case behaviourValue:
                        markupType = MarkupType.Behaviour;
                        break;
                    case actionValue:
                        markupType = MarkupType.Action;
                        break;
                    case appearanceValue:
                        markupType = MarkupType.Appearances;
                        break;
                }
            }

            char opening = default;
            char closing = default;
            switch (markupType)
            {
                case MarkupType.Behaviour:
                    {
                        if (IsBehaviourTag(attribute.Name))
                        {
                            opening = TextAnimatorMarkupManager.OpeningBehavioursBracket;
                            closing = TextAnimatorMarkupManager.ClosingBehavioursBracket;
                        }
                        break;
                    }
                case MarkupType.Action:
                    {
                        if (IsActionTag(attribute.Name))
                        {
                            opening = TextAnimatorMarkupManager.OpeningActionsBracket;
                            closing = TextAnimatorMarkupManager.ClosingActionsBracket;
                        }
                        break;
                    }
                case MarkupType.Appearances:
                    {
                        if (IsAppearanceTag(attribute.Name))
                        {
                            opening = TextAnimatorMarkupManager.OpeningAppearancesBracket;
                            closing = TextAnimatorMarkupManager.ClosingAppearancesBracket;
                        }
                        break;
                    }
                case MarkupType.Unknown:
                    {
                        // we don't know what it is
                        // so we are gonna check them all, one by one
                        // but first we'll check to see if we have done this before for this tag
                        if (cachedAttributeTypes.TryGetValue(attribute.Name, out var tuple))
                        {
                            opening = tuple.start;
                            closing = tuple.end;
                            break;
                        }
                        else
                        {
                            if (IsBehaviourTag(attribute.Name))
                            {
                                opening = TextAnimatorMarkupManager.OpeningBehavioursBracket;
                                closing = TextAnimatorMarkupManager.ClosingBehavioursBracket;
                                cachedAttributeTypes.TryAdd(attribute.Name, (opening, closing));
                                break;
                            }
                            if (IsActionTag(attribute.Name))
                            {
                                opening = TextAnimatorMarkupManager.OpeningActionsBracket;
                                closing = TextAnimatorMarkupManager.ClosingActionsBracket;
                                cachedAttributeTypes.TryAdd(attribute.Name, (opening, closing));
                                break;
                            }
                            if (IsAppearanceTag(attribute.Name))
                            {
                                opening = TextAnimatorMarkupManager.OpeningAppearancesBracket;
                                closing = TextAnimatorMarkupManager.ClosingAppearancesBracket;
                                cachedAttributeTypes.TryAdd(attribute.Name, (opening, closing));
                                break;
                            }
                        }

                        // if we got here then we didn't find it
                        // give up here
                        frontReplacement = string.Empty;
                        backReplacement = string.Empty;
                        return false;
                    }
                default:
                    frontReplacement = string.Empty;
                    backReplacement = string.Empty;
                    return false;
            }

            var conversion = ConvertMarkupIntoTextAnimatorTag(attribute, opening, closing);
            if (!string.IsNullOrWhiteSpace(conversion.Item1) && !string.IsNullOrWhiteSpace(conversion.Item2))
            {
                frontReplacement = conversion.Item1;
                backReplacement = conversion.Item2;
                return true;
            }
            else
            {
                frontReplacement = string.Empty;
                backReplacement = string.Empty;
                return false;
            }
        }

        private static (string?, string?) ConvertMarkupIntoTextAnimatorTag(MarkupAttribute attribute, char openingSymbol, char closingSymbol)
        {
            System.Text.StringBuilder stringBuilder = new();
            stringBuilder.Append(openingSymbol);

            string closing = $"{openingSymbol}/{attribute.Name}{closingSymbol}";

            // ok so if it is a disappearance then we need to special case that right now
            // because we want {#tag} and not {tag} in that case
            if (attribute.TryGetProperty(disappearanceAttributeKey, out bool disappear))
            {
                if (disappear)
                {
                    stringBuilder.Append(DisappearanceSymbol);
                    closing = $"{openingSymbol}/{DisappearanceSymbol}{attribute.Name}{closingSymbol}";
                }
            }
            stringBuilder.Append(attribute.Name);

            // ok so this is gonna be a bit jank
            // because if we have a self-property we NEED to handle that first
            // then otherwise we can do the rest
            if (attribute.Properties.Count > 0)
            {
                if (attribute.Properties.TryGetValue(attribute.Name, out var value))
                {
                    switch (value.Type)
                    {
                        case MarkupValueType.Integer:
                            stringBuilder.Append("=");
                            stringBuilder.Append(value.IntegerValue);
                            break;
                        case MarkupValueType.Float:
                            stringBuilder.Append("=");
                            stringBuilder.Append(value.FloatValue);
                            break;
                        case MarkupValueType.String:
                            stringBuilder.Append("=");
                            stringBuilder.Append(value.StringValue);
                            break;
                        case MarkupValueType.Bool:
                            stringBuilder.Append("=");
                            stringBuilder.Append(value.BoolValue ? "1" : "0");
                            break;
                    }
                }

                // ok now we need to do the next bit which is the properties
                foreach (var property in attribute.Properties)
                {
                    // there are four properties we ignore, self, type, whitespace, and disapperance
                    // all of these are already handled or not relevant to us
                    if (property.Key == attribute.Name)
                    {
                        continue;
                    }
                    if (property.Key == trimAttributeKey)
                    {
                        continue;
                    }
                    if (property.Key == disappearanceAttributeKey)
                    {
                        continue;
                    }
                    if (property.Key == typeAttributeKey)
                    {
                        continue;
                    }

                    switch (property.Value.Type)
                    {
                        case MarkupValueType.Integer:
                            stringBuilder.Append(" ");
                            stringBuilder.Append(property.Key);
                            stringBuilder.Append("=");
                            stringBuilder.Append(property.Value.IntegerValue);
                            break;
                        case MarkupValueType.Float:
                            stringBuilder.Append(" ");
                            stringBuilder.Append(property.Key);
                            stringBuilder.Append("=");
                            stringBuilder.Append(property.Value.FloatValue);
                            break;
                        case MarkupValueType.String:
                            stringBuilder.Append(" ");
                            stringBuilder.Append(property.Key);
                            stringBuilder.Append("=");
                            stringBuilder.Append(property.Value.StringValue);
                            break;
                        case MarkupValueType.Bool:
                            stringBuilder.Append(" ");
                            stringBuilder.Append(property.Key);
                            stringBuilder.Append("=");
                            stringBuilder.Append(property.Value.BoolValue ? "1" : "0");
                            break;
                    }
                }
            }
            var front = stringBuilder.Append(closingSymbol).ToString();
            return (front, closing);
        }
#endif
    }
}