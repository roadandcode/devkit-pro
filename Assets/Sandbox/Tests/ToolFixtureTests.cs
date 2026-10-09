using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.AddressablesAudit;
using RoadAndCode.DevKit.Builds;
using RoadAndCode.DevKit.Common;
using RoadAndCode.DevKit.DataBrowser;
using RoadAndCode.DevKit.Sandbox.Editor;
using RoadAndCode.DevKit.Validation;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.Sandbox.Tests
{
    /// <summary>The ScriptableObject Browser against the sample data, through the real reader.</summary>
    public sealed class BrowserFixtureTests
    {
        private static Catalog Read(bool includeSubAssets = false)
        {
            return new Catalog(new UnityCatalogReader().Read(includeSubAssets, new NullProgress()));
        }

        private static int Count(Catalog catalog, string typeFullName)
        {
            return catalog.Find(new CatalogQuery { TypeFullName = typeFullName, ProjectTypesOnly = false }).Count;
        }

        [Test]
        public void The_sample_data_is_listed_by_type()
        {
            Catalog catalog = Read();

            Assert.That(Count(catalog, typeof(ItemDefinition).FullName), Is.EqualTo(3));
            Assert.That(Count(catalog, typeof(EnemyDefinition).FullName), Is.EqualTo(2));
            Assert.That(Count(catalog, typeof(WaveTable).FullName), Is.EqualTo(2));
            Assert.That(Count(catalog, typeof(BuildPlan).FullName), Is.EqualTo(1));
        }

        [Test]
        public void The_asset_without_a_script_is_listed_as_such()
        {
            List<AssetRecord> found = Read().Find(new CatalogQuery { TypeFullName = AssetRecord.MissingScriptType });

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].Name, Is.EqualTo("Orphan"));
        }

        [Test]
        public void Addressables_own_settings_are_package_types_and_hidden_by_default()
        {
            Catalog catalog = Read();

            Assert.That(catalog.Count(projectTypesOnly: false), Is.GreaterThan(catalog.Count(projectTypesOnly: true)));
            Assert.That(catalog.Types(projectTypesOnly: true), Has.None.Matches<TypeEntry>(type => type.TypeFullName.StartsWith("UnityEditor.")));
            Assert.That(catalog.Types(projectTypesOnly: false), Has.Some.Matches<TypeEntry>(type => type.TypeName == "AddressableAssetGroup"));
        }

        [Test]
        public void Search_finds_an_asset_by_name_and_by_type()
        {
            Catalog catalog = Read();

            Assert.That(catalog.Find(new CatalogQuery { Text = "sword" }), Has.Count.EqualTo(1));
            Assert.That(catalog.Find(new CatalogQuery { Text = "t:enemy" }), Has.Count.EqualTo(2));
        }

        [Test]
        public void The_command_fails_because_of_the_orphan()
        {
            var log = new List<string>();

            int code = CatalogCommand.Execute(new CommandLineArguments(new string[0]), new UnityCatalogReader(), log.Add);

            Assert.That(code, Is.EqualTo(ExitCode.Findings));
            Assert.That(log[0], Does.Contain("RoadAndCode.DevKit.Sandbox.ItemDefinition: 3"));
        }
    }

    /// <summary>The Build Runner's reading of this project. Nothing is built here.</summary>
    public sealed class BuildFixtureTests
    {
        [Test]
        public void The_project_is_read_as_it_is_set_up()
        {
            ProjectFacts project = new UnityProjectInfo().Read();

            Assert.That(project.ProductName, Is.EqualTo("DevKit Sandbox"));
            Assert.That(project.EnabledScenes, Is.EqualTo(new[] { SandboxContent.CleanScene }));
            Assert.That(project.InstalledPlatforms, Has.Member(BuildPlatform.Web));
        }

        [Test]
        public void The_plan_asset_is_found_and_has_nothing_wrong_with_it()
        {
            BuildPlan plan = new AssetBuildPlanStore().Find();
            ProjectFacts project = new UnityProjectInfo().Read();

            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.Profiles, Has.Count.EqualTo(3));
            Assert.That(plan.Profiles[0].WebCompression, Is.EqualTo(WebCompression.Gzip));
            Assert.That(plan.Profiles[0].WebDecompressionFallback, Is.True);

            var installed = new List<BuildProfile>();
            foreach (BuildProfile profile in plan.Profiles)
            {
                if (project.InstalledPlatforms.Contains(profile.Platform)) installed.Add(profile);
            }

            Assert.That(BuildPlanValidator.Check(installed, project), Is.Empty);
            Assert.That(OutputPaths.TryResolve(plan.Profiles[1], project, out string windows, out _), Is.True);
            Assert.That(windows, Is.EqualTo("Builds/Windows/DevKitSandbox.exe"));
        }
    }

    /// <summary>What the editor itself has to pick up: shortcuts and preference pages.</summary>
    public sealed class EditorIntegrationTests
    {
        private static readonly string[] ShortcutIds =
        {
            "DevKit Pro/Validate Open Scenes", "DevKit Pro/Build Runner", "DevKit Pro/Addressables Audit", "DevKit Pro/ScriptableObject Browser",
        };

        [Test]
        public void Every_shortcut_is_registered_with_a_binding()
        {
            var available = new List<string>(ShortcutManager.instance.GetAvailableShortcutIds());

            foreach (string id in ShortcutIds)
            {
                Assert.That(available, Has.Member(id));
                Assert.That(ShortcutManager.instance.GetShortcutBinding(id).keyCombinationSequence, Is.Not.Empty, id);
            }
        }

        [Test]
        public void No_other_shortcut_uses_the_same_keys()
        {
            IShortcutManager manager = ShortcutManager.instance;
            var mine = new Dictionary<string, string>();
            foreach (string id in ShortcutIds) mine[manager.GetShortcutBinding(id).ToString()] = id;

            foreach (string id in manager.GetAvailableShortcutIds())
            {
                if (System.Array.IndexOf(ShortcutIds, id) >= 0) continue;

                string binding = manager.GetShortcutBinding(id).ToString();
                Assert.That(mine.ContainsKey(binding), Is.False, $"'{id}' is also bound to {binding}");
            }

            Assert.That(mine, Has.Count.EqualTo(ShortcutIds.Length), "two of the package's own shortcuts share keys");
        }

        [Test]
        public void Every_tool_has_a_preferences_page_under_the_shared_root()
        {
            SettingsProvider[] pages =
            {
                GeneralPreferencesPage.Create(), ValidationPreferencesPage.Create(), BuildPreferencesPage.Create(),
                AuditPreferencesPage.Create(), BrowserPreferencesPage.Create(),
            };

            Assert.That(pages[0].settingsPath, Is.EqualTo("Preferences/DevKit Pro"));
            foreach (SettingsProvider page in pages)
            {
                Assert.That(page.scope, Is.EqualTo(SettingsScope.User));
                Assert.That(page.settingsPath, Does.StartWith("Preferences/DevKit Pro"));

                var root = new VisualElement();
                page.activateHandler(string.Empty, root);
                Assert.That(root.Q<Label>(className: "devkit-page__title"), Is.Not.Null, page.settingsPath);
                Assert.That(root.Query<VisualElement>(className: "devkit-page__field").ToList(), Is.Not.Empty, page.settingsPath);
            }
        }
    }
}
