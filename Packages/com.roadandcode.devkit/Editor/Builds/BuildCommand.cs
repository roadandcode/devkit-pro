using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEngine;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>
    /// Command-line entry point. Builds the enabled profiles of the project's build plan:
    /// <code>Unity -batchmode -projectPath . -executeMethod RoadAndCode.DevKit.Builds.BuildCommand.Run</code>
    /// Optional: <c>-devkitProfiles Web,Windows</c> (these profiles, enabled or not),
    /// <c>-devkitPlan &lt;asset path&gt;</c> (when the project has more than one plan),
    /// <c>-devkitReport &lt;path&gt;</c>. Exits with 0 when every build succeeded, 1 when one failed
    /// and 2 when nothing could be built.
    /// </summary>
    public static class BuildCommand
    {
        public const string ProfilesArgument = "-devkitProfiles";
        public const string PlanArgument = "-devkitPlan";

        public static void Run()
        {
            CommandLineArguments arguments = CommandLineArguments.FromProcess();
            var store = new AssetBuildPlanStore();
            string planPath = arguments.Value(PlanArgument);
            BuildPlan plan = planPath == null ? store.Find() : store.Find(planPath);
            var project = new UnityProjectInfo();

            EditorApplication.Exit(Execute(arguments, plan, BuildRunnerComposition.Session(project, new LibraryBuildHistory()), Debug.Log));
        }

        public static int Execute(CommandLineArguments arguments, BuildPlan plan, BuildSession session, Action<string> log)
        {
            if (plan == null)
            {
                log("[DevKit] No build plan found. Create one from Tools > DevKit Pro > Build Runner, or pass -devkitPlan <asset path>.");
                return ExitCode.CouldNotRun;
            }

            if (!TrySelect(plan, arguments.List(ProfilesArgument), out List<BuildProfile> profiles, out string unknown))
            {
                log($"[DevKit] The build plan has no profile called '{unknown}'.");
                return ExitCode.CouldNotRun;
            }

            BuildRunResult result = session.Run(profiles, plan.StopOnFailure);
            ScanReport report = result.ToReport();
            int code = CommandOutcome.Finish(report, arguments, text => log("[DevKit] " + text));

            // An error from the check means no build was attempted at all.
            return result.Outcomes.Count == 0 ? ExitCode.CouldNotRun : code;
        }

        private static bool TrySelect(BuildPlan plan, IReadOnlyList<string> names, out List<BuildProfile> profiles, out string unknown)
        {
            unknown = null;
            profiles = new List<BuildProfile>();
            if (names.Count == 0)
            {
                foreach (BuildProfile profile in plan.Profiles)
                {
                    if (profile.Enabled) profiles.Add(profile);
                }

                return true;
            }

            foreach (string name in names)
            {
                BuildProfile match = null;
                foreach (BuildProfile profile in plan.Profiles)
                {
                    if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)) match = match ?? profile;
                }

                if (match == null)
                {
                    unknown = name;
                    return false;
                }

                profiles.Add(match);
            }

            return true;
        }
    }
}
