using System;
using System.IO;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Writes a report to disk. A <c>.json</c> path gets JSON, anything else gets plain text.</summary>
    public static class ReportFile
    {
        public static void Write(string path, ScanReport report)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            bool json = string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);
            File.WriteAllText(path, json ? ReportFormatter.ToJson(report) : ReportFormatter.ToText(report));
        }

        /// <summary>A file name for a report saved from a window: the tool and the time it was saved.</summary>
        public static string NameFor(ScanReport report, DateTime now)
        {
            string tool = report.Tool.Replace(' ', '-').ToLowerInvariant();
            return $"{tool}-{now:yyyyMMdd-HHmmss}.json";
        }
    }
}
