using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.DataBrowser.Tests
{
    internal sealed class FakeCatalogReader : ICatalogReader
    {
        public List<AssetRecord> Records { get; } = new List<AssetRecord>(Sample.Catalog().Records);

        public int Reads { get; private set; }

        public bool LastIncludedSubAssets { get; private set; }

        public IReadOnlyList<AssetRecord> Read(bool includeSubAssets, IProgressSink progress)
        {
            Reads++;
            LastIncludedSubAssets = includeSubAssets;
            return Records.ToArray();
        }
    }

    internal sealed class FakeBrowserView : IBrowserView
    {
        public event Action RefreshRequested;

        public event Action<string> SearchChanged;

        public event Action<string> TypeChosen;

        public event Action<bool> ProjectOnlyChanged;

        public event Action<AssetRecord> RecordChosen;

        public event Action<AssetRecord> RecordOpened;

        public bool ProjectOnly { get; private set; }

        public IReadOnlyList<TypeEntry> Types { get; private set; }

        public int Total { get; private set; }

        public string SelectedType { get; private set; }

        public IReadOnlyList<AssetRecord> Records { get; private set; }

        public AssetRecord Inspected { get; private set; }

        public string Status { get; private set; }

        public void ShowOptions(bool projectTypesOnly) => ProjectOnly = projectTypesOnly;

        public void ShowTypes(IReadOnlyList<TypeEntry> types, int total, string selectedTypeFullName)
        {
            Types = types;
            Total = total;
            SelectedType = selectedTypeFullName;
        }

        public void ShowRecords(IReadOnlyList<AssetRecord> records) => Records = records;

        public void ShowInspector(AssetRecord record) => Inspected = record;

        public void ShowStatus(string text) => Status = text;

        public void ClickRefresh() => RefreshRequested?.Invoke();

        public void Search(string text) => SearchChanged?.Invoke(text);

        public void ChooseType(string typeFullName) => TypeChosen?.Invoke(typeFullName);

        public void SetProjectOnly(bool value) => ProjectOnlyChanged?.Invoke(value);

        public void Choose(AssetRecord record) => RecordChosen?.Invoke(record);

        public void DoubleClick(AssetRecord record) => RecordOpened?.Invoke(record);
    }

    internal sealed class FakeRevealer : IAssetRevealer
    {
        public AssetRecord Revealed { get; private set; }

        public void Reveal(AssetRecord record) => Revealed = record;
    }

    public sealed class BrowserPresenterTests
    {
        private FakeBrowserView _view;
        private FakeCatalogReader _reader;
        private FakeRevealer _revealer;
        private MemoryPreferenceStore _preferences;
        private BrowserPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeBrowserView();
            _reader = new FakeCatalogReader();
            _revealer = new FakeRevealer();
            _preferences = new MemoryPreferenceStore();
            _presenter = new BrowserPresenter(_view, _reader, _preferences, _revealer, () => new NullProgress());
        }

        [Test]
        public void It_opens_with_the_projects_own_types_listed()
        {
            Assert.That(_reader.Reads, Is.EqualTo(1));
            Assert.That(_view.ProjectOnly, Is.True);
            Assert.That(_view.Types, Has.Count.EqualTo(3));
            Assert.That(_view.Total, Is.EqualTo(6));
            Assert.That(_view.SelectedType, Is.Null);
            Assert.That(_view.Records, Has.Count.EqualTo(6));
            Assert.That(_view.Status, Is.EqualTo("6 shown of 6 assets in 3 types"));
        }

        [Test]
        public void Choosing_a_type_and_typing_narrow_the_records()
        {
            _view.ChooseType("Game.ItemDefinition");
            Assert.That(_view.Records, Has.Count.EqualTo(3));

            _view.Search("sw");
            Assert.That(_view.Records, Has.Count.EqualTo(1));
            Assert.That(_view.Status, Is.EqualTo("1 shown of 6 assets in 3 types"));

            _view.ChooseType(null);
            _view.Search(string.Empty);
            Assert.That(_view.Records, Has.Count.EqualTo(6));
        }

        [Test]
        public void Showing_every_type_is_remembered()
        {
            _view.SetProjectOnly(false);

            Assert.That(_view.Types, Has.Count.EqualTo(4));
            Assert.That(_view.Total, Is.EqualTo(7));
            Assert.That(BrowserPreferences.ProjectTypesOnly.Get(_preferences), Is.False);
        }

        [Test]
        public void A_selected_type_that_the_filter_hides_falls_back_to_all_types()
        {
            _view.SetProjectOnly(false);
            _view.ChooseType("Vendor.PipelineAsset");
            Assert.That(_view.Records, Has.Count.EqualTo(1));

            _view.SetProjectOnly(true);

            Assert.That(_view.SelectedType, Is.Null);
            Assert.That(_view.Records, Has.Count.EqualTo(6));
        }

        [Test]
        public void Refresh_reads_again_keeps_the_selected_type_and_clears_the_inspector()
        {
            _view.ChooseType("Game.EnemyDefinition");
            _view.Choose(_view.Records[0]);
            _reader.Records.Add(Sample.Enemy("Brute"));

            _view.ClickRefresh();

            Assert.That(_reader.Reads, Is.EqualTo(2));
            Assert.That(_view.SelectedType, Is.EqualTo("Game.EnemyDefinition"));
            Assert.That(_view.Records, Has.Count.EqualTo(3));
            Assert.That(_view.Inspected, Is.Null);
        }

        [Test]
        public void Selecting_shows_the_inspector_and_double_clicking_reveals()
        {
            AssetRecord record = _view.Records[0];

            _view.Choose(record);
            Assert.That(_view.Inspected, Is.SameAs(record));
            Assert.That(_revealer.Revealed, Is.Null);

            _view.DoubleClick(record);
            Assert.That(_revealer.Revealed, Is.SameAs(record));
        }

        [Test]
        public void Sub_assets_are_read_when_the_preference_asks_for_them()
        {
            Assert.That(_reader.LastIncludedSubAssets, Is.False);

            BrowserPreferences.IncludeSubAssets.Set(_preferences, true);
            _view.ClickRefresh();

            Assert.That(_reader.LastIncludedSubAssets, Is.True);
        }

        [Test]
        public void After_dispose_the_view_is_no_longer_listened_to()
        {
            _presenter.Dispose();

            _view.ClickRefresh();

            Assert.That(_reader.Reads, Is.EqualTo(1));
        }
    }

    public sealed class CatalogCommandTests
    {
        [Test]
        public void A_missing_script_fails_the_run()
        {
            var log = new List<string>();

            int code = CatalogCommand.Execute(new CommandLineArguments(new string[0]), new FakeCatalogReader(), log.Add);

            Assert.That(code, Is.EqualTo(ExitCode.Findings));
            Assert.That(log[0], Does.Contain("Assets: 6"));
        }

        [Test]
        public void A_healthy_project_exits_clean_and_the_flags_reach_the_reader()
        {
            var reader = new FakeCatalogReader();
            reader.Records.RemoveAll(record => record.IsMissingScript);
            var log = new List<string>();

            int code = CatalogCommand.Execute(new CommandLineArguments(new[] { "-devkitSubAssets", "-devkitAllTypes" }), reader, log.Add);

            Assert.That(code, Is.EqualTo(ExitCode.Clean));
            Assert.That(reader.LastIncludedSubAssets, Is.True);
            Assert.That(log[0], Does.Contain("Vendor.PipelineAsset: 1"));
        }
    }
}
