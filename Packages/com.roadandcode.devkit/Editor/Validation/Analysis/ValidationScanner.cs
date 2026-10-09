using System;
using System.Collections.Generic;
using System.Diagnostics;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>What a read covered.</summary>
    public sealed class ReadSummary
    {
        public int Scenes { get; set; }

        public int Prefabs { get; set; }

        public int Assets { get; set; }

        public int Objects { get; set; }

        /// <summary>The person stopped it part of the way through.</summary>
        public bool Cancelled { get; set; }
    }

    /// <summary>Reads the project and hands over one record per object. The analysis owns this; the editor implements it.</summary>
    public interface IProjectReader
    {
        ReadSummary Read(ValidationScope scope, Action<ObjectRecord> visit, IProgressSink progress);
    }

    /// <summary>Runs the rules over everything a reader returns and packs the result into a report.</summary>
    public sealed class ValidationScanner
    {
        public const string ToolName = "Validator";

        private readonly IProjectReader _reader;
        private readonly Func<IReadOnlyList<IValidationRule>> _rules;

        /// <param name="rules">Asked for at the start of every scan, so a changed preference applies to the next one.</param>
        public ValidationScanner(IProjectReader reader, Func<IReadOnlyList<IValidationRule>> rules)
        {
            _reader = reader;
            _rules = rules;
        }

        public ScanReport Scan(ValidationScope scope, IProgressSink progress)
        {
            var watch = Stopwatch.StartNew();
            IReadOnlyList<IValidationRule> rules = _rules();
            var findings = new List<Finding>();

            ReadSummary summary = _reader.Read(scope, record =>
            {
                for (int i = 0; i < rules.Count; i++) rules[i].Check(record, findings);
            }, progress);

            findings.Sort(Compare);

            var facts = new List<ReportFact>
            {
                new ReportFact("Scope", ValidationScopes.Describe(scope)),
                new ReportFact("Scenes", summary.Scenes.ToString()),
                new ReportFact("Prefabs", summary.Prefabs.ToString()),
                new ReportFact("Assets", summary.Assets.ToString()),
                new ReportFact("Objects", summary.Objects.ToString()),
            };
            if (summary.Cancelled) facts.Add(new ReportFact("Cancelled", "yes, the results are partial"));

            return new ScanReport(ToolName, findings, facts, watch.Elapsed);
        }

        // Worst first, then grouped by where they are, so the list reads top-down by file.
        private static int Compare(Finding a, Finding b)
        {
            int order = b.Severity.CompareTo(a.Severity);
            if (order != 0) return order;
            order = string.CompareOrdinal(a.AssetPath, b.AssetPath);
            if (order != 0) return order;
            order = string.CompareOrdinal(a.ObjectPath, b.ObjectPath);
            if (order != 0) return order;
            order = string.CompareOrdinal(a.Rule, b.Rule);
            return order != 0 ? order : string.CompareOrdinal(a.Detail, b.Detail);
        }
    }
}
