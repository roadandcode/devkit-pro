using System;
using System.IO;
using NUnit.Framework;

namespace RoadAndCode.DevKit.Common.Tests
{
    public sealed class ReportTests
    {
        private static ScanReport Sample()
        {
            var findings = new[]
            {
                new Finding("missing-script", Severity.Error, "A component's script is gone", "Assets/A.unity", "Root/Child", "Component 2"),
                new Finding("empty-object", Severity.Info, "Nothing on it", "Assets/A.unity", "Marker"),
                new Finding("unassigned-reference", Severity.Warning, "Says \"none\"\nand a tab\t", "Assets/B.prefab"),
            };
            var facts = new[] { new ReportFact("Scenes", "1"), new ReportFact("Prefabs", "1") };
            return new ScanReport("Validator", findings, facts, TimeSpan.FromMilliseconds(42));
        }

        [Test]
        public void Counts_are_per_severity()
        {
            ScanReport report = Sample();

            Assert.That(report.Count(Severity.Error), Is.EqualTo(1));
            Assert.That(report.Count(Severity.Warning), Is.EqualTo(1));
            Assert.That(report.Count(Severity.Info), Is.EqualTo(1));
        }

        [Test]
        public void HasAtLeast_looks_at_the_worst_finding()
        {
            var notes = new ScanReport("T", new[] { new Finding("r", Severity.Info, "m") }, null, TimeSpan.Zero);

            Assert.That(notes.HasAtLeast(Severity.Info), Is.True);
            Assert.That(notes.HasAtLeast(Severity.Warning), Is.False);
            Assert.That(Sample().HasAtLeast(Severity.Error), Is.True);
        }

        [Test]
        public void A_report_without_findings_or_facts_is_valid()
        {
            var empty = new ScanReport("T", null, null, TimeSpan.Zero);

            Assert.That(empty.Findings, Is.Empty);
            Assert.That(empty.Facts, Is.Empty);
            Assert.That(ReportFormatter.ToJson(empty), Does.Contain("\"findings\": []"));
            Assert.That(ReportFormatter.ToJson(empty), Does.Contain("\"facts\": {}"));
        }

        [Test]
        public void Location_joins_asset_and_object()
        {
            Assert.That(new Finding("r", Severity.Info, "m", "Assets/A.unity", "Root/Child").Location, Is.EqualTo("Assets/A.unity > Root/Child"));
            Assert.That(new Finding("r", Severity.Info, "m", "Assets/A.asset").Location, Is.EqualTo("Assets/A.asset"));
            Assert.That(new Finding("r", Severity.Info, "m").Location, Is.Empty);
        }

        [Test]
        public void Json_has_counts_facts_and_every_finding()
        {
            string json = ReportFormatter.ToJson(Sample());

            Assert.That(json, Does.Contain("\"tool\": \"Validator\""));
            Assert.That(json, Does.Contain("\"durationMs\": 42"));
            Assert.That(json, Does.Contain("\"counts\": { \"error\": 1, \"warning\": 1, \"info\": 1 }"));
            Assert.That(json, Does.Contain("\"Scenes\": \"1\""));
            Assert.That(json, Does.Contain("\"rule\": \"missing-script\", \"severity\": \"error\""));
            Assert.That(json, Does.Contain("\"object\": \"Root/Child\""));
        }

        [Test]
        public void Json_escapes_quotes_and_control_characters()
        {
            Assert.That(ReportFormatter.Quote("a\"b\\c\nd\te\u0001"), Is.EqualTo("\"a\\\"b\\\\c\\nd\\te\\u0001\""));
            Assert.That(ReportFormatter.Quote(null), Is.EqualTo("\"\""));
        }

        [Test]
        public void Json_brackets_and_braces_balance()
        {
            string json = ReportFormatter.ToJson(Sample());
            string withoutStrings = System.Text.RegularExpressions.Regex.Replace(json, "\"(?:[^\"\\\\]|\\\\.)*\"", "\"\"");

            Assert.That(Occurrences(withoutStrings, '{'), Is.EqualTo(Occurrences(withoutStrings, '}')));
            Assert.That(Occurrences(withoutStrings, '['), Is.EqualTo(Occurrences(withoutStrings, ']')));
        }

        [Test]
        public void Text_has_one_line_per_finding_after_the_summary_and_facts()
        {
            string[] lines = ReportFormatter.ToText(Sample()).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Assert.That(lines[0], Does.StartWith("Validator: 1 errors, 1 warnings, 1 notes"));
            Assert.That(lines[1], Is.EqualTo("  Scenes: 1"));
            Assert.That(lines[3], Is.EqualTo("ERROR missing-script: A component's script is gone [Assets/A.unity > Root/Child] (Component 2)"));
        }

        [Test]
        public void The_extension_picks_the_format()
        {
            string folder = Path.Combine(Path.GetTempPath(), "devkit-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                string json = Path.Combine(folder, "nested", "report.json");
                string text = Path.Combine(folder, "report.txt");

                ReportFile.Write(json, Sample());
                ReportFile.Write(text, Sample());

                Assert.That(File.ReadAllText(json), Does.StartWith("{"));
                Assert.That(File.ReadAllText(text), Does.StartWith("Validator:"));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void A_saved_report_is_named_after_the_tool_and_the_time()
        {
            var report = new ScanReport("Addressables Audit", null, null, TimeSpan.Zero);

            Assert.That(ReportFile.NameFor(report, new DateTime(2026, 3, 4, 5, 6, 7)), Is.EqualTo("addressables-audit-20260304-050607.json"));
        }

        private static int Occurrences(string text, char character)
        {
            int count = 0;
            foreach (char c in text)
            {
                if (c == character) count++;
            }

            return count;
        }
    }
}
