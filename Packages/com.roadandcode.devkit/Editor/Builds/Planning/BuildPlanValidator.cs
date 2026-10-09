using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>Finds what would make a set of builds fail, or surprise someone, before any of them starts.</summary>
    public static class BuildPlanValidator
    {
        public const string NoProfiles = "no-profiles";
        public const string NoScenes = "no-scenes";
        public const string BadOutputPath = "bad-output-path";
        public const string OutputInAssets = "output-in-assets";
        public const string DuplicateOutput = "duplicate-output";
        public const string DuplicateName = "duplicate-name";
        public const string PlatformNotInstalled = "platform-not-installed";
        public const string AbsoluteOutput = "absolute-output";

        public static List<Finding> Check(IReadOnlyList<BuildProfile> profiles, ProjectFacts project)
        {
            var findings = new List<Finding>();
            if (profiles.Count == 0) findings.Add(new Finding(NoProfiles, Severity.Error, "There is nothing to build: no profile is enabled"));
            if (project.EnabledScenes.Count == 0) findings.Add(new Finding(NoScenes, Severity.Error, "No scene is enabled in Build Settings"));

            var locations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BuildProfile profile in profiles)
            {
                if (!names.Add(profile.Name))
                {
                    findings.Add(new Finding(DuplicateName, Severity.Warning, $"More than one profile is called '{profile.Name}'; the command line picks profiles by name", objectPath: profile.Name));
                }

                if (!project.InstalledPlatforms.Contains(profile.Platform))
                {
                    findings.Add(new Finding(PlatformNotInstalled, Severity.Error,
                        $"'{profile.Name}' needs {PlatformTraits.For(profile.Platform).Name} build support, which is not installed in this editor", objectPath: profile.Name));
                }

                if (!OutputPaths.TryResolve(profile, project, out string location, out string problem))
                {
                    findings.Add(new Finding(BadOutputPath, Severity.Error, $"'{profile.Name}' {problem}", objectPath: profile.Name));
                    continue;
                }

                if (OutputPaths.IsInsideAssets(location))
                {
                    findings.Add(new Finding(OutputInAssets, Severity.Error, $"'{profile.Name}' builds into the Assets folder, where Unity would import the build", objectPath: profile.Name, detail: location));
                }
                else if (OutputPaths.IsAbsolute(location))
                {
                    findings.Add(new Finding(AbsoluteOutput, Severity.Info, $"'{profile.Name}' builds to an absolute path, which only exists on this machine", objectPath: profile.Name, detail: location));
                }

                if (locations.TryGetValue(location, out string other))
                {
                    findings.Add(new Finding(DuplicateOutput, Severity.Error, $"'{profile.Name}' and '{other}' build to the same place", objectPath: profile.Name, detail: location));
                }
                else
                {
                    locations[location] = profile.Name;
                }
            }

            return findings;
        }

        public static bool HasErrors(List<Finding> findings) => findings.Exists(finding => finding.Severity == Severity.Error);
    }
}
