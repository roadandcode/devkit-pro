using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Builds.Tests
{
    public sealed class OutputPathTests
    {
        private static readonly ProjectFacts Project = new FakeProject().Read();

        private static string Resolve(BuildProfile profile)
        {
            Assert.That(OutputPaths.TryResolve(profile, Project, out string location, out string problem), Is.True, problem);
            return location;
        }

        [Test]
        public void A_web_build_goes_to_the_folder_as_written()
        {
            Assert.That(Resolve(new BuildProfile("Web", BuildPlatform.Web, "Builds/Web")), Is.EqualTo("Builds/Web"));
            Assert.That(Resolve(new BuildProfile("Web", BuildPlatform.Web, "Builds\\WebGL\\")), Is.EqualTo("Builds/WebGL"));
        }

        [Test]
        public void Tokens_are_filled_in_and_the_product_name_loses_what_does_not_belong_in_a_file_name()
        {
            var profile = new BuildProfile("Store Build", BuildPlatform.Web, "Builds/{platform}/{product}-{version}-{profile}");

            Assert.That(Resolve(profile), Is.EqualTo("Builds/Web/TacticaAI-0.1.0-StoreBuild"));
        }

        [Test]
        public void A_windows_or_android_folder_gets_a_file_named_after_the_product()
        {
            Assert.That(Resolve(new BuildProfile("Windows", BuildPlatform.Windows, "Builds/Windows")), Is.EqualTo("Builds/Windows/TacticaAI.exe"));
            Assert.That(Resolve(new BuildProfile("Android", BuildPlatform.Android, "Builds/Android/")), Is.EqualTo("Builds/Android/TacticaAI.apk"));
        }

        [Test]
        public void A_path_that_already_names_the_file_is_kept()
        {
            Assert.That(Resolve(new BuildProfile("Windows", BuildPlatform.Windows, "Builds/Windows/{product}.exe")), Is.EqualTo("Builds/Windows/TacticaAI.exe"));
            Assert.That(Resolve(new BuildProfile("Windows", BuildPlatform.Windows, "Out/Game.EXE")), Is.EqualTo("Out/Game.EXE"));
        }

        [Test]
        public void An_app_bundle_is_an_aab()
        {
            BuildProfile profile = new BuildProfile("Store", BuildPlatform.Android, "Builds/Android").WithAppBundle(true);

            Assert.That(Resolve(profile), Is.EqualTo("Builds/Android/TacticaAI.aab"));
        }

        [Test]
        public void An_empty_path_and_an_unknown_token_are_refused_with_the_reason()
        {
            Assert.That(OutputPaths.TryResolve(new BuildProfile("Web", BuildPlatform.Web, "  "), Project, out _, out string empty), Is.False);
            Assert.That(empty, Is.EqualTo("has no output path"));

            Assert.That(OutputPaths.TryResolve(new BuildProfile("Web", BuildPlatform.Web, "Builds/{date}"), Project, out _, out string token), Is.False);
            Assert.That(token, Is.EqualTo("uses an unknown token '{date}'"));
        }

        [TestCase("Assets", true)]
        [TestCase("Assets/Builds/Web", true)]
        [TestCase("assets/Builds", true)]
        [TestCase("AssetsBackup/Web", false)]
        [TestCase("Builds/Assets", false)]
        public void Building_into_assets_is_recognised(string path, bool inside)
        {
            Assert.That(OutputPaths.IsInsideAssets(path), Is.EqualTo(inside));
        }
    }

    public sealed class PlanValidatorTests
    {
        private static List<Finding> Check(FakeProject project, params BuildProfile[] profiles) => BuildPlanValidator.Check(profiles, project.Read());

        private static BuildProfile Web(string name = "Web", string path = "Builds/Web") => new BuildProfile(name, BuildPlatform.Web, path);

        [Test]
        public void The_default_plan_is_clean()
        {
            Assert.That(BuildPlanValidator.Check(Plans.Default().Profiles, new FakeProject().Read()), Is.Empty);
        }

        [Test]
        public void Nothing_enabled_and_no_scenes_are_errors()
        {
            var project = new FakeProject();
            project.Scenes.Clear();

            List<Finding> findings = Check(project);

            Assert.That(findings.ConvertAll(finding => finding.Rule), Is.EqualTo(new[] { "no-profiles", "no-scenes" }));
            Assert.That(BuildPlanValidator.HasErrors(findings), Is.True);
        }

        [Test]
        public void A_platform_without_build_support_is_an_error()
        {
            var project = new FakeProject();
            project.Installed.Remove(BuildPlatform.Android);

            List<Finding> findings = Check(project, Web(), new BuildProfile("Android", BuildPlatform.Android, "Builds/Android"));

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Rule, Is.EqualTo("platform-not-installed"));
            Assert.That(findings[0].ObjectPath, Is.EqualTo("Android"));
        }

        [Test]
        public void Two_profiles_building_to_one_place_is_an_error()
        {
            List<Finding> findings = Check(new FakeProject(), Web("Web"), Web("Web Development", "builds/web/"));

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Rule, Is.EqualTo("duplicate-output"));
            Assert.That(findings[0].Message, Does.Contain("'Web Development' and 'Web'"));
        }

        [Test]
        public void Building_into_assets_is_an_error_and_a_bad_path_is_named()
        {
            List<Finding> findings = Check(new FakeProject(), Web("Inside", "Assets/Builds"), Web("Broken", "Builds/{when}"));

            Assert.That(findings.ConvertAll(finding => finding.Rule), Is.EqualTo(new[] { "output-in-assets", "bad-output-path" }));
            Assert.That(findings[1].Message, Is.EqualTo("'Broken' uses an unknown token '{when}'"));
        }

        [Test]
        public void An_absolute_path_is_only_a_note_and_a_repeated_name_only_a_warning()
        {
            List<Finding> findings = Check(new FakeProject(), Web("Web", "C:/Builds/Web"), Web("Web", "Builds/Other"));

            Assert.That(findings.ConvertAll(finding => finding.Rule), Is.EqualTo(new[] { "absolute-output", "duplicate-name" }));
            Assert.That(BuildPlanValidator.HasErrors(findings), Is.False);
        }
    }

    public sealed class SessionTests
    {
        private FakeProject _project;
        private FakeBuilder _builder;
        private MemoryHistory _history;
        private BuildSession _session;

        [SetUp]
        public void SetUp()
        {
            _project = new FakeProject();
            _builder = new FakeBuilder();
            _history = new MemoryHistory();
            _session = new BuildSession(_project, _builder, _history);
        }

        [Test]
        public void The_platform_the_editor_is_on_is_built_first_and_platforms_stay_together()
        {
            var profiles = new[]
            {
                new BuildProfile("Web", BuildPlatform.Web, "Builds/Web"),
                new BuildProfile("Android", BuildPlatform.Android, "Builds/Android"),
                new BuildProfile("Windows", BuildPlatform.Windows, "Builds/Windows"),
                new BuildProfile("Web Debug", BuildPlatform.Web, "Builds/WebDebug"),
            };

            List<BuildProfile> ordered = BuildSession.Order(profiles, BuildPlatform.Windows);

            Assert.That(ordered.ConvertAll(profile => profile.Name), Is.EqualTo(new[] { "Windows", "Web", "Web Debug", "Android" }));
        }

        [Test]
        public void Without_a_known_active_platform_the_plans_own_order_is_kept()
        {
            List<BuildProfile> ordered = BuildSession.Order(Plans.Default().Profiles, null);

            Assert.That(ordered.ConvertAll(profile => profile.Name), Is.EqualTo(new[] { "Web", "Windows", "Android" }));
        }

        [Test]
        public void Each_profile_is_built_to_its_resolved_location_with_the_enabled_scenes()
        {
            BuildRunResult result = _session.Run(Plans.Default().Profiles, stopOnFailure: true);

            Assert.That(result.Outcomes, Has.Count.EqualTo(3));
            Assert.That(result.AllSucceeded, Is.True);
            Assert.That(_builder.Requests[0].Location, Is.EqualTo("Builds/Windows/TacticaAI.exe"));
            Assert.That(_builder.Requests[1].Location, Is.EqualTo("Builds/Web"));
            Assert.That(_builder.Requests[1].Profile.WebCompression, Is.EqualTo(WebCompression.Gzip));
            Assert.That(_builder.Requests[1].Profile.WebDecompressionFallback, Is.True);
            Assert.That(_builder.Requests[2].Scenes, Is.EqualTo(new[] { "Assets/Scenes/Bootstrap.unity" }));
        }

        [Test]
        public void An_error_in_the_plan_means_nothing_is_built()
        {
            _project.Scenes.Clear();

            BuildRunResult result = _session.Run(Plans.Default().Profiles, stopOnFailure: true);

            Assert.That(_builder.Requests, Is.Empty);
            Assert.That(result.Outcomes, Is.Empty);
            Assert.That(result.AllSucceeded, Is.False);
            Assert.That(result.Problems[0].Rule, Is.EqualTo("no-scenes"));
        }

        [Test]
        public void A_failure_stops_the_run_or_not_as_the_plan_says()
        {
            _builder.Failing.Add("Windows");

            Assert.That(_session.Run(Plans.Default().Profiles, stopOnFailure: true).Outcomes, Has.Count.EqualTo(1));
            Assert.That(_session.Run(Plans.Default().Profiles, stopOnFailure: false).Outcomes, Has.Count.EqualTo(3));
        }

        [Test]
        public void Every_result_is_remembered()
        {
            _builder.Failing.Add("Web");

            _session.Run(Plans.Default().Profiles, stopOnFailure: false);

            Assert.That(_history.Last("Windows").Succeeded, Is.True);
            Assert.That(_history.Last("Web").Succeeded, Is.False);
            Assert.That(_history.Last("Never"), Is.Null);
        }

        [Test]
        public void The_report_has_a_line_per_build_and_fails_on_a_failed_build()
        {
            _builder.Failing.Add("Android");

            ScanReport report = _session.Run(Plans.Default().Profiles, stopOnFailure: true).ToReport();

            Assert.That(report.Tool, Is.EqualTo("Build Runner"));
            Assert.That(report.Facts[0].Value, Is.EqualTo("3"));
            Assert.That(report.Facts[1].Value, Is.EqualTo("2"));
            Assert.That(report.Findings[0].Rule, Is.EqualTo("build-succeeded"));
            Assert.That(report.Findings[0].Message, Is.EqualTo("'Windows' built in 80 s, 5.0 MB, 2 warnings"));
            Assert.That(report.Findings[0].AssetPath, Is.EqualTo("Builds/Windows/TacticaAI.exe"));
            Assert.That(report.Findings[2].Severity, Is.EqualTo(Severity.Error));
            Assert.That(report.Findings[2].Message, Does.StartWith("'Android' failed after 80 s with 3 errors"));
        }

        [Test]
        public void An_outcome_survives_being_written_as_json()
        {
            BuildOutcome outcome = _session.Run(Plans.Default().Profiles, stopOnFailure: true).Outcomes[0];

            BuildOutcome copy = UnityEngine.JsonUtility.FromJson<BuildOutcome>(UnityEngine.JsonUtility.ToJson(outcome));

            Assert.That(copy.Profile, Is.EqualTo("Windows"));
            Assert.That(copy.Succeeded, Is.True);
            Assert.That(copy.SizeBytes, Is.EqualTo(outcome.SizeBytes));
            Assert.That(copy.FinishedUtc, Is.EqualTo(outcome.FinishedUtc));
        }
    }
}
