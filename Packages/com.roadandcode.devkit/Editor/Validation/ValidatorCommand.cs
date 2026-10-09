using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEngine;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>
    /// Command-line entry point:
    /// <code>Unity -batchmode -projectPath . -executeMethod RoadAndCode.DevKit.Validation.ValidatorCommand.Run</code>
    /// Optional: <c>-devkitScope open,build,scenes,prefabs,assets,project,all</c> (default project),
    /// <c>-devkitRules &lt;ids&gt;</c> (default all), <c>-devkitOptionalFields &lt;patterns&gt;</c>,
    /// <c>-devkitReport &lt;path&gt;</c>, <c>-devkitFailOn error|warning|info|never</c>.
    /// </summary>
    public static class ValidatorCommand
    {
        public const string ScopeArgument = "-devkitScope";
        public const string RulesArgument = "-devkitRules";
        public const string OptionalFieldsArgument = "-devkitOptionalFields";

        public static void Run()
        {
            EditorApplication.Exit(Execute(CommandLineArguments.FromProcess(), new UnityProjectReader(), Debug.Log));
        }

        public static int Execute(CommandLineArguments arguments, IProjectReader reader, Action<string> log)
        {
            if (!ValidationScopes.TryParse(arguments.List(ScopeArgument), out ValidationScope scope, out string unknownScope))
            {
                log($"[DevKit] Unknown scope '{unknownScope}'. Use open, build, scenes, prefabs, assets, project or all.");
                return ExitCode.CouldNotRun;
            }

            if (!TryRules(arguments, out IReadOnlyList<IValidationRule> rules, out string unknownRule))
            {
                log($"[DevKit] Unknown check '{unknownRule}'.");
                return ExitCode.CouldNotRun;
            }

            ScanReport report = new ValidationScanner(reader, () => rules).Scan(scope, new NullProgress());
            return CommandOutcome.Finish(report, arguments, text => log("[DevKit] " + text));
        }

        // A command-line run starts from the defaults, never from this machine's preferences,
        // so the same command gives the same answer everywhere.
        private static bool TryRules(CommandLineArguments arguments, out IReadOnlyList<IValidationRule> rules, out string unknown)
        {
            unknown = null;
            var defaults = new MemoryPreferenceStore();
            string optional = arguments.Value(OptionalFieldsArgument);
            if (optional != null) ValidationPreferences.OptionalFields.Set(defaults, optional);

            IReadOnlyList<IValidationRule> all = ValidationPreferences.Rules(defaults);
            IReadOnlyList<string> wanted = arguments.List(RulesArgument);
            if (wanted.Count == 0)
            {
                rules = all;
                return true;
            }

            var chosen = new List<IValidationRule>();
            foreach (string id in wanted)
            {
                IValidationRule match = Find(all, id);
                if (match == null)
                {
                    unknown = id;
                    rules = null;
                    return false;
                }

                chosen.Add(match);
            }

            rules = chosen;
            return true;
        }

        private static IValidationRule Find(IReadOnlyList<IValidationRule> rules, string id)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                if (string.Equals(rules[i].Id, id, StringComparison.OrdinalIgnoreCase)) return rules[i];
            }

            return null;
        }
    }
}
