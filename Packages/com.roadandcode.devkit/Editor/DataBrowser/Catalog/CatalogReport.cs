using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.DataBrowser
{
    /// <summary>Reads the project's ScriptableObjects. The catalog owns this; the editor implements it.</summary>
    public interface ICatalogReader
    {
        IReadOnlyList<AssetRecord> Read(bool includeSubAssets, IProgressSink progress);
    }

    /// <summary>The catalog as a report: how many assets of each type, and the ones whose script is gone.</summary>
    public static class CatalogReport
    {
        public const string ToolName = "ScriptableObject Browser";
        public const string MissingScriptRule = "missing-script";

        public static ScanReport From(Catalog catalog, bool projectTypesOnly, TimeSpan duration)
        {
            List<TypeEntry> types = catalog.Types(projectTypesOnly);
            var facts = new List<ReportFact>
            {
                new ReportFact("Assets", catalog.Count(projectTypesOnly).ToString()),
                new ReportFact("Types", types.Count.ToString()),
            };
            foreach (TypeEntry type in types) facts.Add(new ReportFact(type.TypeFullName, type.Count.ToString()));

            var findings = new List<Finding>();
            foreach (AssetRecord record in catalog.Records)
            {
                if (!record.IsMissingScript) continue;
                findings.Add(new Finding(MissingScriptRule, Severity.Error, "The script this asset was made from is missing", record.Path));
            }

            return new ScanReport(ToolName, findings, facts, duration);
        }
    }
}
