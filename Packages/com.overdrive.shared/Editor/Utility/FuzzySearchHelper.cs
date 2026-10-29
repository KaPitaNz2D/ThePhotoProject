using System;
using System.Collections.Generic;

namespace Overdrive.Framework
{
    /// <summary>
    /// A simple fuzzy search helper that returns true if all characters in the pattern
    /// appear in order in the candidate string.
    /// </summary>
    public static class FuzzySearchHelper
    {
        // Returns true if candidate contains any substring of word's length with <= 1 edit distance
        private static bool SubstringMatchWithTolerance(string word, string candidate, int maxDistance, out int bestIndex, out int bestDistance)
        {
            bestIndex = -1;
            bestDistance = int.MaxValue;
            for (int i = 0; i <= candidate.Length - word.Length; i++)
            {
                string sub = candidate.Substring(i, word.Length);
                int dist = LevenshteinDistance(word, sub);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestIndex = i;
                }
                if (dist <= maxDistance)
                    return true;
            }
            return false;
        }

        // Levenshtein distance for two strings of equal length
        private static int LevenshteinDistance(string a, string b)
        {
            int distance = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    distance++;
            }
            return distance;
        }

        /// <summary>
        /// Returns a breakdown of the fuzzy match score calculation for debugging/testing.
        /// </summary>
        public static string FuzzyMatchScoreBreakdown(string pattern, string candidate)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return "Empty pattern: score = int.MaxValue";
            pattern = pattern.ToLower();
            candidate = candidate.ToLower();
            var words = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int score = 0;
            var breakdown = new List<string>();
            int prevEndIndex = -1;
            // Find the final section (after last separator)
            string[] sections = candidate.Split(new[] { '>', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
            string finalSection = sections.Length > 0 ? sections[sections.Length - 1].Trim() : candidate;
            var finalWords = finalSection.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int w = 0; w < words.Length; w++)
            {
                var word = words[w];
                int substringIndex = candidate.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                int matchIndex = -1, matchLength = word.Length, matchDistance = 0;
                bool matched = false;//, typo = false;
                if (substringIndex >= 0)
                {
                    score += word.Length;
                    breakdown.Add($"+{word.Length}: candidate contains word '{word}' as substring");
                    matchIndex = substringIndex;
                    matched = true;
                    if (substringIndex == 0 || (substringIndex > 0 && !char.IsLetter(candidate[substringIndex - 1])))
                    {
                        score += 1;
                        breakdown.Add($"+1: '{word}' matched at start of word");
                    }
                }
                else
                {
                    int bestIndex, bestDistance;
                    if (SubstringMatchWithTolerance(word, candidate, 2, out bestIndex, out bestDistance))
                    {
                        score += word.Length - bestDistance;
                        breakdown.Add($"+{word.Length - bestDistance}: '{word}' matched with {bestDistance} typo(s) at '{candidate.Substring(bestIndex, word.Length)}'");
                        matchIndex = bestIndex;
                        matchDistance = bestDistance;
                        matched = true;
                        //typo = true;
                        if (bestIndex == 0 || (bestIndex > 0 && !char.IsLetter(candidate[bestIndex - 1])))
                        {
                            score += 1;
                            breakdown.Add($"+1: '{word}' matched at start of word (with typo)");
                        }
                    }
                }
                if (!matched)
                {
                    breakdown.Add($"0: word '{word}' not matched as substring or with tolerance");
                    return string.Join(" | ", breakdown) + $" | FINAL SCORE: 0";
                }
                // Follow bonus: if previous word ended and this word starts after only non-letter chars
                if (w > 0 && prevEndIndex >= 0 && matchIndex >= 0 && matchIndex > prevEndIndex)
                {
                    bool onlyNonLetters = true;
                    for (int i = prevEndIndex; i < matchIndex; i++)
                    {
                        if (char.IsLetter(candidate[i]))
                        {
                            onlyNonLetters = false;
                            break;
                        }
                    }
                    if (onlyNonLetters)
                    {
                        score += 1;
                        breakdown.Add($"+1: '{words[w-1]}' followed immediately by '{word}' (follow bonus)");
                    }
                }
                prevEndIndex = matchIndex + matchLength;
                // True action name bonus: if word matches a whole word in final section
                foreach (var fw in finalWords)
                {
                    if (fw.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 1;
                        breakdown.Add($"+1: '{word}' matches word '{fw}' in final section (true action name bonus)");
                        break;
                    }
                }
            }
            breakdown.Add($"FINAL SCORE: {score}");
            return string.Join(" | ", breakdown);
        }
        /// <summary>
        /// Returns a relevance score for fuzzy matching a pattern against a candidate.
        /// Higher score means more relevant.
        /// Factors: number of matched words, total matched chars, earliest match position.
        /// </summary>
        public static int FuzzyMatchScore(string pattern, string candidate)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return int.MaxValue; // Empty pattern matches everything
            pattern = pattern.ToLower();
            candidate = candidate.ToLower();
            var words = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int score = 0;
            int prevEndIndex = -1;
            // Find the final section (after last separator)
            string[] sections = candidate.Split(new[] { '>', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
            string finalSection = sections.Length > 0 ? sections[sections.Length - 1].Trim() : candidate;
            var finalWords = finalSection.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int w = 0; w < words.Length; w++)
            {
                var word = words[w];
                int substringIndex = candidate.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                int matchIndex = -1, matchLength = word.Length;
                bool matched = false;
                if (substringIndex >= 0)
                {
                    score += word.Length;
                    matchIndex = substringIndex;
                    matched = true;
                    if (substringIndex == 0 || (substringIndex > 0 && !char.IsLetter(candidate[substringIndex - 1])))
                        score += 1;
                }
                else
                {
                    int bestIndex, bestDistance;
                    if (SubstringMatchWithTolerance(word, candidate, 2, out bestIndex, out bestDistance))
                    {
                        score += word.Length - bestDistance;
                        matchIndex = bestIndex;
                        matched = true;
                        if (bestIndex == 0 || (bestIndex > 0 && !char.IsLetter(candidate[bestIndex - 1])))
                            score += 1;
                    }
                }
                if (!matched)
                    return 0;
                // Follow bonus: if previous word ended and this word starts after only non-letter chars
                if (w > 0 && prevEndIndex >= 0 && matchIndex >= 0 && matchIndex > prevEndIndex)
                {
                    bool onlyNonLetters = true;
                    for (int i = prevEndIndex; i < matchIndex; i++)
                    {
                        if (char.IsLetter(candidate[i]))
                        {
                            onlyNonLetters = false;
                            break;
                        }
                    }
                    if (onlyNonLetters)
                        score += 1;
                }
                prevEndIndex = matchIndex + matchLength;
                // True action name bonus: if word matches a whole word in final section
                foreach (var fw in finalWords)
                {
                    if (fw.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 1;
                        break;
                    }
                }
            }
            return score;
        }

        public static bool FuzzyMatch(string pattern, string candidate)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return true;
            pattern = pattern.ToLower();
            candidate = candidate.ToLower();
            var words = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                int bestIndex, bestDistance;
                if (candidate.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (!SubstringMatchWithTolerance(word, candidate, 2, out bestIndex, out bestDistance))
                    return false;
            }
            return true;
        }
    }
}
