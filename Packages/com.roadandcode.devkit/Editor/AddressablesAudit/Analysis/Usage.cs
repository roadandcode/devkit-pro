using System;
using System.Collections.Generic;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>
    /// Works out which entries something in the project can reach: those named directly by a
    /// reference, an address or a label, and those such an entry depends on.
    /// </summary>
    public sealed class Usage
    {
        private readonly HashSet<EntryRecord> _used = new HashSet<EntryRecord>();
        private readonly Dictionary<string, EntryRecord> _byPath = new Dictionary<string, EntryRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly List<EntryRecord> _folders = new List<EntryRecord>();

        public Usage(AddressablesSnapshot snapshot)
        {
            var pending = new Queue<EntryRecord>();
            foreach (GroupRecord group in snapshot.Groups)
            {
                foreach (EntryRecord entry in group.Entries)
                {
                    if (entry.AssetPath.Length > 0) _byPath[entry.AssetPath] = entry;
                    if (entry.IsFolder) _folders.Add(entry);
                    if (IsNamed(entry, snapshot.References) && _used.Add(entry)) pending.Enqueue(entry);
                }
            }

            // Loading an entry loads what it depends on, so those are in use too.
            while (pending.Count > 0)
            {
                foreach (string dependency in pending.Dequeue().Dependencies)
                {
                    EntryRecord owner = EntryFor(dependency);
                    if (owner != null && _used.Add(owner)) pending.Enqueue(owner);
                }
            }
        }

        public bool IsUsed(EntryRecord entry) => _used.Contains(entry);

        /// <summary>The entry that makes this asset addressable: its own, or a folder entry above it.</summary>
        public EntryRecord EntryFor(string assetPath)
        {
            if (_byPath.TryGetValue(assetPath, out EntryRecord entry)) return entry;

            foreach (EntryRecord folder in _folders)
            {
                if (assetPath.StartsWith(folder.AssetPath + "/", StringComparison.OrdinalIgnoreCase)) return folder;
            }

            return null;
        }

        public int UsedIn(GroupRecord group)
        {
            int used = 0;
            foreach (EntryRecord entry in group.Entries)
            {
                if (IsUsed(entry)) used++;
            }

            return used;
        }

        private static bool IsNamed(EntryRecord entry, ReferenceIndex references)
        {
            if (references.HasGuid(entry.Guid) || references.HasName(entry.Address)) return true;
            if (entry.IsFolder && references.HasNameUnder(entry.Address)) return true;

            foreach (string label in entry.Labels)
            {
                if (references.HasName(label)) return true;
            }

            return false;
        }
    }

    /// <summary>A group at a glance, for the window's overview.</summary>
    public sealed class GroupSummary
    {
        public GroupSummary(string name, int entries, int usedEntries, long sourceBytes)
        {
            Name = name;
            Entries = entries;
            UsedEntries = usedEntries;
            SourceBytes = sourceBytes;
        }

        public string Name { get; }

        public int Entries { get; }

        public int UsedEntries { get; }

        /// <summary>Total size of the entries' source files. An estimate of weight, not a bundle size.</summary>
        public long SourceBytes { get; }
    }

    public static class Sizes
    {
        private const float Megabyte = 1024f * 1024f;

        public static string Megabytes(long bytes) => (bytes / Megabyte).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " MB";
    }
}
