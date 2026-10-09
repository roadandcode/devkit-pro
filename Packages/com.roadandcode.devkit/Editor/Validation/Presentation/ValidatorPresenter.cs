using System;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>What the validator window can show and what it reports back.</summary>
    public interface IValidatorView
    {
        event Action ScanRequested;

        /// <summary>One part of the scope was switched on or off.</summary>
        event Action<ValidationScope> ScopeToggled;

        event Action<Finding> FindingChosen;

        event Action SaveRequested;

        void ShowScope(ValidationScope scope);

        void ShowReport(ScanReport report);

        void ShowStatus(string text);
    }

    /// <summary>The validator window's behaviour, with no window in it.</summary>
    public sealed class ValidatorPresenter : IDisposable
    {
        private readonly IValidatorView _view;
        private readonly ValidationScanner _scanner;
        private readonly IPreferenceStore _preferences;
        private readonly IFindingLocator _locator;
        private readonly IReportSaver _saver;
        private readonly Func<IProgressSink> _progress;

        private ValidationScope _scope;
        private ScanReport _last;

        public ValidatorPresenter(IValidatorView view, ValidationScanner scanner, IPreferenceStore preferences,
            IFindingLocator locator, IReportSaver saver, Func<IProgressSink> progress)
        {
            _view = view;
            _scanner = scanner;
            _preferences = preferences;
            _locator = locator;
            _saver = saver;
            _progress = progress;

            _scope = (ValidationScope)(int)ValidationPreferences.Scope.Get(preferences);

            _view.ScanRequested += OnScanRequested;
            _view.ScopeToggled += OnScopeToggled;
            _view.FindingChosen += OnFindingChosen;
            _view.SaveRequested += OnSaveRequested;

            _view.ShowScope(_scope);
            _view.ShowStatus("Nothing checked yet.");
        }

        public void Dispose()
        {
            _view.ScanRequested -= OnScanRequested;
            _view.ScopeToggled -= OnScopeToggled;
            _view.FindingChosen -= OnFindingChosen;
            _view.SaveRequested -= OnSaveRequested;
        }

        /// <summary>Runs a scan over a scope other than the one the window is set to. Used by the shortcut.</summary>
        public void Scan(ValidationScope scope)
        {
            IProgressSink progress = _progress();
            try
            {
                _last = _scanner.Scan(scope, progress);
            }
            finally
            {
                progress.Done();
            }

            _view.ShowReport(_last);
            _view.ShowStatus($"{_last.Summary} ({ValidationScopes.Describe(scope)})");
        }

        private void OnScanRequested() => Scan(_scope);

        private void OnScopeToggled(ValidationScope part)
        {
            ValidationScope changed = _scope ^ part;
            if (changed == ValidationScope.None)
            {
                _view.ShowStatus("At least one thing has to stay in the scope.");
                return;
            }

            _scope = changed;
            ValidationPreferences.Scope.Set(_preferences, (int)_scope);
            _view.ShowScope(_scope);
        }

        private void OnFindingChosen(Finding finding) => _locator.Reveal(finding);

        private void OnSaveRequested()
        {
            if (_last == null)
            {
                _view.ShowStatus("There is no report to save yet.");
                return;
            }

            _view.ShowStatus("Saved " + _saver.Save(_last));
        }
    }
}
