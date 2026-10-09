using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.DataBrowser
{
    /// <summary>What the browser window can show and what it reports back.</summary>
    public interface IBrowserView
    {
        event Action RefreshRequested;

        event Action<string> SearchChanged;

        /// <summary>A type was picked in the list. Null means "all types".</summary>
        event Action<string> TypeChosen;

        event Action<bool> ProjectOnlyChanged;

        event Action<AssetRecord> RecordChosen;

        /// <summary>A record was double-clicked.</summary>
        event Action<AssetRecord> RecordOpened;

        void ShowOptions(bool projectTypesOnly);

        void ShowTypes(IReadOnlyList<TypeEntry> types, int total, string selectedTypeFullName);

        void ShowRecords(IReadOnlyList<AssetRecord> records);

        /// <summary>Shows the asset's fields for editing, or clears the panel when null.</summary>
        void ShowInspector(AssetRecord record);

        void ShowStatus(string text);
    }

    /// <summary>Selects and pings an asset in the Project window.</summary>
    public interface IAssetRevealer
    {
        void Reveal(AssetRecord record);
    }

    /// <summary>The browser window's behaviour, with no window in it.</summary>
    public sealed class BrowserPresenter : IDisposable
    {
        private readonly IBrowserView _view;
        private readonly ICatalogReader _reader;
        private readonly IPreferenceStore _preferences;
        private readonly IAssetRevealer _revealer;
        private readonly Func<IProgressSink> _progress;
        private readonly CatalogQuery _query = new CatalogQuery();

        private Catalog _catalog = new Catalog(null);

        public BrowserPresenter(IBrowserView view, ICatalogReader reader, IPreferenceStore preferences, IAssetRevealer revealer,
            Func<IProgressSink> progress)
        {
            _view = view;
            _reader = reader;
            _preferences = preferences;
            _revealer = revealer;
            _progress = progress;

            _query.ProjectTypesOnly = BrowserPreferences.ProjectTypesOnly.Get(preferences);

            _view.RefreshRequested += Refresh;
            _view.SearchChanged += OnSearchChanged;
            _view.TypeChosen += OnTypeChosen;
            _view.ProjectOnlyChanged += OnProjectOnlyChanged;
            _view.RecordChosen += OnRecordChosen;
            _view.RecordOpened += OnRecordOpened;

            _view.ShowOptions(_query.ProjectTypesOnly);
            Refresh();
        }

        public void Dispose()
        {
            _view.RefreshRequested -= Refresh;
            _view.SearchChanged -= OnSearchChanged;
            _view.TypeChosen -= OnTypeChosen;
            _view.ProjectOnlyChanged -= OnProjectOnlyChanged;
            _view.RecordChosen -= OnRecordChosen;
            _view.RecordOpened -= OnRecordOpened;
        }

        /// <summary>Reads the project again. Assets come and go while the window is open.</summary>
        public void Refresh()
        {
            IProgressSink progress = _progress();
            try
            {
                _catalog = new Catalog(_reader.Read(BrowserPreferences.IncludeSubAssets.Get(_preferences), progress));
            }
            finally
            {
                progress.Done();
            }

            _view.ShowInspector(null);
            Show();
        }

        private void OnSearchChanged(string text)
        {
            _query.Text = text;
            ShowRecords(_catalog.Types(_query.ProjectTypesOnly).Count);
        }

        private void OnTypeChosen(string typeFullName)
        {
            _query.TypeFullName = typeFullName;
            ShowRecords(_catalog.Types(_query.ProjectTypesOnly).Count);
        }

        private void OnProjectOnlyChanged(bool projectTypesOnly)
        {
            _query.ProjectTypesOnly = projectTypesOnly;
            BrowserPreferences.ProjectTypesOnly.Set(_preferences, projectTypesOnly);
            Show();
        }

        private void OnRecordChosen(AssetRecord record) => _view.ShowInspector(record);

        private void OnRecordOpened(AssetRecord record) => _revealer.Reveal(record);

        private void Show()
        {
            List<TypeEntry> types = _catalog.Types(_query.ProjectTypesOnly);

            // The type that was selected may be gone after a refresh or a change of filter.
            if (_query.TypeFullName != null && !types.Exists(type => type.TypeFullName == _query.TypeFullName)) _query.TypeFullName = null;

            _view.ShowTypes(types, _catalog.Count(_query.ProjectTypesOnly), _query.TypeFullName);
            ShowRecords(types.Count);
        }

        private void ShowRecords(int typeCount)
        {
            List<AssetRecord> records = _catalog.Find(_query);
            _view.ShowRecords(records);
            _view.ShowStatus($"{records.Count} shown of {_catalog.Count(_query.ProjectTypesOnly)} assets in {typeCount} types");
        }
    }
}
