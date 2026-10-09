using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>What an audit produced: the report, and the per-group overview the window shows above it.</summary>
    public sealed class AuditResult
    {
        public AuditResult(ScanReport report, IReadOnlyList<GroupSummary> groups)
        {
            Report = report;
            Groups = groups;
        }

        public ScanReport Report { get; }

        public IReadOnlyList<GroupSummary> Groups { get; }
    }

    /// <summary>Runs every rule over a snapshot.</summary>
    public sealed class Auditor
    {
        public const string ToolName = "Addressables Audit";

        private readonly IReadOnlyList<IAuditRule> _rules;

        public Auditor(IReadOnlyList<IAuditRule> rules)
        {
            _rules = rules;
        }

        /// <summary>Every rule, in the order their findings are most useful to read.</summary>
        public static IReadOnlyList<IAuditRule> AllRules()
        {
            return new IAuditRule[]
            {
                new MissingAssetRule(), new UnreferencedRule(), new EmptyGroupRule(), new DuplicatedDependencyRule(),
                new OversizedBundleRule(), new DuplicateAddressRule(), new SceneInBuildRule(),
            };
        }

        public AuditResult Audit(AddressablesSnapshot snapshot, AuditOptions options, TimeSpan readTime)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var usage = new Usage(snapshot);
            var findings = new List<Finding>();
            for (int i = 0; i < _rules.Count; i++) _rules[i].Check(snapshot, usage, options, findings);

            // Worst first; within a severity the rules' own order is kept.
            var ordered = new List<Finding>(findings.Count);
            for (int severity = (int)Severity.Error; severity >= (int)Severity.Info; severity--)
            {
                foreach (Finding finding in findings)
                {
                    if ((int)finding.Severity == severity) ordered.Add(finding);
                }
            }

            var groups = new List<GroupSummary>(snapshot.Groups.Count);
            int entries = 0;
            foreach (GroupRecord group in snapshot.Groups)
            {
                groups.Add(new GroupSummary(group.Name, group.Entries.Count, usage.UsedIn(group), usage.SourceBytes(group)));
                entries += group.Entries.Count;
            }

            long bundleBytes = 0;
            foreach (BundleRecord bundle in snapshot.Bundles) bundleBytes += bundle.SizeBytes;

            var facts = new List<ReportFact>
            {
                new ReportFact("Groups", snapshot.Groups.Count.ToString()),
                new ReportFact("Entries", entries.ToString()),
                new ReportFact("Built bundles", snapshot.Bundles.Count == 0 ? "none found" : $"{snapshot.Bundles.Count} ({Sizes.Megabytes(bundleBytes)})"),
                new ReportFact("Bundle limit", Sizes.Megabytes(options.MaxBundleBytes)),
            };

            return new AuditResult(new ScanReport(ToolName, ordered, facts, readTime + watch.Elapsed), groups);
        }
    }
}
