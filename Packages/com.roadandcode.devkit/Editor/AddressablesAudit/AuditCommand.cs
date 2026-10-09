using System;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEngine;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>
    /// Command-line entry point:
    /// <code>Unity -batchmode -projectPath . -executeMethod RoadAndCode.DevKit.AddressablesAudit.AuditCommand.Run</code>
    /// Optional: <c>-devkitMaxBundleMb &lt;number&gt;</c> (default 10), <c>-devkitPackageAssets</c>,
    /// <c>-devkitReport &lt;path&gt;</c>, <c>-devkitFailOn error|warning|info|never</c>.
    /// </summary>
    public static class AuditCommand
    {
        public const string MaxBundleArgument = "-devkitMaxBundleMb";
        public const string PackageAssetsArgument = "-devkitPackageAssets";

        public static void Run()
        {
            EditorApplication.Exit(Execute(CommandLineArguments.FromProcess(), AuditComposition.Runner(), Debug.Log));
        }

        public static int Execute(CommandLineArguments arguments, AuditRunner runner, Action<string> log)
        {
            var options = new AuditOptions { IncludePackageAssets = arguments.Has(PackageAssetsArgument) };
            if (arguments.Has(MaxBundleArgument))
            {
                if (!arguments.TryNumber(MaxBundleArgument, out float megabytes) || megabytes <= 0f)
                {
                    log($"[DevKit] {MaxBundleArgument} needs a size in megabytes, such as 10 or 0.5.");
                    return ExitCode.CouldNotRun;
                }

                options.MaxBundleMegabytes = megabytes;
            }

            if (!runner.TryRun(options, new NullProgress(), out AuditResult result, out string problem))
            {
                log("[DevKit] " + problem);
                return ExitCode.CouldNotRun;
            }

            return CommandOutcome.Finish(result.Report, arguments, text => log("[DevKit] " + text));
        }
    }
}
