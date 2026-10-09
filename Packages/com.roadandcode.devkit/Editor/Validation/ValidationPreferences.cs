using System.Collections.Generic;
using RoadAndCode.DevKit.Common;
using UnityEditor;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>Per-user settings of the validator, and the rule list they produce.</summary>
    public static class ValidationPreferences
    {
        public const string DefaultOptionalFields = "*optional*";

        public static readonly BoolPreference MissingScripts = new BoolPreference(
            "Validation.MissingScripts", "Missing scripts", "Components and assets whose script file is gone.", true);

        public static readonly BoolPreference MissingReferences = new BoolPreference(
            "Validation.MissingReferences", "Missing references", "Reference fields that point at something deleted.", true);

        public static readonly BoolPreference MissingPrefabs = new BoolPreference(
            "Validation.MissingPrefabs", "Missing prefabs", "Instances of a prefab that has been deleted.", true);

        public static readonly BoolPreference UnassignedReferences = new BoolPreference(
            "Validation.UnassignedReferences", "Unassigned references", "Reference fields on the project's own scripts that were left empty.", true);

        public static readonly BoolPreference EmptyObjects = new BoolPreference(
            "Validation.EmptyObjects", "Empty objects", "Objects with no components and no children.", true);

        public static readonly TextPreference OptionalFields = new TextPreference(
            "Validation.OptionalFields",
            "Fields allowed to be empty",
            "Comma-separated field names; * stands for anything. A matching reference field is not reported when it is empty.",
            DefaultOptionalFields);

        /// <summary>The scope the window was last used with. Not on the page; the window's own menu sets it.</summary>
        public static readonly NumberPreference Scope = new NumberPreference(
            "Validation.Scope", "Scope", string.Empty, (float)ValidationScopes.Project, 1f, 31f);

        public static readonly IReadOnlyList<Preference> Page = new Preference[]
        {
            MissingScripts, MissingReferences, MissingPrefabs, UnassignedReferences, EmptyObjects, OptionalFields,
        };

        public static IReadOnlyList<IValidationRule> Rules(IPreferenceStore store)
        {
            var rules = new List<IValidationRule>();
            if (MissingScripts.Get(store)) rules.Add(new MissingScriptRule());
            if (MissingReferences.Get(store)) rules.Add(new MissingReferenceRule());
            if (MissingPrefabs.Get(store)) rules.Add(new MissingPrefabRule());
            if (UnassignedReferences.Get(store)) rules.Add(new UnassignedReferenceRule(new NamePatterns(OptionalFields.Get(store))));
            if (EmptyObjects.Get(store)) rules.Add(new EmptyObjectRule());
            return rules;
        }
    }

    public static class ValidationPreferencesPage
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return PreferencesPage.Create(
                PreferencesPage.RootPath + "/Validator",
                "Validator",
                "Which checks the Scene and Asset Validator window runs. A command-line run ignores these and runs every check unless it is told otherwise.",
                ValidationPreferences.Page,
                new EditorPreferenceStore());
        }
    }
}
