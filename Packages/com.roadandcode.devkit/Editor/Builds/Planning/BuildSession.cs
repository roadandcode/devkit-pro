using System;
using System.Collections.Generic;
using System.Globalization;
using RoadAndCode.DevKit.Common;
using UnityEngine;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>One build, ready to hand to Unity.</summary>
    public sealed class BuildRequest
    {
        public BuildRequest(BuildProfile profile, string location, IReadOnlyList<string> scenes)
        {
            Profile = profile;
            Location = location;
            Scenes = scenes;
        }

        public BuildProfile Profile { get; }

        public string Location { get; }

        public IReadOnlyList<string> Scenes { get; }
    }

    /// <summary>How one build ended. Serializable so the last results survive a script reload.</summary>
    [Serializable]
    public sealed class BuildOutcome
    {
        [SerializeField] private string _profile;
        [SerializeField] private bool _succeeded;
        [SerializeField] private string _location;
        [SerializeField] private long _sizeBytes;
        [SerializeField] private float _seconds;
        [SerializeField] private int _errors;
        [SerializeField] private int _warnings;
        [SerializeField] private string _finishedUtc;

        public BuildOutcome(string profile, bool succeeded, string location, long sizeBytes, float seconds, int errors, int warnings, DateTime finishedUtc)
        {
            _profile = profile;
            _succeeded = succeeded;
            _location = location;
            _sizeBytes = sizeBytes;
            _seconds = seconds;
            _errors = errors;
            _warnings = warnings;
            _finishedUtc = finishedUtc.ToString("o", CultureInfo.InvariantCulture);
        }

        public string Profile => _profile;

        public bool Succeeded => _succeeded;

        public string Location => _location;

        public long SizeBytes => _sizeBytes;

        public float Seconds => _seconds;

        public int Errors => _errors;

        public int Warnings => _warnings;

        public DateTime FinishedUtc =>
            DateTime.TryParse(_finishedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime time) ? time : DateTime.MinValue;

        public string Describe()
        {
            string size = (_sizeBytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture);
            string time = _seconds.ToString("0", CultureInfo.InvariantCulture);
            return _succeeded
                ? $"'{_profile}' built in {time} s, {size} MB, {_warnings} warnings"
                : $"'{_profile}' failed after {time} s with {_errors} errors. The editor log has the details";
        }
    }

    /// <summary>Makes the player. The planner owns this; the editor implements it with BuildPipeline.</summary>
    public interface IPlayerBuilder
    {
        BuildOutcome Build(BuildRequest request);
    }

    public interface IProjectInfo
    {
        ProjectFacts Read();
    }

    /// <summary>Remembers how the last build of each profile went.</summary>
    public interface IBuildHistory
    {
        BuildOutcome Last(string profile);

        void Record(BuildOutcome outcome);
    }

    public sealed class BuildRunResult
    {
        public BuildRunResult(IReadOnlyList<Finding> problems, IReadOnlyList<BuildOutcome> outcomes, TimeSpan duration)
        {
            Problems = problems;
            Outcomes = outcomes;
            Duration = duration;
        }

        /// <summary>What the check before building found. Any error here means nothing was built.</summary>
        public IReadOnlyList<Finding> Problems { get; }

        public IReadOnlyList<BuildOutcome> Outcomes { get; }

        public TimeSpan Duration { get; }

        public bool AllSucceeded
        {
            get
            {
                foreach (BuildOutcome outcome in Outcomes)
                {
                    if (!outcome.Succeeded) return false;
                }

                return Outcomes.Count > 0;
            }
        }

        public int SucceededCount
        {
            get
            {
                int succeeded = 0;
                foreach (BuildOutcome outcome in Outcomes)
                {
                    if (outcome.Succeeded) succeeded++;
                }

                return succeeded;
            }
        }

        /// <summary>One finding per build: a note for a success, an error for a failure.</summary>
        public List<Finding> OutcomeFindings()
        {
            var findings = new List<Finding>(Outcomes.Count);
            foreach (BuildOutcome outcome in Outcomes)
            {
                findings.Add(new Finding(outcome.Succeeded ? "build-succeeded" : "build-failed", outcome.Succeeded ? Severity.Info : Severity.Error,
                    outcome.Describe(), outcome.Location, outcome.Profile));
            }

            return findings;
        }

        /// <summary>The run as a report: a line per build, after whatever the check found.</summary>
        public ScanReport ToReport()
        {
            var findings = new List<Finding>(Problems);
            findings.AddRange(OutcomeFindings());

            var facts = new List<ReportFact>
            {
                new ReportFact("Builds", Outcomes.Count.ToString()),
                new ReportFact("Succeeded", SucceededCount.ToString()),
            };
            return new ScanReport(BuildSession.ToolName, findings, facts, Duration);
        }
    }

    /// <summary>Checks a set of profiles, orders them and builds them one after another.</summary>
    public sealed class BuildSession
    {
        public const string ToolName = "Build Runner";

        private readonly IProjectInfo _project;
        private readonly IPlayerBuilder _builder;
        private readonly IBuildHistory _history;

        public BuildSession(IProjectInfo project, IPlayerBuilder builder, IBuildHistory history)
        {
            _project = project;
            _builder = builder;
            _history = history;
        }

        public BuildRunResult Run(IReadOnlyList<BuildProfile> profiles, bool stopOnFailure)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ProjectFacts project = _project.Read();
            List<Finding> problems = BuildPlanValidator.Check(profiles, project);
            var outcomes = new List<BuildOutcome>();
            if (BuildPlanValidator.HasErrors(problems)) return new BuildRunResult(problems, outcomes, watch.Elapsed);

            foreach (BuildProfile profile in Order(profiles, project.ActivePlatform))
            {
                OutputPaths.TryResolve(profile, project, out string location, out _);
                BuildOutcome outcome = _builder.Build(new BuildRequest(profile, location, project.EnabledScenes));
                _history.Record(outcome);
                outcomes.Add(outcome);
                if (!outcome.Succeeded && stopOnFailure) break;
            }

            return new BuildRunResult(problems, outcomes, watch.Elapsed);
        }

        /// <summary>
        /// The platform the editor is already on goes first and each other platform's profiles stay
        /// together, so the editor switches platform as few times as possible.
        /// </summary>
        public static List<BuildProfile> Order(IReadOnlyList<BuildProfile> profiles, BuildPlatform? active)
        {
            var platforms = new List<BuildPlatform>();
            if (active.HasValue) platforms.Add(active.Value);
            foreach (BuildProfile profile in profiles)
            {
                if (!platforms.Contains(profile.Platform)) platforms.Add(profile.Platform);
            }

            var ordered = new List<BuildProfile>(profiles.Count);
            foreach (BuildPlatform platform in platforms)
            {
                foreach (BuildProfile profile in profiles)
                {
                    if (profile.Platform == platform) ordered.Add(profile);
                }
            }

            return ordered;
        }
    }
}
