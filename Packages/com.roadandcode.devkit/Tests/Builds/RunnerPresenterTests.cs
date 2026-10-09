using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Builds.Tests
{
    public sealed class RunnerPresenterTests
    {
        private static readonly DateTime Noon = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private FakeRunnerView _view;
        private FakePlanStore _store;
        private FakeProject _project;
        private FakeBuilder _builder;
        private MemoryHistory _history;
        private FakeRevealer _revealer;
        private MemoryPreferenceStore _preferences;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeRunnerView();
            _store = new FakePlanStore { Plan = Plans.Default() };
            _project = new FakeProject();
            _builder = new FakeBuilder();
            _history = new MemoryHistory();
            _revealer = new FakeRevealer();
            _preferences = new MemoryPreferenceStore();
            _now = Noon.AddMinutes(5);
        }

        private BuildRunnerPresenter Create()
        {
            return new BuildRunnerPresenter(_view, _store, _project, new BuildSession(_project, _builder, _history), _history, _revealer,
                _preferences, () => _now);
        }

        [Test]
        public void Without_a_plan_it_offers_to_create_one()
        {
            _store.Plan = null;
            Create();

            Assert.That(_view.HasPlan, Is.False);
            Assert.That(_view.Status, Does.Contain("no build plan"));

            _view.ClickCreatePlan();

            Assert.That(_view.HasPlan, Is.True);
            Assert.That(_view.Profiles, Has.Count.EqualTo(3));
        }

        [Test]
        public void Each_profile_is_listed_with_where_it_builds_to()
        {
            Create();

            Assert.That(_view.Profiles[0].Name, Is.EqualTo("Web"));
            Assert.That(_view.Profiles[0].Platform, Is.EqualTo("Web"));
            Assert.That(_view.Profiles[0].Location, Is.EqualTo("Builds/Web"));
            Assert.That(_view.Profiles[1].Location, Is.EqualTo("Builds/Windows/TacticaAI.exe"));
            Assert.That(_view.Profiles[0].LastResult, Is.EqualTo("never built here"));
            Assert.That(_view.Profiles[0].LastSucceeded, Is.Null);
            Assert.That(_view.Findings, Is.Empty);
            Assert.That(_view.Status, Is.EqualTo("3 of 3 profiles enabled, 1 scenes in the build"));
        }

        [Test]
        public void Problems_with_the_plan_are_listed_before_anything_is_built()
        {
            _project.Installed.Remove(BuildPlatform.Android);
            Create();

            Assert.That(_view.Findings, Has.Count.EqualTo(1));
            Assert.That(_view.Findings[0].Rule, Is.EqualTo("platform-not-installed"));
        }

        [Test]
        public void Switching_a_profile_off_saves_the_plan_and_drops_its_problems()
        {
            _project.Installed.Remove(BuildPlatform.Android);
            Create();

            _view.Toggle(2, false);

            Assert.That(_store.Saves, Is.EqualTo(1));
            Assert.That(_view.Profiles[2].Enabled, Is.False);
            Assert.That(_view.Findings, Is.Empty);
            Assert.That(_view.Status, Does.StartWith("2 of 3 profiles enabled"));
        }

        [Test]
        public void Build_enabled_builds_only_what_is_switched_on()
        {
            Create();
            _view.Toggle(2, false);

            _view.ClickBuildEnabled();

            Assert.That(_builder.Requests.ConvertAll(request => request.Profile.Name), Is.EqualTo(new[] { "Windows", "Web" }));
            Assert.That(_view.Status, Is.EqualTo("2 of 2 builds succeeded in 0 s"));
            Assert.That(_view.Findings, Has.Count.EqualTo(2));
            Assert.That(_view.Profiles[0].LastResult, Is.EqualTo("built 5 min ago"));
            Assert.That(_view.Profiles[0].LastSucceeded, Is.True);
            Assert.That(_view.Profiles[2].LastSucceeded, Is.Null);
        }

        [Test]
        public void One_profile_can_be_built_on_its_own_even_when_it_is_switched_off()
        {
            Create();
            _view.Toggle(0, false);

            _view.ClickBuild(0);

            Assert.That(_builder.Requests, Has.Count.EqualTo(1));
            Assert.That(_builder.Requests[0].Location, Is.EqualTo("Builds/Web"));
        }

        [Test]
        public void A_failed_build_is_shown_as_failed_and_as_an_error()
        {
            _builder.Failing.Add("Web");
            Create();

            _view.ClickBuild(0);

            Assert.That(_view.Profiles[0].LastSucceeded, Is.False);
            Assert.That(_view.Profiles[0].LastResult, Does.StartWith("failed"));
            Assert.That(_view.Findings[0].Severity, Is.EqualTo(Severity.Error));
            Assert.That(_view.Status, Is.EqualTo("0 of 1 builds succeeded in 0 s"));
        }

        [Test]
        public void An_error_in_the_plan_blocks_the_build_and_says_so()
        {
            _project.Scenes.Clear();
            Create();

            _view.ClickBuildEnabled();

            Assert.That(_builder.Requests, Is.Empty);
            Assert.That(_view.Status, Does.StartWith("Nothing was built"));
        }

        [Test]
        public void Show_opens_the_last_output_or_says_there_is_none()
        {
            Create();

            _view.ClickShow(0);
            Assert.That(_revealer.Revealed, Is.Null);
            Assert.That(_view.Status, Does.Contain("has not been built"));

            _view.ClickBuild(0);
            _view.ClickShow(0);
            Assert.That(_revealer.Revealed, Is.EqualTo("Builds/Web"));
        }

        [Test]
        public void The_output_is_shown_after_a_build_only_when_the_preference_asks()
        {
            Create();

            _view.ClickBuild(0);
            Assert.That(_revealer.Revealed, Is.Null);

            BuildPreferences.RevealWhenDone.Set(_preferences, true);
            _view.ClickBuild(1);
            Assert.That(_revealer.Revealed, Is.EqualTo("Builds/Windows/TacticaAI.exe"));
        }

        [Test]
        public void Edit_plan_selects_the_asset_and_refresh_reads_it_again()
        {
            Create();

            _view.ClickEditPlan();
            Assert.That(_store.Selected, Is.SameAs(_store.Plan));

            _store.Plan = Plans.With(new BuildProfile("Only", BuildPlatform.Web, "Out"));
            _view.ClickRefresh();
            Assert.That(_view.Profiles, Has.Count.EqualTo(1));
        }

        [Test]
        public void How_long_ago_reads_in_minutes_hours_and_days()
        {
            _history.Record(new BuildOutcome("Web", true, "Builds/Web", 1, 1f, 0, 0, Noon));
            Create();

            _now = Noon.AddSeconds(20);
            _view.ClickRefresh();
            Assert.That(_view.Profiles[0].LastResult, Is.EqualTo("built just now"));

            _now = Noon.AddHours(3);
            _view.ClickRefresh();
            Assert.That(_view.Profiles[0].LastResult, Is.EqualTo("built 3 h ago"));

            _now = Noon.AddDays(2);
            _view.ClickRefresh();
            Assert.That(_view.Profiles[0].LastResult, Is.EqualTo("built 2 d ago"));
        }

        [Test]
        public void After_dispose_the_view_is_no_longer_listened_to()
        {
            BuildRunnerPresenter presenter = Create();
            presenter.Dispose();

            _view.ClickBuildEnabled();

            Assert.That(_builder.Requests, Is.Empty);
        }
    }

    public sealed class BuildCommandTests
    {
        private readonly List<string> _log = new List<string>();
        private FakeProject _project;
        private FakeBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _project = new FakeProject();
            _builder = new FakeBuilder();
            _log.Clear();
        }

        private int Run(BuildPlan plan, params string[] arguments)
        {
            var session = new BuildSession(_project, _builder, new MemoryHistory());
            return BuildCommand.Execute(new CommandLineArguments(arguments), plan, session, _log.Add);
        }

        [Test]
        public void Without_a_plan_the_command_cannot_run()
        {
            Assert.That(Run(null), Is.EqualTo(ExitCode.CouldNotRun));
            Assert.That(_log[0], Does.Contain("No build plan"));
        }

        [Test]
        public void By_default_every_enabled_profile_is_built()
        {
            BuildPlan plan = Plans.Default();
            plan.Profiles[2].Enabled = false;

            Assert.That(Run(plan), Is.EqualTo(ExitCode.Clean));
            Assert.That(_builder.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public void Named_profiles_are_built_whether_enabled_or_not()
        {
            BuildPlan plan = Plans.Default();
            plan.Profiles[0].Enabled = false;

            Assert.That(Run(plan, "-devkitProfiles", "web"), Is.EqualTo(ExitCode.Clean));
            Assert.That(_builder.Requests, Has.Count.EqualTo(1));
            Assert.That(_builder.Requests[0].Profile.Name, Is.EqualTo("Web"));
        }

        [Test]
        public void An_unknown_profile_name_stops_the_run()
        {
            Assert.That(Run(Plans.Default(), "-devkitProfiles", "Web,Switch"), Is.EqualTo(ExitCode.CouldNotRun));
            Assert.That(_log[0], Does.Contain("'Switch'"));
            Assert.That(_builder.Requests, Is.Empty);
        }

        [Test]
        public void A_failed_build_exits_with_one_and_a_blocked_run_with_two()
        {
            _builder.Failing.Add("Web");
            Assert.That(Run(Plans.Default(), "-devkitProfiles", "Web"), Is.EqualTo(ExitCode.Findings));

            _project.Scenes.Clear();
            Assert.That(Run(Plans.Default(), "-devkitProfiles", "Web"), Is.EqualTo(ExitCode.CouldNotRun));
        }
    }
}
