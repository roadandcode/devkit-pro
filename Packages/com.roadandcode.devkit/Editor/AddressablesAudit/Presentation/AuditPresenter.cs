using System;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>What the audit window can show and what it reports back.</summary>
    public interface IAuditView
    {
        event Action AuditRequested;

        event Action<Finding> FindingChosen;

        event Action SaveRequested;

        void ShowResult(AuditResult result);

        /// <summary>Replaces the content with the reason there is nothing to audit.</summary>
        void ShowUnavailable(string reason);

        void ShowStatus(string text);
    }

    /// <summary>The audit window's behaviour, with no window in it.</summary>
    public sealed class AuditPresenter : IDisposable
    {
        private readonly IAuditView _view;
        private readonly AuditRunner _runner;
        private readonly IPreferenceStore _preferences;
        private readonly IFindingLocator _locator;
        private readonly IReportSaver _saver;
        private readonly Func<IProgressSink> _progress;

        private AuditResult _last;

        public AuditPresenter(IAuditView view, AuditRunner runner, IPreferenceStore preferences, IFindingLocator locator,
            IReportSaver saver, Func<IProgressSink> progress)
        {
            _view = view;
            _runner = runner;
            _preferences = preferences;
            _locator = locator;
            _saver = saver;
            _progress = progress;

            _view.AuditRequested += Audit;
            _view.FindingChosen += OnFindingChosen;
            _view.SaveRequested += OnSaveRequested;

            if (_runner.IsAvailable) _view.ShowStatus("Not audited yet.");
            else _view.ShowUnavailable(AuditRunner.NotInstalled);
        }

        public void Dispose()
        {
            _view.AuditRequested -= Audit;
            _view.FindingChosen -= OnFindingChosen;
            _view.SaveRequested -= OnSaveRequested;
        }

        public void Audit()
        {
            IProgressSink progress = _progress();
            bool ran;
            string problem;
            try
            {
                ran = _runner.TryRun(AuditPreferences.Options(_preferences), progress, out _last, out problem);
            }
            finally
            {
                progress.Done();
            }

            if (!ran)
            {
                _view.ShowUnavailable(problem);
                return;
            }

            _view.ShowResult(_last);
            _view.ShowStatus(_last.Report.Summary);
        }

        private void OnFindingChosen(Finding finding) => _locator.Reveal(finding);

        private void OnSaveRequested()
        {
            if (_last == null)
            {
                _view.ShowStatus("There is no report to save yet.");
                return;
            }

            _view.ShowStatus("Saved " + _saver.Save(_last.Report));
        }
    }
}
