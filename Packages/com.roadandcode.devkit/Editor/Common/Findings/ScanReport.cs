using System;
using System.Collections.Generic;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>A named number or path that describes a run: how many scenes were read, where a build went.</summary>
    public readonly struct ReportFact
    {
        public ReportFact(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; }
    }

    /// <summary>The result of one run of a tool.</summary>
    public sealed class ScanReport
    {
        private static readonly Finding[] NoFindings = new Finding[0];
        private static readonly ReportFact[] NoFacts = new ReportFact[0];

        public ScanReport(string tool, IReadOnlyList<Finding> findings, IReadOnlyList<ReportFact> facts, TimeSpan duration)
        {
            Tool = tool;
            Findings = findings ?? NoFindings;
            Facts = facts ?? NoFacts;
            Duration = duration;
        }

        public string Tool { get; }

        public IReadOnlyList<Finding> Findings { get; }

        public IReadOnlyList<ReportFact> Facts { get; }

        public TimeSpan Duration { get; }

        public int Count(Severity severity)
        {
            int count = 0;
            for (int i = 0; i < Findings.Count; i++)
            {
                if (Findings[i].Severity == severity) count++;
            }

            return count;
        }

        public bool HasAtLeast(Severity severity)
        {
            for (int i = 0; i < Findings.Count; i++)
            {
                if (Findings[i].Severity >= severity) return true;
            }

            return false;
        }

        /// <summary>One line for a status bar or a log.</summary>
        public string Summary =>
            $"{Tool}: {Count(Severity.Error)} errors, {Count(Severity.Warning)} warnings, {Count(Severity.Info)} notes in {Duration.TotalMilliseconds:F0} ms";
    }
}
