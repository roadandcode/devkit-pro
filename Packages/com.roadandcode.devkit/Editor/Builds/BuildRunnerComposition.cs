using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;
using UnityEditor;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>The one place the Build Runner's concrete types are named and wired together.</summary>
    public static class BuildRunnerComposition
    {
        public static BuildRunnerPresenter Present(IBuildRunnerView view)
        {
            var project = new UnityProjectInfo();
            var history = new LibraryBuildHistory();
            return new BuildRunnerPresenter(view, new AssetBuildPlanStore(), project, Session(project, history), history,
                new FileBrowserRevealer(), new EditorPreferenceStore(), () => DateTime.UtcNow);
        }

        public static BuildSession Session(IProjectInfo project, IBuildHistory history) => new BuildSession(project, new UnityPlayerBuilder(), history);
    }

    /// <summary>Per-user settings of the Build Runner. What gets built is in the project's build plan, not here.</summary>
    public static class BuildPreferences
    {
        public static readonly BoolPreference RevealWhenDone = new BoolPreference(
            "Builds.RevealWhenDone",
            "Show the output when a build finishes",
            "Opens the folder of the last build in the file browser when every build started from the window succeeded.",
            false);

        public static readonly IReadOnlyList<Preference> Page = new Preference[] { RevealWhenDone };
    }

    public static class BuildPreferencesPage
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return PreferencesPage.Create(
                PreferencesPage.RootPath + "/Build Runner",
                "Build Runner",
                "How the Build Runner window behaves on this machine. The builds themselves are listed in the project's Build Plan asset.",
                BuildPreferences.Page,
                new EditorPreferenceStore());
        }
    }
}
