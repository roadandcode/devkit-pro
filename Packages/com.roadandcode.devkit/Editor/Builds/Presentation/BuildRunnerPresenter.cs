using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>One profile as the window lists it.</summary>
    public sealed class ProfileRow
    {
        public ProfileRow(string name, string platform, bool enabled, string location, string lastResult, bool? lastSucceeded)
        {
            Name = name;
            Platform = platform;
            Enabled = enabled;
            Location = location;
            LastResult = lastResult;
            LastSucceeded = lastSucceeded;
        }

        public string Name { get; }

        public string Platform { get; }

        public bool Enabled { get; }

        /// <summary>Where the build goes, or what is wrong with the output path.</summary>
        public string Location { get; }

        public string LastResult { get; }

        /// <summary>Null when the profile has never been built on this machine.</summary>
        public bool? LastSucceeded { get; }
    }

    /// <summary>What the Build Runner window can show and what it reports back.</summary>
    public interface IBuildRunnerView
    {
        event Action CreatePlanRequested;

        event Action EditPlanRequested;

        event Action RefreshRequested;

        event Action BuildEnabledRequested;

        /// <summary>Build just the profile at this index.</summary>
        event Action<int> BuildOneRequested;

        event Action<int, bool> ProfileToggled;

        event Action<int> RevealRequested;

        /// <summary>The project has no build plan yet; offer to create one.</summary>
        void ShowNoPlan();

        void ShowPlan(IReadOnlyList<ProfileRow> profiles, IReadOnlyList<Finding> findings);

        void ShowStatus(string text);
    }

    /// <summary>The Build Runner window's behaviour, with no window in it.</summary>
    public sealed class BuildRunnerPresenter : IDisposable
    {
        private readonly IBuildRunnerView _view;
        private readonly IBuildPlanStore _store;
        private readonly IProjectInfo _project;
        private readonly BuildSession _session;
        private readonly IBuildHistory _history;
        private readonly IOutputRevealer _revealer;
        private readonly IPreferenceStore _preferences;
        private readonly Func<DateTime> _utcNow;

        private BuildPlan _plan;
        private IReadOnlyList<Finding> _lastRun = new Finding[0];

        public BuildRunnerPresenter(IBuildRunnerView view, IBuildPlanStore store, IProjectInfo project, BuildSession session,
            IBuildHistory history, IOutputRevealer revealer, IPreferenceStore preferences, Func<DateTime> utcNow)
        {
            _view = view;
            _store = store;
            _project = project;
            _session = session;
            _history = history;
            _revealer = revealer;
            _preferences = preferences;
            _utcNow = utcNow;

            _view.CreatePlanRequested += OnCreatePlanRequested;
            _view.EditPlanRequested += OnEditPlanRequested;
            _view.RefreshRequested += Refresh;
            _view.BuildEnabledRequested += OnBuildEnabledRequested;
            _view.BuildOneRequested += OnBuildOneRequested;
            _view.ProfileToggled += OnProfileToggled;
            _view.RevealRequested += OnRevealRequested;

            Refresh();
        }

        public void Dispose()
        {
            _view.CreatePlanRequested -= OnCreatePlanRequested;
            _view.EditPlanRequested -= OnEditPlanRequested;
            _view.RefreshRequested -= Refresh;
            _view.BuildEnabledRequested -= OnBuildEnabledRequested;
            _view.BuildOneRequested -= OnBuildOneRequested;
            _view.ProfileToggled -= OnProfileToggled;
            _view.RevealRequested -= OnRevealRequested;
        }

        /// <summary>Reads the plan and the project again. The plan is edited in the Inspector while the window is open.</summary>
        public void Refresh()
        {
            _plan = _store.Find();
            if (_plan == null)
            {
                _view.ShowNoPlan();
                _view.ShowStatus("This project has no build plan yet.");
                return;
            }

            ProjectFacts project = _project.Read();
            var rows = new List<ProfileRow>(_plan.Profiles.Count);
            foreach (BuildProfile profile in _plan.Profiles) rows.Add(Row(profile, project));

            // Problems with what is enabled now, then what the last run from this window said.
            var findings = new List<Finding>(BuildPlanValidator.Check(Enabled(), project));
            findings.AddRange(_lastRun);
            _view.ShowPlan(rows, findings);
            _view.ShowStatus($"{Enabled().Count} of {_plan.Profiles.Count} profiles enabled, {project.EnabledScenes.Count} scenes in the build");
        }

        private ProfileRow Row(BuildProfile profile, ProjectFacts project)
        {
            string location = OutputPaths.TryResolve(profile, project, out string resolved, out string problem) ? resolved : "Output path " + problem;
            BuildOutcome last = _history.Last(profile.Name);
            string result = last == null ? "never built here" : (last.Succeeded ? "built " : "failed ") + Ago(_utcNow() - last.FinishedUtc);
            return new ProfileRow(profile.Name, PlatformTraits.For(profile.Platform).Name, profile.Enabled, location, result, last?.Succeeded);
        }

        private List<BuildProfile> Enabled()
        {
            var enabled = new List<BuildProfile>();
            foreach (BuildProfile profile in _plan.Profiles)
            {
                if (profile.Enabled) enabled.Add(profile);
            }

            return enabled;
        }

        private void OnCreatePlanRequested()
        {
            _store.Create();
            Refresh();
        }

        private void OnEditPlanRequested()
        {
            if (_plan != null) _store.Select(_plan);
        }

        private void OnProfileToggled(int index, bool enabled)
        {
            if (!IsProfile(index)) return;

            _plan.Profiles[index].Enabled = enabled;
            _store.Save(_plan);
            Refresh();
        }

        private void OnBuildEnabledRequested()
        {
            if (_plan != null) Build(Enabled());
        }

        private void OnBuildOneRequested(int index)
        {
            if (IsProfile(index)) Build(new[] { _plan.Profiles[index] });
        }

        private void OnRevealRequested(int index)
        {
            if (!IsProfile(index)) return;

            BuildOutcome last = _history.Last(_plan.Profiles[index].Name);
            if (last == null) _view.ShowStatus($"'{_plan.Profiles[index].Name}' has not been built on this machine yet.");
            else _revealer.Reveal(last.Location);
        }

        private void Build(IReadOnlyList<BuildProfile> profiles)
        {
            BuildRunResult result = _session.Run(profiles, _plan.StopOnFailure);

            // The check's findings are shown fresh by Refresh; keep only what the builds themselves said.
            _lastRun = result.OutcomeFindings();

            Refresh();
            _view.ShowStatus(result.Outcomes.Count == 0
                ? "Nothing was built. Fix the errors in the list first."
                : $"{result.SucceededCount} of {result.Outcomes.Count} builds succeeded in {result.Duration.TotalSeconds:F0} s");

            if (result.AllSucceeded && BuildPreferences.RevealWhenDone.Get(_preferences))
            {
                _revealer.Reveal(result.Outcomes[result.Outcomes.Count - 1].Location);
            }
        }

        private bool IsProfile(int index) => _plan != null && index >= 0 && index < _plan.Profiles.Count;

        private static string Ago(TimeSpan span)
        {
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min ago";
            return span.TotalDays < 1 ? $"{(int)span.TotalHours} h ago" : $"{(int)span.TotalDays} d ago";
        }
    }
}
