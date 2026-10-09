using System;
using System.Collections.Generic;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>
    /// A comma-separated list of field names, with <c>*</c> standing for any run of characters.
    /// Used to say which reference fields are allowed to be empty.
    /// </summary>
    public sealed class NamePatterns
    {
        private static readonly char[] Separators = { ',', ';' };

        private readonly List<string> _patterns = new List<string>();

        public NamePatterns(string list)
        {
            foreach (string part in (list ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                string pattern = part.Trim();
                if (pattern.Length > 0) _patterns.Add(pattern);
            }
        }

        public bool Matches(string name)
        {
            for (int i = 0; i < _patterns.Count; i++)
            {
                if (Matches(_patterns[i], 0, name, 0)) return true;
            }

            return false;
        }

        private static bool Matches(string pattern, int p, string name, int n)
        {
            while (p < pattern.Length)
            {
                if (pattern[p] == '*')
                {
                    // A star takes as many characters as it needs for the rest to fit.
                    for (int skip = n; skip <= name.Length; skip++)
                    {
                        if (Matches(pattern, p + 1, name, skip)) return true;
                    }

                    return false;
                }

                if (n >= name.Length || char.ToLowerInvariant(pattern[p]) != char.ToLowerInvariant(name[n])) return false;
                p++;
                n++;
            }

            return n == name.Length;
        }
    }
}
