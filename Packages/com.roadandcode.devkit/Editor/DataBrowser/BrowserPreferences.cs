using System.Collections.Generic;
using RoadAndCode.DevKit.Common;
using UnityEditor;

namespace RoadAndCode.DevKit.DataBrowser
{
    /// <summary>Per-user settings of the ScriptableObject Browser.</summary>
    public static class BrowserPreferences
    {
        public static readonly BoolPreference ProjectTypesOnly = new BoolPreference(
            "DataBrowser.ProjectTypesOnly",
            "Project types only",
            "Hide assets whose type comes from Unity or an installed package, such as render pipeline and Addressables settings.",
            true);

        public static readonly BoolPreference IncludeSubAssets = new BoolPreference(
            "DataBrowser.IncludeSubAssets",
            "Include sub-assets",
            "Also list ScriptableObjects stored inside another asset's file. Slower: those files have to be loaded.",
            false);

        public static readonly IReadOnlyList<Preference> Page = new Preference[] { ProjectTypesOnly, IncludeSubAssets };
    }

    public static class BrowserPreferencesPage
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return PreferencesPage.Create(
                PreferencesPage.RootPath + "/ScriptableObject Browser",
                "ScriptableObject Browser",
                "What the browser lists. Press Refresh in the window after changing these.",
                BrowserPreferences.Page,
                new EditorPreferenceStore());
        }
    }
}
