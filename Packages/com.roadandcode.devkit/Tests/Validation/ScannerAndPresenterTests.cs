using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Validation.Tests
{
    public sealed class ScopeTests
    {
        [Test]
        public void No_names_means_the_project_scope()
        {
            Assert.That(ValidationScopes.TryParse(new string[0], out ValidationScope scope, out _), Is.True);
            Assert.That(scope, Is.EqualTo(ValidationScope.BuildScenes | ValidationScope.Prefabs | ValidationScope.Assets));
        }

        [Test]
        public void Names_combine_and_ignore_case()
        {
            Assert.That(ValidationScopes.TryParse(new[] { "OPEN", "prefabs" }, out ValidationScope scope, out _), Is.True);
            Assert.That(scope, Is.EqualTo(ValidationScope.OpenScenes | ValidationScope.Prefabs));
        }

        [Test]
        public void An_unknown_name_is_reported()
        {
            Assert.That(ValidationScopes.TryParse(new[] { "build", "textures" }, out _, out string unknown), Is.False);
            Assert.That(unknown, Is.EqualTo("textures"));
        }

        [Test]
        public void A_scope_describes_itself_in_words()
        {
            Assert.That(ValidationScopes.Describe(ValidationScopes.Project), Is.EqualTo("build scenes, prefabs, assets"));
            Assert.That(ValidationScopes.Describe(ValidationScope.AllScenes | ValidationScope.BuildScenes), Is.EqualTo("all scenes"));
            Assert.That(ValidationScopes.Describe(ValidationScope.None), Is.EqualTo("nothing"));
        }
    }

    public sealed class ScannerTests
    {
        private static readonly IReadOnlyList<IValidationRule> AllRules = ValidationPreferences.Rules(new MemoryPreferenceStore());

        [Test]
        public void Findings_come_out_worst_first_then_by_place()
        {
            FakeReader reader = new FakeReader().With(
                Records.SceneObject("B/Leftover", Records.Transform()),
                Records.SceneObject("B", Records.Transform(), Records.Script("Spawner", Records.Reference("_prefab", ReferenceState.None))),
                Records.SceneObject("A", Records.Transform(), ComponentRecord.MissingScript()),
                Records.SceneObject("Z", Records.Transform(), Records.BuiltIn("MeshRenderer", Records.Reference("m_Mesh", ReferenceState.Missing))));

            ScanReport report = new ValidationScanner(reader, () => AllRules).Scan(ValidationScopes.Project, new NullProgress());

            Assert.That(report.Findings, Has.Count.EqualTo(4));
            Assert.That(report.Findings[0].ObjectPath, Is.EqualTo("A"));
            Assert.That(report.Findings[1].ObjectPath, Is.EqualTo("Z"));
            Assert.That(report.Findings[2].Severity, Is.EqualTo(Severity.Warning));
            Assert.That(report.Findings[3].Severity, Is.EqualTo(Severity.Info));
        }

        [Test]
        public void The_report_says_what_was_covered()
        {
            var reader = new FakeReader();
            reader.Summary.Scenes = 2;
            reader.Summary.Prefabs = 7;

            ScanReport report = new ValidationScanner(reader, () => AllRules).Scan(ValidationScope.OpenScenes, new NullProgress());

            Assert.That(report.Tool, Is.EqualTo("Validator"));
            Assert.That(Fact(report, "Scope"), Is.EqualTo("open scenes"));
            Assert.That(Fact(report, "Scenes"), Is.EqualTo("2"));
            Assert.That(Fact(report, "Prefabs"), Is.EqualTo("7"));
            Assert.That(Fact(report, "Cancelled"), Is.Null);
            Assert.That(reader.LastScope, Is.EqualTo(ValidationScope.OpenScenes));
        }

        [Test]
        public void A_cancelled_read_is_marked_as_partial()
        {
            var reader = new FakeReader();
            reader.Summary.Cancelled = true;

            ScanReport report = new ValidationScanner(reader, () => AllRules).Scan(ValidationScopes.Project, new NullProgress());

            Assert.That(Fact(report, "Cancelled"), Does.Contain("partial"));
        }

        [Test]
        public void The_rules_are_asked_for_again_on_every_scan()
        {
            int asked = 0;
            var scanner = new ValidationScanner(new FakeReader(), () =>
            {
                asked++;
                return AllRules;
            });

            scanner.Scan(ValidationScopes.Project, new NullProgress());
            scanner.Scan(ValidationScopes.Project, new NullProgress());

            Assert.That(asked, Is.EqualTo(2));
        }

        private static string Fact(ScanReport report, string name)
        {
            foreach (ReportFact fact in report.Facts)
            {
                if (fact.Name == name) return fact.Value;
            }

            return null;
        }
    }

    public sealed class PresenterTests
    {
        private FakeView _view;
        private FakeReader _reader;
        private FakeLocator _locator;
        private FakeSaver _saver;
        private CountingProgress _progress;
        private MemoryPreferenceStore _preferences;
        private ValidatorPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeView();
            _reader = new FakeReader().With(Records.SceneObject("Enemy", Records.Transform(), ComponentRecord.MissingScript()));
            _locator = new FakeLocator();
            _saver = new FakeSaver();
            _progress = new CountingProgress();
            _preferences = new MemoryPreferenceStore();
            _presenter = Create();
        }

        private ValidatorPresenter Create()
        {
            var scanner = new ValidationScanner(_reader, () => ValidationPreferences.Rules(_preferences));
            return new ValidatorPresenter(_view, scanner, _preferences, _locator, _saver, () => _progress);
        }

        [Test]
        public void It_opens_on_the_project_scope_with_nothing_checked()
        {
            Assert.That(_view.Scope, Is.EqualTo(ValidationScopes.Project));
            Assert.That(_view.Report, Is.Null);
            Assert.That(_view.Status, Is.EqualTo("Nothing checked yet."));
        }

        [Test]
        public void Validate_scans_the_current_scope_and_shows_the_report()
        {
            _view.ClickValidate();

            Assert.That(_reader.LastScope, Is.EqualTo(ValidationScopes.Project));
            Assert.That(_view.Report.Findings, Has.Count.EqualTo(1));
            Assert.That(_view.Status, Does.Contain("1 errors"));
            Assert.That(_view.Status, Does.Contain("build scenes"));
            Assert.That(_progress.DoneCalls, Is.EqualTo(1));
        }

        [Test]
        public void Toggling_changes_the_scope_and_remembers_it()
        {
            _view.Toggle(ValidationScope.Prefabs);
            _view.Toggle(ValidationScope.OpenScenes);

            ValidationScope expected = ValidationScope.OpenScenes | ValidationScope.BuildScenes | ValidationScope.Assets;
            Assert.That(_view.Scope, Is.EqualTo(expected));

            // A window opened later starts where this one was left.
            _presenter.Dispose();
            _view = new FakeView();
            _presenter = Create();
            Assert.That(_view.Scope, Is.EqualTo(expected));
        }

        [Test]
        public void The_last_part_of_the_scope_cannot_be_switched_off()
        {
            _view.Toggle(ValidationScope.Prefabs);
            _view.Toggle(ValidationScope.Assets);
            _view.Toggle(ValidationScope.BuildScenes);

            Assert.That(_view.Scope, Is.EqualTo(ValidationScope.BuildScenes));
            Assert.That(_view.Status, Does.Contain("At least one"));
        }

        [Test]
        public void The_shortcut_scans_another_scope_without_changing_the_windows_own()
        {
            _presenter.Scan(ValidationScope.OpenScenes);

            Assert.That(_reader.LastScope, Is.EqualTo(ValidationScope.OpenScenes));
            Assert.That(_view.Scope, Is.EqualTo(ValidationScopes.Project));
        }

        [Test]
        public void Choosing_a_finding_reveals_it()
        {
            _view.ClickValidate();
            Finding finding = _view.Report.Findings[0];

            _view.Choose(finding);

            Assert.That(_locator.Revealed, Is.SameAs(finding));
        }

        [Test]
        public void Saving_needs_a_report_first()
        {
            _view.ClickSave();
            Assert.That(_saver.Saved, Is.Null);
            Assert.That(_view.Status, Does.Contain("no report"));

            _view.ClickValidate();
            _view.ClickSave();
            Assert.That(_saver.Saved, Is.SameAs(_view.Report));
            Assert.That(_view.Status, Is.EqualTo("Saved Logs/DevKit/report.json"));
        }

        [Test]
        public void A_rule_switched_off_in_the_preferences_is_not_run_by_the_next_scan()
        {
            ValidationPreferences.MissingScripts.Set(_preferences, false);

            _view.ClickValidate();

            Assert.That(_view.Report.Findings, Is.Empty);
        }

        [Test]
        public void After_dispose_the_view_is_no_longer_listened_to()
        {
            _presenter.Dispose();

            _view.ClickValidate();

            Assert.That(_reader.Reads, Is.EqualTo(0));
        }
    }

    public sealed class CommandTests
    {
        private readonly List<string> _log = new List<string>();

        private int Run(FakeReader reader, params string[] arguments)
        {
            _log.Clear();
            return ValidatorCommand.Execute(new CommandLineArguments(arguments), reader, _log.Add);
        }

        private static FakeReader BrokenProject()
        {
            return new FakeReader().With(
                Records.SceneObject("Enemy", Records.Transform(), ComponentRecord.MissingScript()),
                Records.SceneObject("Gate", Records.Transform(), Records.Script("Spawner", Records.Reference("_prefab", ReferenceState.None))));
        }

        [Test]
        public void A_clean_project_exits_with_zero()
        {
            Assert.That(Run(new FakeReader()), Is.EqualTo(ExitCode.Clean));
        }

        [Test]
        public void An_error_fails_the_run_by_default()
        {
            Assert.That(Run(BrokenProject()), Is.EqualTo(ExitCode.Findings));
            Assert.That(string.Join("\n", _log), Does.Contain("ERROR missing-script"));
        }

        [Test]
        public void Only_the_named_checks_run()
        {
            Assert.That(Run(BrokenProject(), "-devkitRules", "unassigned-reference"), Is.EqualTo(ExitCode.Clean));
            Assert.That(Run(BrokenProject(), "-devkitRules", "unassigned-reference", "-devkitFailOn", "warning"), Is.EqualTo(ExitCode.Findings));
        }

        [Test]
        public void Optional_fields_can_be_named_on_the_command_line()
        {
            int code = Run(BrokenProject(), "-devkitRules", "unassigned-reference", "-devkitFailOn", "warning", "-devkitOptionalFields", "_prefab");

            Assert.That(code, Is.EqualTo(ExitCode.Clean));
        }

        [Test]
        public void The_scope_is_passed_to_the_reader()
        {
            FakeReader reader = BrokenProject();

            Run(reader, "-devkitScope", "open,prefabs");

            Assert.That(reader.LastScope, Is.EqualTo(ValidationScope.OpenScenes | ValidationScope.Prefabs));
        }

        [Test]
        public void A_bad_scope_or_check_name_stops_before_anything_is_read()
        {
            FakeReader reader = BrokenProject();

            Assert.That(Run(reader, "-devkitScope", "everything"), Is.EqualTo(ExitCode.CouldNotRun));
            Assert.That(_log[0], Does.Contain("'everything'"));
            Assert.That(Run(reader, "-devkitRules", "missing-everything"), Is.EqualTo(ExitCode.CouldNotRun));
            Assert.That(reader.Reads, Is.EqualTo(0));
        }
    }
}
