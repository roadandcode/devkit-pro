using System.Globalization;
using System.Text;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Turns a report into text. JSON for machines, plain lines for a log or a person.</summary>
    public static class ReportFormatter
    {
        public static string ToJson(ScanReport report)
        {
            var json = new StringBuilder(256 + report.Findings.Count * 160);
            json.Append("{\n");
            json.Append("  \"tool\": ").Append(Quote(report.Tool)).Append(",\n");
            json.Append("  \"durationMs\": ").Append(((long)report.Duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"counts\": { \"error\": ").Append(report.Count(Severity.Error))
                .Append(", \"warning\": ").Append(report.Count(Severity.Warning))
                .Append(", \"info\": ").Append(report.Count(Severity.Info)).Append(" },\n");

            json.Append("  \"facts\": {");
            for (int i = 0; i < report.Facts.Count; i++)
            {
                json.Append(i == 0 ? "\n" : ",\n");
                json.Append("    ").Append(Quote(report.Facts[i].Name)).Append(": ").Append(Quote(report.Facts[i].Value));
            }

            json.Append(report.Facts.Count == 0 ? "},\n" : "\n  },\n");

            json.Append("  \"findings\": [");
            for (int i = 0; i < report.Findings.Count; i++)
            {
                Finding finding = report.Findings[i];
                json.Append(i == 0 ? "\n" : ",\n");
                json.Append("    { \"rule\": ").Append(Quote(finding.Rule))
                    .Append(", \"severity\": ").Append(Quote(Name(finding.Severity)))
                    .Append(", \"message\": ").Append(Quote(finding.Message))
                    .Append(", \"asset\": ").Append(Quote(finding.AssetPath))
                    .Append(", \"object\": ").Append(Quote(finding.ObjectPath))
                    .Append(", \"detail\": ").Append(Quote(finding.Detail)).Append(" }");
            }

            json.Append(report.Findings.Count == 0 ? "]\n" : "\n  ]\n");
            json.Append("}\n");
            return json.ToString();
        }

        public static string ToText(ScanReport report)
        {
            var text = new StringBuilder(128 + report.Findings.Count * 120);
            text.Append(report.Summary).Append('\n');
            for (int i = 0; i < report.Facts.Count; i++)
            {
                text.Append("  ").Append(report.Facts[i].Name).Append(": ").Append(report.Facts[i].Value).Append('\n');
            }

            for (int i = 0; i < report.Findings.Count; i++)
            {
                Finding finding = report.Findings[i];
                text.Append(Name(finding.Severity).ToUpperInvariant()).Append(' ').Append(finding.Rule).Append(": ").Append(finding.Message);
                if (finding.Location.Length > 0) text.Append(" [").Append(finding.Location).Append(']');
                if (finding.Detail.Length > 0) text.Append(" (").Append(finding.Detail).Append(')');
                text.Append('\n');
            }

            return text.ToString();
        }

        public static string Name(Severity severity)
        {
            if (severity == Severity.Error) return "error";
            return severity == Severity.Warning ? "warning" : "info";
        }

        public static string Quote(string value)
        {
            if (value == null) return "\"\"";

            var quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (c < ' ') quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else quoted.Append(c);
                        break;
                }
            }

            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
