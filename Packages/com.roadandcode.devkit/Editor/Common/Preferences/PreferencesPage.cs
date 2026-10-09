using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Builds a page for the Preferences window from a list of settings.</summary>
    public static class PreferencesPage
    {
        /// <summary>Every tool's page hangs under this one, as <c>Preferences/DevKit Pro/&lt;Tool&gt;</c>.</summary>
        public const string RootPath = "Preferences/DevKit Pro";

        public static SettingsProvider Create(string path, string title, string summary, IReadOnlyList<Preference> preferences, IPreferenceStore store)
        {
            var keywords = new HashSet<string>();
            for (int i = 0; i < preferences.Count; i++) keywords.Add(preferences[i].Label);

            return new SettingsProvider(path, SettingsScope.User)
            {
                label = title,
                keywords = keywords,
                activateHandler = (search, root) => Build(root, title, summary, preferences, store),
            };
        }

        /// <summary>Fills an element with the page. Public so a test can build a page without the Preferences window.</summary>
        public static void Build(VisualElement root, string title, string summary, IReadOnlyList<Preference> preferences, IPreferenceStore store)
        {
            root.Clear();
            DevKitStyle.Apply(root);

            var page = new ScrollView();
            page.AddToClassList("devkit-page");
            root.Add(page);

            var heading = new Label(title);
            heading.AddToClassList("devkit-page__title");
            page.Add(heading);

            var about = new Label(summary);
            about.AddToClassList("devkit-page__summary");
            page.Add(about);

            for (int i = 0; i < preferences.Count; i++)
            {
                VisualElement field = preferences[i].CreateField(store);
                field.AddToClassList("devkit-page__field");
                page.Add(field);
            }

            if (preferences.Count == 0) return;

            var reset = new Button(() =>
            {
                for (int i = 0; i < preferences.Count; i++) preferences[i].Reset(store);
                Build(root, title, summary, preferences, store);
            })
            {
                text = "Reset to defaults",
            };
            reset.AddToClassList("devkit-page__reset");
            page.Add(reset);
        }
    }
}
