/*
Text Animator for Yarn Spinner is licensed to you under the terms found in the file LICENSE.md.
*/

namespace Yarn.Unity.Addons.TextAnimatorIntegration
{
#nullable enable
    using Yarn.Markup;
    using System.Collections.Generic;
#if YS_USE_TEXT_ANIMATOR_3
    using Febucci.TextAnimatorForUnity;
#endif

    public partial class TextAnimatorMarkupManager
    {
#if YS_USE_TEXT_ANIMATOR_3
        public static char OpeningActionsBracket => '<';
        public static char ClosingActionsBracket => '>';
        public static char OpeningAppearancesBracket => TextAnimatorSettings.Instance.Settings.parsingAppearances.openingBracket;
        public static char ClosingAppearancesBracket => TextAnimatorSettings.Instance.Settings.parsingAppearances.closingBracket;
        public static char DisappearanceSymbol => TextAnimatorSettings.Instance.Settings.parsingDisappearances.middleSymbol;
        public static char OpeningBehavioursBracket => TextAnimatorSettings.Instance.Settings.parsingBehaviors.openingBracket;
        public static char ClosingBehavioursBracket => TextAnimatorSettings.Instance.Settings.parsingBehaviors.closingBracket;

        public static bool IsTextAnimatorTag(string tagID)
        {
            if (tagID == "notype")
            {
                return true;
            }
            if (TextAnimatorSettings.Instance.Settings.GlobalActionsDatabase.Database.ContainsKey(tagID))
            {
                return true;
            }
            if (TextAnimatorSettings.Instance.Settings.GlobalEffectsDatabase.Database.ContainsKey(tagID))
            {
                return true;
            }

            return false;
        }

        public static HashSet<string> AllTags()
        {
            HashSet<string> tags = new();

            tags.UnionWith(TextAnimatorSettings.Instance.Settings.GlobalActionsDatabase.Database.Keys);
            tags.UnionWith(TextAnimatorSettings.Instance.Settings.GlobalEffectsDatabase.Database.Keys);
            tags.Add("notype");

            return tags;
        }

        private static bool IsBehaviourTag(string tagID)
        {
            return TextAnimatorSettings.Instance.Settings.GlobalEffectsDatabase.Database.ContainsKey(tagID);
        }
        private static bool IsActionTag(string tagID)
        {
            return TextAnimatorSettings.Instance.Settings.GlobalActionsDatabase.Database.ContainsKey(tagID);
        }
        private static bool IsAppearanceTag(string tagID)
        {
            return TextAnimatorSettings.Instance.Settings.GlobalEffectsDatabase.Database.ContainsKey(tagID);
        }

        private static Dictionary<string, (char start, char end)> cachedAttributeTypes = new()
        {
            {"notype", ('<', '>') },
        };
#endif
    }
}