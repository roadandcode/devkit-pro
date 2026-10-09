using System.Collections.Generic;
using UnityEditor;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Settings every tool shares.</summary>
    public static class GeneralPreferences
    {
        public static readonly TextPreference ReportFolder = new TextPreference(
            "General.ReportFolder",
            "Report folder",
            "Where a report saved from a window goes. Relative to the project folder.",
            "Logs/DevKit");

        public static readonly BoolPreference SelectOnClick = new BoolPreference(
            "General.SelectOnClick",
            "Select what a finding points at",
            "Clicking a finding selects and pings the object or asset it is about.",
            true);

        public static readonly IReadOnlyList<Preference> All = new Preference[] { ReportFolder, SelectOnClick };
    }

    public static class GeneralPreferencesPage
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return PreferencesPage.Create(
                PreferencesPage.RootPath,
                "DevKit Pro",
                "Settings shared by every tool. Each tool has its own page underneath. Shortcuts are under Edit > Shortcuts, in the DevKit Pro category.",
                GeneralPreferences.All,
                new EditorPreferenceStore());
        }
    }
}
