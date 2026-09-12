/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

namespace Yarn.Unity.Addons.TextAnimatorIntegration
{
#nullable enable
    using Yarn.Markup;
    using System.Collections.Generic;
#if YS_USE_TEXT_ANIMATOR_2
    using Febucci.UI;
#endif

    public partial class TextAnimatorMarkupManager
    {
#if YS_USE_TEXT_ANIMATOR_2
        public static char OpeningActionsBracket => TextAnimatorSettings.Instance.actions.openingSymbol;
        public static char ClosingActionsBracket => TextAnimatorSettings.Instance.actions.closingSymbol;
        public static char OpeningAppearancesBracket => TextAnimatorSettings.Instance.appearances.openingSymbol;
        public static char ClosingAppearancesBracket => TextAnimatorSettings.Instance.appearances.closingSymbol;
        public static char DisappearanceSymbol => TextAnimatorSettings.Instance.disappearancesMiddleSymbol;
        public static char OpeningBehavioursBracket => TextAnimatorSettings.Instance.behaviors.openingSymbol;
        public static char ClosingBehavioursBracket => TextAnimatorSettings.Instance.behaviors.closingSymbol;

        public static bool IsTextAnimatorTag(string tagID)
        {
            if (tagID == "notype")
            {
                return true;
            }
            if (TextAnimatorSettings.Instance.actions.defaultDatabase.ContainsKey(tagID))
            {
                return true;
            }
            if (TextAnimatorSettings.Instance.behaviors.defaultDatabase.ContainsKey(tagID))
            {
                return true;
            }
            if (TextAnimatorSettings.Instance.appearances.defaultDatabase.ContainsKey(tagID))
            {
                return true;
            }

            return false;
        }

        public static HashSet<string> AllTags()
        {
            HashSet<string> tags = new();

            foreach (var action in TextAnimatorSettings.Instance.actions.defaultDatabase.Data)
            {
                if (action == null)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(action.TagID))
                {
                    continue;
                }
                tags.Add(action.TagID);
            }
            foreach (var behaviors in TextAnimatorSettings.Instance.behaviors.defaultDatabase.Data)
            {
                if (behaviors == null)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(behaviors.TagID))
                {
                    continue;
                }
                tags.Add(behaviors.TagID);
            }
            foreach (var appearance in TextAnimatorSettings.Instance.appearances.defaultDatabase.Data)
            {
                if (appearance == null)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(appearance.TagID))
                {
                    continue;
                }
                tags.Add(appearance.TagID);
            }
            tags.Add("notype");

            return tags;
        }

        private static bool IsBehaviourTag(string tagID)
        {
            return TextAnimatorSettings.Instance.behaviors.defaultDatabase.ContainsKey(tagID);
        }
        private static bool IsActionTag(string tagID)
        {
            return TextAnimatorSettings.Instance.actions.defaultDatabase.ContainsKey(tagID);
        }
        private static bool IsAppearanceTag(string tagID)
        {
            return TextAnimatorSettings.Instance.appearances.defaultDatabase.ContainsKey(tagID);
        }

        private static Dictionary<string, (char start, char end)> cachedAttributeTypes = new()
        {
            {"notype", ('<', '>') },
        };
#endif
    }
}