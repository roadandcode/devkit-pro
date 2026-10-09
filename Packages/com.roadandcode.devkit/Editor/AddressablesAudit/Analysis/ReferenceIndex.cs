using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>
    /// What the project's scenes, prefabs, assets and scripts say they load: GUIDs held by
    /// <c>AssetReference</c> fields, and names (addresses or labels) that appear as text.
    /// </summary>
    public sealed class ReferenceIndex
    {
        // How an AssetReference and an AssetLabelReference serialize.
        private static readonly Regex AssetGuid = new Regex(@"m_AssetGUID:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
        private static readonly Regex LabelString = new Regex(@"m_LabelString:\s*(.+)", RegexOptions.Compiled);
        private static readonly Regex StringLiteral = new Regex("\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\"", RegexOptions.Compiled);
        private static readonly char[] LineBreaks = { '\n' };
        private static readonly char[] Quotes = { '"', '\'' };

        private readonly HashSet<string> _guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);

        public bool HasGuid(string guid) => _guids.Contains(guid);

        public bool HasName(string name) => _names.Contains(name);

        /// <summary>A folder entry is named by anything that addresses something inside it.</summary>
        public bool HasNameUnder(string folderAddress)
        {
            string prefix = folderAddress + "/";
            foreach (string name in _names)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>
        /// Reads a text-serialized scene, prefab or asset. Besides reference fields it picks up any
        /// plain string value that is one of the <paramref name="knownNames"/>, which is how an
        /// address kept in a string field is found.
        /// </summary>
        public void AddSerialized(string yaml, ISet<string> knownNames)
        {
            foreach (Match match in AssetGuid.Matches(yaml)) _guids.Add(match.Groups[1].Value);
            foreach (Match match in LabelString.Matches(yaml)) _names.Add(Clean(match.Groups[1].Value));
            if (knownNames == null || knownNames.Count == 0) return;

            foreach (string line in yaml.Split(LineBreaks))
            {
                string value = ValueOf(line);
                if (value.Length > 0 && knownNames.Contains(value)) _names.Add(value);
            }
        }

        /// <summary>Reads a script. Every string literal counts as a name the code might load.</summary>
        public void AddCode(string source)
        {
            foreach (Match match in StringLiteral.Matches(source))
            {
                string literal = match.Groups[1].Value;
                if (literal.Length > 0) _names.Add(literal);
            }
        }

        // "  key: value" or "  - value", with quotes dropped.
        private static string ValueOf(string line)
        {
            string trimmed = line.Trim();
            int colon = trimmed.IndexOf(": ", StringComparison.Ordinal);
            if (colon >= 0) return Clean(trimmed.Substring(colon + 2));
            return trimmed.StartsWith("- ", StringComparison.Ordinal) ? Clean(trimmed.Substring(2)) : string.Empty;
        }

        private static string Clean(string value) => value.Trim().Trim(Quotes);
    }
}
