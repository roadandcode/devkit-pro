using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;
using UnityEngine;

namespace RoadAndCode.DevKit.Builds.Tests
{
    internal sealed class FakeProject : IProjectInfo
    {
        public string ProductName { get; set; } = "Tactica AI";

        public string Version { get; set; } = "0.1.0";

        public List<string> Scenes { get; } = new List<string> { "Assets/Scenes/Bootstrap.unity" };

        public HashSet<BuildPlatform> Installed { get; } = new HashSet<BuildPlatform> { BuildPlatform.Web, BuildPlatform.Windows, BuildPlatform.Android };

        public BuildPlatform? Active { get; set; } = BuildPlatform.Windows;

        public ProjectFacts Read() => new ProjectFacts(ProductName, Version, Scenes, Installed, Active);
    }

    internal sealed class FakeBuilder : IPlayerBuilder
    {
        public List<BuildRequest> Requests { get; } = new List<BuildRequest>();

        /// <summary>Profiles whose build should fail.</summary>
        public HashSet<string> Failing { get; } = new HashSet<string>();

        public BuildOutcome Build(BuildRequest request)
        {
            Requests.Add(request);
            bool succeeded = !Failing.Contains(request.Profile.Name);
            return new BuildOutcome(request.Profile.Name, succeeded, request.Location, 5 * 1024 * 1024, 80f, succeeded ? 0 : 3, 2,
                new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        }
    }

    internal sealed class MemoryHistory : IBuildHistory
    {
        private readonly Dictionary<string, BuildOutcome> _outcomes = new Dictionary<string, BuildOutcome>();

        public BuildOutcome Last(string profile) => _outcomes.TryGetValue(profile, out BuildOutcome outcome) ? outcome : null;

        public void Record(BuildOutcome outcome) => _outcomes[outcome.Profile] = outcome;
    }

    internal sealed class FakePlanStore : IBuildPlanStore
    {
        public BuildPlan Plan { get; set; }

        public int Saves { get; private set; }

        public BuildPlan Selected { get; private set; }

        public BuildPlan Find() => Plan;

        public BuildPlan Create()
        {
            Plan = Plans.Default();
            return Plan;
        }

        public void Save(BuildPlan plan) => Saves++;

        public void Select(BuildPlan plan) => Selected = plan;
    }

    internal sealed class FakeRevealer : IOutputRevealer
    {
        public string Revealed { get; private set; }

        public void Reveal(string location) => Revealed = location;
    }

    internal sealed class FakeRunnerView : IBuildRunnerView
    {
        public event Action CreatePlanRequested;

        public event Action EditPlanRequested;

        public event Action RefreshRequested;

        public event Action BuildEnabledRequested;

        public event Action<int> BuildOneRequested;

        public event Action<int, bool> ProfileToggled;

        public event Action<int> RevealRequested;

        public bool HasPlan { get; private set; }

        public IReadOnlyList<ProfileRow> Profiles { get; private set; }

        public IReadOnlyList<Finding> Findings { get; private set; }

        public string Status { get; private set; }

        public void ShowNoPlan() => HasPlan = false;

        public void ShowPlan(IReadOnlyList<ProfileRow> profiles, IReadOnlyList<Finding> findings)
        {
            HasPlan = true;
            Profiles = profiles;
            Findings = findings;
        }

        public void ShowStatus(string text) => Status = text;

        public void ClickCreatePlan() => CreatePlanRequested?.Invoke();

        public void ClickEditPlan() => EditPlanRequested?.Invoke();

        public void ClickRefresh() => RefreshRequested?.Invoke();

        public void ClickBuildEnabled() => BuildEnabledRequested?.Invoke();

        public void ClickBuild(int index) => BuildOneRequested?.Invoke(index);

        public void Toggle(int index, bool enabled) => ProfileToggled?.Invoke(index, enabled);

        public void ClickShow(int index) => RevealRequested?.Invoke(index);
    }

    internal static class Plans
    {
        public static BuildPlan Default()
        {
            var plan = ScriptableObject.CreateInstance<BuildPlan>();
            plan.AddDefaults();
            return plan;
        }

        public static BuildPlan With(params BuildProfile[] profiles)
        {
            var plan = ScriptableObject.CreateInstance<BuildPlan>();
            foreach (BuildProfile profile in profiles) plan.Add(profile);
            return plan;
        }
    }
}
