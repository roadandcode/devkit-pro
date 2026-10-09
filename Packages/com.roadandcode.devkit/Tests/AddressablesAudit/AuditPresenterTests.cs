using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.AddressablesAudit.Tests
{
    internal sealed class FakeAddressablesReader : IAddressablesReader
    {
        public AddressablesSnapshot Snapshot { get; set; }

        public string Problem { get; set; }

        public AuditOptions LastOptions { get; private set; }

        public int Reads { get; private set; }

        public bool TryRead(AuditOptions options, IProgressSink progress, out AddressablesSnapshot snapshot, out string problem)
        {
            Reads++;
            LastOptions = options;
            snapshot = Snapshot;
            problem = Problem;
            return Problem == null;
        }
    }

    internal sealed class FakeAuditView : IAuditView
    {
        public event Action AuditRequested;

        public event Action<Finding> FindingChosen;

        public event Action SaveRequested;

        public AuditResult Result { get; private set; }

        public string Unavailable { get; private set; }

        public string Status { get; private set; }

        public void ShowResult(AuditResult result)
        {
            Result = result;
            Unavailable = null;
        }

        public void ShowUnavailable(string reason) => Unavailable = reason;

        public void ShowStatus(string text) => Status = text;

        public void ClickAudit() => AuditRequested?.Invoke();

        public void Choose(Finding finding) => FindingChosen?.Invoke(finding);

        public void ClickSave() => SaveRequested?.Invoke();
    }

    internal sealed class FakeLocator : IFindingLocator
    {
        public Finding Revealed { get; private set; }

        public void Reveal(Finding finding) => Revealed = finding;
    }

    internal sealed class FakeSaver : IReportSaver
    {
        public ScanReport Saved { get; private set; }

        public string Save(ScanReport report)
        {
            Saved = report;
            return "Logs/DevKit/audit.json";
        }
    }

    public sealed class AuditPresenterTests
    {
        private FakeAuditView _view;
        private FakeAddressablesReader _reader;
        private FakeLocator _locator;
        private FakeSaver _saver;
        private MemoryPreferenceStore _preferences;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeAuditView();
            _locator = new FakeLocator();
            _saver = new FakeSaver();
            _preferences = new MemoryPreferenceStore();
            _reader = new FakeAddressablesReader
            {
                Snapshot = new SnapshotBuilder()
                    .Group("Orphans", SnapshotBuilder.Entry("old-crate"))
                    .Bundle("Library/com.unity.addressables/aa/WebGL/orphans.bundle", 6)
                    .Build(),
            };
        }

        private AuditPresenter Create(IAddressablesReader reader)
        {
            var runner = new AuditRunner(reader, new Auditor(Auditor.AllRules()));
            return new AuditPresenter(_view, runner, _preferences, _locator, _saver, () => new NullProgress());
        }

        [Test]
        public void Without_the_addressables_package_the_window_says_so()
        {
            Create(null);

            Assert.That(_view.Unavailable, Is.EqualTo(AuditRunner.NotInstalled));

            _view.ClickAudit();
            Assert.That(_view.Result, Is.Null);
            Assert.That(_view.Unavailable, Is.EqualTo(AuditRunner.NotInstalled));
        }

        [Test]
        public void A_project_without_settings_shows_the_readers_reason()
        {
            _reader.Problem = "No settings yet.";
            Create(_reader);

            _view.ClickAudit();

            Assert.That(_view.Unavailable, Is.EqualTo("No settings yet."));
            Assert.That(_view.Result, Is.Null);
        }

        [Test]
        public void Audit_shows_the_groups_and_the_findings()
        {
            Create(_reader);
            Assert.That(_view.Status, Is.EqualTo("Not audited yet."));

            _view.ClickAudit();

            Assert.That(_view.Result.Groups, Has.Count.EqualTo(1));
            Assert.That(_view.Result.Report.Findings, Has.Count.EqualTo(1));
            Assert.That(_view.Result.Report.Findings[0].Rule, Is.EqualTo("unreferenced-group"));
            Assert.That(_view.Status, Does.StartWith("Addressables Audit: 0 errors, 1 warnings"));
        }

        [Test]
        public void The_size_limit_comes_from_the_preferences_at_the_time_of_the_audit()
        {
            Create(_reader);
            AuditPreferences.MaxBundleMegabytes.Set(_preferences, 5f);
            AuditPreferences.IncludePackageAssets.Set(_preferences, true);

            _view.ClickAudit();

            Assert.That(_reader.LastOptions.MaxBundleMegabytes, Is.EqualTo(5f));
            Assert.That(_reader.LastOptions.IncludePackageAssets, Is.True);
            Assert.That(_view.Result.Report.Findings, Has.Some.Matches<Finding>(finding => finding.Rule == "oversized-bundle"));
        }

        [Test]
        public void Choosing_reveals_and_saving_needs_a_result()
        {
            Create(_reader);

            _view.ClickSave();
            Assert.That(_saver.Saved, Is.Null);
            Assert.That(_view.Status, Does.Contain("no report"));

            _view.ClickAudit();
            _view.Choose(_view.Result.Report.Findings[0]);
            _view.ClickSave();

            Assert.That(_locator.Revealed, Is.SameAs(_view.Result.Report.Findings[0]));
            Assert.That(_saver.Saved, Is.SameAs(_view.Result.Report));
            Assert.That(_view.Status, Is.EqualTo("Saved Logs/DevKit/audit.json"));
        }

        [Test]
        public void After_dispose_the_view_is_no_longer_listened_to()
        {
            AuditPresenter presenter = Create(_reader);
            presenter.Dispose();

            _view.ClickAudit();

            Assert.That(_reader.Reads, Is.EqualTo(0));
        }
    }

    public sealed class AuditCommandTests
    {
        private readonly List<string> _log = new List<string>();

        private int Run(IAddressablesReader reader, params string[] arguments)
        {
            _log.Clear();
            var runner = new AuditRunner(reader, new Auditor(Auditor.AllRules()));
            return AuditCommand.Execute(new CommandLineArguments(arguments), runner, _log.Add);
        }

        private static FakeAddressablesReader Reader()
        {
            return new FakeAddressablesReader
            {
                Snapshot = new SnapshotBuilder()
                    .Group("Props", SnapshotBuilder.Entry("crate"))
                    .Bundle("props.bundle", 6)
                    .Code("Load(\"crate\");")
                    .Build(),
            };
        }

        [Test]
        public void Without_addressables_the_command_cannot_run()
        {
            Assert.That(Run(null), Is.EqualTo(ExitCode.CouldNotRun));
            Assert.That(_log[0], Does.Contain("not installed"));
        }

        [Test]
        public void A_tidy_setup_exits_clean()
        {
            Assert.That(Run(Reader()), Is.EqualTo(ExitCode.Clean));
        }

        [Test]
        public void The_limit_and_the_threshold_come_from_the_arguments()
        {
            FakeAddressablesReader reader = Reader();

            Assert.That(Run(reader, "-devkitMaxBundleMb", "0.5", "-devkitPackageAssets"), Is.EqualTo(ExitCode.Clean), "a warning does not fail a run by default");
            Assert.That(reader.LastOptions.MaxBundleMegabytes, Is.EqualTo(0.5f));
            Assert.That(reader.LastOptions.IncludePackageAssets, Is.True);
            Assert.That(Run(reader, "-devkitMaxBundleMb", "0.5", "-devkitFailOn", "warning"), Is.EqualTo(ExitCode.Findings));
        }

        [Test]
        public void A_limit_that_is_not_a_positive_number_is_refused()
        {
            Assert.That(Run(Reader(), "-devkitMaxBundleMb", "big"), Is.EqualTo(ExitCode.CouldNotRun));
            Assert.That(Run(Reader(), "-devkitMaxBundleMb", "0"), Is.EqualTo(ExitCode.CouldNotRun));
        }

        [Test]
        public void The_real_reader_is_found_when_addressables_is_installed()
        {
            IAddressablesReader reader = AuditComposition.FindReader();

#if DEVKIT_TESTS_WITH_ADDRESSABLES
            Assert.That(reader, Is.Not.Null);
            Assert.That(reader.GetType().Assembly.GetName().Name, Is.EqualTo("RoadAndCode.DevKit.AddressablesAudit.Adapter"));
#else
            Assert.That(reader, Is.Null);
#endif
        }
    }
}
