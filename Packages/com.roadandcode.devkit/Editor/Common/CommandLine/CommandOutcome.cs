using System;

namespace RoadAndCode.DevKit.Common
{
    public static class ExitCode
    {
        /// <summary>The tool ran and found nothing at or above the failure threshold.</summary>
        public const int Clean = 0;

        /// <summary>The tool ran and found something at or above the threshold.</summary>
        public const int Findings = 1;

        /// <summary>The tool could not run: bad arguments or something it needs is not there.</summary>
        public const int CouldNotRun = 2;
    }

    /// <summary>What a command-line run does with its report: write it, log it, choose the exit code.</summary>
    public static class CommandOutcome
    {
        public static int Finish(ScanReport report, CommandLineArguments arguments, Action<string> log)
        {
            string path = arguments.Value(CommandLineArguments.Report);
            if (!string.IsNullOrEmpty(path)) ReportFile.Write(path, report);

            log(ReportFormatter.ToText(report));
            return ExitCodeFor(report, arguments.Value(CommandLineArguments.FailOn));
        }

        public static int ExitCodeFor(ScanReport report, string failOn)
        {
            if (!TryThreshold(failOn, out Severity threshold, out bool never)) return ExitCode.CouldNotRun;
            if (never) return ExitCode.Clean;
            return report.HasAtLeast(threshold) ? ExitCode.Findings : ExitCode.Clean;
        }

        /// <summary>Parses a failure threshold. No value means "error".</summary>
        public static bool TryThreshold(string text, out Severity threshold, out bool never)
        {
            threshold = Severity.Error;
            never = false;
            if (string.IsNullOrEmpty(text)) return true;

            switch (text.ToLowerInvariant())
            {
                case "error": return true;
                case "warning": threshold = Severity.Warning; return true;
                case "info": threshold = Severity.Info; return true;
                case "never": never = true; return true;
                default: return false;
            }
        }
    }
}
