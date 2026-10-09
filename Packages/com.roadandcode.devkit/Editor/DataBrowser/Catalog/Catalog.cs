using System;
using System.Collections.Generic;

namespace RoadAndCode.DevKit.DataBrowser
{
    /// <summary>One ScriptableObject in the project, as plain data.</summary>
    public sealed class AssetRecord
    {
        public const string MissingScriptType = "(missing script)";

        public AssetRecord(string guid, string path, string name, string typeName, string typeFullName, bool isProjectType,
            bool isSubAsset = false)
        {
            Guid = guid;
            Path = path;
            Name = name;
            TypeName = typeName;
            TypeFullName = typeFullName;
            IsProjectType = isProjectType;
            IsSubAsset = isSubAsset;
        }

        public string Guid { get; }

        public string Path { get; }

        public string Name { get; }

        public string TypeName { get; }

        /// <summary>Namespace and name. Two types with the same short name stay apart.</summary>
        public string TypeFullName { get; }

        /// <summary>The type is declared in the project, not in Unity or an installed package.</summary>
        public bool IsProjectType { get; }

        /// <summary>Lives inside another asset's file.</summary>
        public bool IsSubAsset { get; }

        public bool IsMissingScript => TypeFullName == MissingScriptType;

        public string Folder
        {
            get
            {
                int slash = Path.LastIndexOf('/');
                return slash < 0 ? string.Empty : Path.Substring(0, slash);
            }
        }

        /// <summary>An asset file whose script cannot be found. It counts as the project's own, so it is never hidden.</summary>
        public static AssetRecord MissingScript(string guid, string path, string name)
        {
            return new AssetRecord(guid, path, name, MissingScriptType, MissingScriptType, true);
        }
    }

    /// <summary>A type and how many assets of it there are.</summary>
    public sealed class TypeEntry
    {
        public TypeEntry(string typeName, string typeFullName, int count)
        {
            TypeName = typeName;
            TypeFullName = typeFullName;
            Count = count;
        }

        public string TypeName { get; }

        public string TypeFullName { get; }

        public int Count { get; }
    }

    /// <summary>What the browser is asked to show.</summary>
    public sealed class CatalogQuery
    {
        /// <summary>Words that must all appear in the name or path. A word written <c>t:Name</c> must appear in the type instead.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Only this type, or every type when null.</summary>
        public string TypeFullName { get; set; }

        public bool ProjectTypesOnly { get; set; } = true;
    }

    /// <summary>Every ScriptableObject that was found, with the grouping and searching the browser needs.</summary>
    public sealed class Catalog
    {
        private const string TypePrefix = "t:";
        private static readonly char[] Spaces = { ' ' };

        public Catalog(IReadOnlyList<AssetRecord> records)
        {
            Records = records ?? new AssetRecord[0];
        }

        public IReadOnlyList<AssetRecord> Records { get; }

        public int Count(bool projectTypesOnly)
        {
            int count = 0;
            for (int i = 0; i < Records.Count; i++)
            {
                if (!projectTypesOnly || Records[i].IsProjectType) count++;
            }

            return count;
        }

        /// <summary>The types present, by name, each with its number of assets.</summary>
        public List<TypeEntry> Types(bool projectTypesOnly)
        {
            var counts = new Dictionary<string, int>();
            var names = new Dictionary<string, string>();
            for (int i = 0; i < Records.Count; i++)
            {
                AssetRecord record = Records[i];
                if (projectTypesOnly && !record.IsProjectType) continue;

                counts.TryGetValue(record.TypeFullName, out int count);
                counts[record.TypeFullName] = count + 1;
                names[record.TypeFullName] = record.TypeName;
            }

            var types = new List<TypeEntry>(counts.Count);
            foreach (KeyValuePair<string, int> pair in counts) types.Add(new TypeEntry(names[pair.Key], pair.Key, pair.Value));
            types.Sort((a, b) =>
            {
                int order = string.Compare(a.TypeName, b.TypeName, StringComparison.OrdinalIgnoreCase);
                return order != 0 ? order : string.CompareOrdinal(a.TypeFullName, b.TypeFullName);
            });
            return types;
        }

        public List<AssetRecord> Find(CatalogQuery query)
        {
            string[] terms = (query.Text ?? string.Empty).Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
            var found = new List<AssetRecord>();
            for (int i = 0; i < Records.Count; i++)
            {
                AssetRecord record = Records[i];
                if (query.ProjectTypesOnly && !record.IsProjectType) continue;
                if (query.TypeFullName != null && record.TypeFullName != query.TypeFullName) continue;
                if (MatchesAll(record, terms)) found.Add(record);
            }

            found.Sort((a, b) =>
            {
                int order = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return order != 0 ? order : string.CompareOrdinal(a.Path, b.Path);
            });
            return found;
        }

        private static bool MatchesAll(AssetRecord record, string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                string term = terms[i];
                bool matches = term.StartsWith(TypePrefix, StringComparison.OrdinalIgnoreCase)
                    ? Has(record.TypeFullName, term.Substring(TypePrefix.Length))
                    : Has(record.Name, term) || Has(record.Path, term);
                if (!matches) return false;
            }

            return true;
        }

        private static bool Has(string text, string term) => text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
