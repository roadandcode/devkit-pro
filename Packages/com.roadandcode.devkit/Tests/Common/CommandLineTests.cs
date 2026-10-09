using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace RoadAndCode.DevKit.Common.Tests
{
    public sealed class CommandLineTests
    {
        private static CommandLineArguments Arguments(params string[] values) => new CommandLineArguments(values);

        private static ScanReport ReportWith(params Severity[] severities)
        {
            var findings = new List<Finding>();
            foreach (Severity severity in severities) findings.Add(new Finding("rule", severity, "message"));
            return new ScanReport("Tool", findings, null, TimeSpan.Zero);
        }

        [Test]
        public void A_value_follows_its_name()
        {
            CommandLineArguments arguments = Arguments("Unity.exe", "-batchmode", "-devkitReport", "Logs/a.json");

            Assert.That(arguments.Value("-devkitReport"), Is.EqualTo("Logs/a.json"));
            Assert.That(arguments.Has("-batchmode"), Is.True);
            Assert.That(arguments.Has("-quit"), Is.False);
        }

        [Test]
        public void Names_are_matched_without_regard_to_case()
        {
            Assert.That(Arguments("-DEVKITREPORT", "x").Value("-devkitReport"), Is.EqualTo("x"));
        }

        [Test]
        public void A_missing_or_empty_value_gives_the_fallback()
        {
            Assert.That(Arguments("-other").Value("-devkitReport", "fallback"), Is.EqualTo("fallback"));
            Assert.That(Arguments("-devkitReport").Value("-devkitReport", "fallback"), Is.EqualTo("fallback"));
            Assert.That(Arguments("-devkitReport", "-quit").Value("-devkitReport", "fallback"), Is.EqualTo("fallback"));
        }

        [Test]
        public void A_list_is_split_on_commas_and_trimmed()
        {
            Assert.That(Arguments("-devkitScope", "build, prefabs,,assets").List("-devkitScope"), Is.EqualTo(new[] { "build", "prefabs", "assets" }));
            Assert.That(Arguments().List("-devkitScope"), Is.Empty);
        }

        [Test]
        public void A_number_is_read_with_a_dot_whatever_the_machine_uses()
        {
            Assert.That(Arguments("-size", "0.25").TryNumber("-size", out float number), Is.True);
            Assert.That(number, Is.EqualTo(0.25f));
            Assert.That(Arguments("-size", "big").TryNumber("-size", out _), Is.False);
        }

        [TestCase(null, Severity.Error, ExitCode.Findings)]
        [TestCase(null, Severity.Warning, ExitCode.Clean)]
        [TestCase("error", Severity.Warning, ExitCode.Clean)]
        [TestCase("warning", Severity.Warning, ExitCode.Findings)]
        [TestCase("warning", Severity.Info, ExitCode.Clean)]
        [TestCase("info", Severity.Info, ExitCode.Findings)]
        [TestCase("never", Severity.Error, ExitCode.Clean)]
        [TestCase("WARNING", Severity.Error, ExitCode.Findings)]
        public void The_threshold_decides_the_exit_code(string failOn, Severity worst, int expected)
        {
            Assert.That(CommandOutcome.ExitCodeFor(ReportWith(worst), failOn), Is.EqualTo(expected));
        }

        [Test]
        public void An_unknown_threshold_is_a_failure_to_run()
        {
            Assert.That(CommandOutcome.ExitCodeFor(ReportWith(), "sometimes"), Is.EqualTo(ExitCode.CouldNotRun));
        }

        [Test]
        public void A_clean_report_exits_clean()
        {
            Assert.That(CommandOutcome.ExitCodeFor(ReportWith(), "info"), Is.EqualTo(ExitCode.Clean));
        }

        [Test]
        public void Finish_writes_the_file_logs_the_text_and_returns_the_code()
        {
            string path = Path.Combine(Path.GetTempPath(), "devkit-tests-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                string logged = null;
                int code = CommandOutcome.Finish(ReportWith(Severity.Error), Arguments("-devkitReport", path), text => logged = text);

                Assert.That(code, Is.EqualTo(ExitCode.Findings));
                Assert.That(File.Exists(path), Is.True);
                Assert.That(logged, Does.Contain("ERROR rule: message"));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
