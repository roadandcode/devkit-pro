using System;
using System.Collections.Generic;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>One addressable entry, as plain data.</summary>
    public sealed class EntryRecord
    {
        private static readonly string[] Nothing = new string[0];

        public EntryRecord(string guid, string address, string assetPath, IReadOnlyList<string> labels = null, long sizeBytes = 0,
            IReadOnlyList<string> dependencies = null, bool assetExists = true, bool isScene = false, bool isFolder = false)
        {
            Guid = guid;
            Address = address ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Labels = labels ?? Nothing;
            SizeBytes = sizeBytes;
            Dependencies = dependencies ?? Nothing;
            AssetExists = assetExists;
            IsScene = isScene;
            IsFolder = isFolder;
        }

        public string Guid { get; }

        public string Address { get; }

        public string AssetPath { get; }

        public IReadOnlyList<string> Labels { get; }

        /// <summary>Size of the source file on disk, or of everything in the folder.</summary>
        public long SizeBytes { get; }

        /// <summary>Paths of every asset this entry needs, itself left out.</summary>
        public IReadOnlyList<string> Dependencies { get; }

        public bool AssetExists { get; }

        public bool IsScene { get; }

        /// <summary>A folder entry makes everything under it addressable.</summary>
        public bool IsFolder { get; }
    }

    public sealed class GroupRecord
    {
        public GroupRecord(string name, IReadOnlyList<EntryRecord> entries)
        {
            Name = name;
            Entries = entries ?? new EntryRecord[0];
        }

        public string Name { get; }

        public IReadOnlyList<EntryRecord> Entries { get; }
    }

    /// <summary>A bundle file left by the last Addressables build.</summary>
    public sealed class BundleRecord
    {
        public BundleRecord(string path, long sizeBytes)
        {
            Path = path;
            SizeBytes = sizeBytes;
        }

        public string Path { get; }

        public long SizeBytes { get; }
    }

    /// <summary>Everything the audit looks at, read out of the project once.</summary>
    public sealed class AddressablesSnapshot
    {
        public AddressablesSnapshot(IReadOnlyList<GroupRecord> groups, IReadOnlyList<BundleRecord> bundles, ReferenceIndex references,
            IReadOnlyCollection<string> buildScenes)
        {
            Groups = groups ?? new GroupRecord[0];
            Bundles = bundles ?? new BundleRecord[0];
            References = references ?? new ReferenceIndex();
            BuildScenes = new HashSet<string>(buildScenes ?? new string[0], StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<GroupRecord> Groups { get; }

        public IReadOnlyList<BundleRecord> Bundles { get; }

        public ReferenceIndex References { get; }

        /// <summary>Scenes enabled in Build Settings.</summary>
        public ISet<string> BuildScenes { get; }
    }

    public sealed class AuditOptions
    {
        public const float DefaultMaxBundleMegabytes = 10f;

        public float MaxBundleMegabytes { get; set; } = DefaultMaxBundleMegabytes;

        /// <summary>Whether assets that live in packages count when looking for duplicated dependencies.</summary>
        public bool IncludePackageAssets { get; set; }

        public long MaxBundleBytes => (long)(MaxBundleMegabytes * 1024f * 1024f);
    }

    /// <summary>Reads the Addressables settings. Implemented in the assembly that is only compiled when Addressables is installed.</summary>
    public interface IAddressablesReader
    {
        /// <returns>False, with the reason, when the project has no Addressables settings to read.</returns>
        bool TryRead(AuditOptions options, Common.IProgressSink progress, out AddressablesSnapshot snapshot, out string problem);
    }
}
