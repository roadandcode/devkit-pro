using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>One check over the whole Addressables setup.</summary>
    public interface IAuditRule
    {
        string Id { get; }

        void Check(AddressablesSnapshot snapshot, Usage usage, AuditOptions options, List<Finding> findings);
    }

    public sealed class EmptyGroupRule : IAuditRule
    {
        public const string RuleId = "empty-group";

        public string Id => RuleId;

        public void Check(AddressablesSnapshot snapshot, Usage usage, AuditOptions options, List<Finding> findings)
        {
            foreach (GroupRecord group in snapshot.Groups)
            {
                if (group.Entries.Count == 0) findings.Add(new Finding(RuleId, Severity.Warning, $"Group '{group.Name}' has no entries", objectPath: group.Name));
            }
        }
    }

    /// <summary>
    /// An AssetReference whose target is not addressable: the asset was deleted, taken out of its
    /// group, or never added. Nothing in the editor complains and the load fails at run time.
    /// </summary>
    public sealed class DanglingReferenceRule : IAuditRule
    {
        public const string RuleId = "dangling-reference";

        public string Id => RuleId;

        public void Check(AddressablesSnapshot snapshot, Usage usage, AuditOptions options, List<Finding> findings)
        {
            foreach (KeyValuePair<string, List<string>> reference in snapshot.References.Guids)
            {
                if (usage.HasEntry(reference.Key)) continue;

                string target = snapshot.ReferencedAssetPath(reference.Key);
                if (target == null) continue;

                // Inside an addressable folder is addressable.
                if (target.Length > 0 && usage.EntryFor(target) != null) continue;

                string message = target.Length == 0
                    ? "An AssetReference points at an asset that no longer exists"
                    : $"An AssetReference points at {target}, which is not addressable";
                foreach (string source in reference.Value) findings.Add(new Finding(RuleId, Severity.Error, message, source, detail: reference.Key));
            }
        }
    }

    /// <summary>
    /// An entry whose asset is gone. The Addressables package drops such an entry by itself the next
    /// time it imports the group or builds, without failing anything, so this only catches the state
    /// in between: a file deleted outside the editor that the editor has not noticed yet.
    /// </summary>
    public sealed class MissingAssetRule : IAuditRule
    {
        public const string RuleId = "missing-asset";

        public string Id => RuleId;

        public void Check(AddressablesSnapshot snapshot, Usage usage, AuditOptions options, List<Finding> findings)
        {
            foreach (GroupRecord group in snapshot.Groups)
            {
                foreach (EntryRecord entry in group.Entries)
                {
                    if (entry.AssetExists) continue;
                    findings.Add(new Finding(RuleId, Severity.Error, $"Entry '{entry.Address}' points at an asset that no longer exists",
                        entry.AssetPath, group.Name, entry.Guid));
                }
            }
        }
    }

    /// <summary>Two entries answering to one address. Loading by that address returns whichever comes first.</summary>
    public sealed class DuplicateAddressRule : IAuditRule
    {
        public const string RuleId = "duplicate-address";

        public string Id => RuleId;

        public void Check(AddressablesSnapshot snapshot, Usage usage, AuditOptions options, List<Finding> findings)
        {
            var byAddress = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (GroupRecord group in snapshot.Groups)
            {
                foreach (EntryRecord entry in group.Entries)
                {
                    if (!byAddress.TryGetValue(entry.Address, out List<string> owners))
                    {
                        owners = new List<string>();
                        byAddress[entry.Address] = owners;
                        order.Add(entry.Address);
                    }

                    owners.Add($"{entry.AssetPath} ({group.Name})");
                }
            }

            foreach (string address in order)
            {
                List<string> owners = byAddress[address];
                if (owners.Count < 2) continue;
                findings.Add(new Finding(RuleId, Severity.Warning, $"Address '{address}' is used by {owners.Count} entries",
                    detail: string.Join(", ", owners)));
            }
        }
    }

    /// <summary>
    /// Entries nothing asks for. A group where that is true of every entry is built, shipped and never loaded.
    /// </summary>
    public sealed class UnreferencedRule : IAuditRule
    {
        public const string GroupRuleId = "unreferenced-group";
        public const string EntryRuleId = "unreferenced-entry";

        public string Id => GroupRuleId;

        public void Check(AddressablesSnapshot snapshot, Usage usage, AuditOptions options, List<Finding> findings)
        {
            foreach (GroupRecord group in snapshot.Groups)
            {
                if (group.Entries.Count == 0) continue;

                if (usage.UsedIn(group) == 0)
                {
                    findings.Add(new Finding(GroupRuleId, Severity.Warning,
                        $"Nothing references group '{group.Name}': none of its {group.Entries.Count} entries is named by an AssetReference, an address or a label",
                        objectPath: group.Name));
                    continue;
                }

                foreach (EntryRecord entry in group.Entries)
                {
                    // An entry whose asset is gone is already an error; saying it is unused as well adds nothing.
                    if (usage.IsUsed(entry) || !entry.AssetExists) continue;
                    findings.Add(new Finding(EntryRuleId, Severity.Info, $"Nothing references entry '{entry.Address}'", entry.AssetPath, group.Name));
                }
            }
        }
    }

    /// <summary>
    /// An asset that is not addressable itself but is needed by entries in more than one group.
    /// Each of those groups' bundles gets its own copy.
    /// </summary>
    public sealed class DuplicatedDependencyRule : IAuditRule
    {
        public const string RuleId = "duplicated-dependency";

        public string Id => RuleId;

        public void Check(AddressablesSnapshot snapshot, Usage usage, AuditOptions options, List<Finding> findings)
        {
            var groupsByDependency = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (GroupRecord group in snapshot.Groups)
            {
                foreach (EntryRecord entry in group.Entries)
                {
                    foreach (string dependency in entry.Dependencies)
                    {
                        if (usage.EntryFor(dependency) != null) continue;

                        if (!groupsByDependency.TryGetValue(dependency, out List<string> groups))
                        {
                            groups = new List<string>();
                            groupsByDependency[dependency] = groups;
                        }

                        if (!groups.Contains(group.Name)) groups.Add(group.Name);
                    }
                }
            }

            foreach (KeyValuePair<string, List<string>> pair in groupsByDependency)
            {
                if (pair.Value.Count < 2) continue;
                findings.Add(new Finding(RuleId, Severity.Warning,
                    $"Not addressable itself but needed by {pair.Value.Count} groups, so each bundle gets its own copy",
                    pair.Key, detail: string.Join(", ", pair.Value)));
            }
        }
    }

    /// <summary>
    /// Bundles over the size limit. Uses the bundles from the last build when there are any, and
    /// otherwise the size of each group's source files as a rough stand-in.
    /// </summary>
    public sealed class OversizedBundleRule : IAuditRule
    {
        public const string BundleRuleId = "oversized-bundle";
        public const string EstimateRuleId = "large-group";

        public string Id => BundleRuleId;

        public void Check(AddressablesSnapshot snapshot, Usage usage, AuditOptions options, List<Finding> findings)
        {
            string limit = Sizes.Megabytes(options.MaxBundleBytes);
            foreach (BundleRecord bundle in snapshot.Bundles)
            {
                if (bundle.SizeBytes <= options.MaxBundleBytes) continue;
                findings.Add(new Finding(BundleRuleId, Severity.Warning, $"Bundle is {Sizes.Megabytes(bundle.SizeBytes)}, over the {limit} limit", bundle.Path));
            }

            if (snapshot.Bundles.Count > 0) return;

            foreach (GroupRecord group in snapshot.Groups)
            {
                long bytes = usage.SourceBytes(group);
                if (bytes <= options.MaxBundleBytes) continue;

                findings.Add(new Finding(EstimateRuleId, Severity.Info,
                    $"Group '{group.Name}' holds {Sizes.Megabytes(bytes)} of source files, over the {limit} limit. No built bundles were found, so this is an estimate",
                    objectPath: group.Name));
            }
        }
    }

    /// <summary>
    /// A scene that is both addressable and in Build Settings ends up in the player twice. The
    /// Addressables package stops this when either list is edited in the editor, so in practice it
    /// is a state left by a merge or a hand edit, which nothing re-checks until the next edit.
    /// </summary>
    public sealed class SceneInBuildRule : IAuditRule
    {
        public const string RuleId = "scene-in-build";

        public string Id => RuleId;

        public void Check(AddressablesSnapshot snapshot, Usage usage, AuditOptions options, List<Finding> findings)
        {
            foreach (GroupRecord group in snapshot.Groups)
            {
                foreach (EntryRecord entry in group.Entries)
                {
                    if (!entry.IsScene || !snapshot.BuildScenes.Contains(entry.AssetPath)) continue;
                    findings.Add(new Finding(RuleId, Severity.Warning, "Scene is addressable and also enabled in Build Settings, so it is built twice",
                        entry.AssetPath, group.Name));
                }
            }
        }
    }
}
