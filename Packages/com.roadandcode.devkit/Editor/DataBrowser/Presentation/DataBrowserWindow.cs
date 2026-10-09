using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RoadAndCode.DevKit.DataBrowser
{
    /// <summary>
    /// The browser's window: types on the left, assets of the chosen type in the middle, the
    /// selected asset's fields on the right. <see cref="BrowserPresenter"/> decides what is shown.
    /// </summary>
    public sealed class DataBrowserWindow : EditorWindow, IBrowserView
    {
        private const string Title = "ScriptableObjects";
        private const float RowHeight = 22f;

        private readonly List<TypeEntry> _typeRows = new List<TypeEntry>();

        private BrowserPresenter _presenter;
        private IReadOnlyList<AssetRecord> _recordRows = new AssetRecord[0];
        private ToolbarToggle _projectOnly;
        private ListView _types;
        private ListView _records;
        private ScrollView _inspector;
        private Label _status;

        public event Action RefreshRequested;

        public event Action<string> SearchChanged;

        public event Action<string> TypeChosen;

        public event Action<bool> ProjectOnlyChanged;

        public event Action<AssetRecord> RecordChosen;

        public event Action<AssetRecord> RecordOpened;

        public static DataBrowserWindow Open()
        {
            var window = GetWindow<DataBrowserWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(640f, 280f);
            return window;
        }

        public void ShowOptions(bool projectTypesOnly) => _projectOnly.SetValueWithoutNotify(projectTypesOnly);

        public void ShowTypes(IReadOnlyList<TypeEntry> types, int total, string selectedTypeFullName)
        {
            _typeRows.Clear();
            _typeRows.Add(new TypeEntry("All types", null, total));
            int selected = 0;
            for (int i = 0; i < types.Count; i++)
            {
                _typeRows.Add(types[i]);
                if (types[i].TypeFullName == selectedTypeFullName) selected = i + 1;
            }

            _types.itemsSource = _typeRows;
            _types.RefreshItems();
            _types.SetSelectionWithoutNotify(new[] { selected });
        }

        public void ShowRecords(IReadOnlyList<AssetRecord> records)
        {
            _recordRows = records;
            _records.itemsSource = (System.Collections.IList)records;
            _records.RefreshItems();
            _records.ClearSelection();
        }

        public void ShowInspector(AssetRecord record)
        {
            _inspector.Clear();
            if (record == null)
            {
                AddHint("Select an asset to edit it here. Double-click to find it in the Project window.");
                return;
            }

            Object target = Load(record);
            if (target == null)
            {
                AddHint(record.IsMissingScript ? "This asset's script is missing, so it has no fields to show." : "This asset could not be loaded.");
                return;
            }

            var section = new VisualElement();
            section.AddToClassList("devkit-section");
            var title = new Label(record.Name);
            title.AddToClassList("devkit-section__title");
            section.Add(title);
            var where = new Label($"{record.TypeFullName}\n{record.Path}");
            where.AddToClassList("devkit-cell--dim");
            section.Add(where);
            _inspector.Add(section);
            _inspector.Add(new InspectorElement(target));
        }

        public void ShowStatus(string text) => _status.text = text;

        [MenuItem(DevKitMenu.Root + "ScriptableObject Browser", priority = 40)]
        private static void OpenFromMenu() => Open();

        [Shortcut(DevKitMenu.Shortcuts + "ScriptableObject Browser", KeyCode.O, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        private static void OpenFromShortcut() => Open();

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            DevKitStyle.Apply(root);

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => RefreshRequested?.Invoke()) { text = "Refresh", name = "refresh" });
            _projectOnly = new ToolbarToggle { text = "Project types only", name = "project-only" };
            _projectOnly.RegisterValueChangedCallback(change => ProjectOnlyChanged?.Invoke(change.newValue));
            toolbar.Add(_projectOnly);

            var spacer = new VisualElement();
            spacer.AddToClassList("devkit-toolbar__spacer");
            toolbar.Add(spacer);

            var search = new ToolbarSearchField { tooltip = "Words in the name or path. t:Name narrows by type." };
            search.AddToClassList("devkit-toolbar__search");
            search.RegisterValueChangedCallback(change => SearchChanged?.Invoke(change.newValue));
            toolbar.Add(search);
            root.Add(toolbar);

            _types = NewList(MakeRow, BindType);
            _types.selectionChanged += selection =>
            {
                foreach (object item in selection)
                {
                    TypeChosen?.Invoke(((TypeEntry)item).TypeFullName);
                    return;
                }
            };

            _records = NewList(MakeRow, BindRecord);
            _records.selectionChanged += selection =>
            {
                foreach (object item in selection)
                {
                    RecordChosen?.Invoke((AssetRecord)item);
                    return;
                }
            };
            _records.itemsChosen += chosen =>
            {
                foreach (object item in chosen)
                {
                    RecordOpened?.Invoke((AssetRecord)item);
                    return;
                }
            };

            _inspector = new ScrollView();

            var right = new TwoPaneSplitView(0, 280f, TwoPaneSplitViewOrientation.Horizontal);
            right.Add(_records);
            right.Add(_inspector);

            var split = new TwoPaneSplitView(0, 210f, TwoPaneSplitViewOrientation.Horizontal);
            split.Add(_types);
            split.Add(right);
            root.Add(split);

            var statusBar = new VisualElement();
            statusBar.AddToClassList("devkit-status");
            _status = new Label();
            _status.AddToClassList("devkit-status__text");
            statusBar.Add(_status);
            root.Add(statusBar);

            _presenter = BrowserComposition.Present(this);
        }

        private void OnDestroy() => _presenter?.Dispose();

        // Assets are created and deleted while the window sits open.
        private void OnProjectChange() => _presenter?.Refresh();

        private static ListView NewList(Func<VisualElement> makeItem, Action<VisualElement, int> bindItem)
        {
            return new ListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                makeItem = makeItem,
                bindItem = bindItem,
            };
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("devkit-row");
            var name = new Label { name = "name" };
            name.AddToClassList("devkit-cell");
            name.AddToClassList("devkit-row__grow");
            row.Add(name);
            var note = new Label { name = "note" };
            note.AddToClassList("devkit-cell");
            note.AddToClassList("devkit-cell--dim");
            row.Add(note);
            return row;
        }

        private void BindType(VisualElement row, int index)
        {
            TypeEntry entry = _typeRows[index];
            row.Q<Label>("name").text = entry.TypeName;
            row.Q<Label>("note").text = entry.Count.ToString();
            row.tooltip = entry.TypeFullName ?? string.Empty;
        }

        private void BindRecord(VisualElement row, int index)
        {
            AssetRecord record = _recordRows[index];
            row.Q<Label>("name").text = record.IsSubAsset ? record.Name + " (sub-asset)" : record.Name;
            row.Q<Label>("note").text = record.TypeName;
            row.tooltip = record.Path;
        }

        private void AddHint(string text)
        {
            var hint = new Label(text);
            hint.AddToClassList("devkit-empty");
            _inspector.Add(hint);
        }

        private static Object Load(AssetRecord record)
        {
            if (!record.IsSubAsset) return AssetDatabase.LoadMainAssetAtPath(record.Path);

            foreach (Object item in AssetDatabase.LoadAllAssetRepresentationsAtPath(record.Path))
            {
                if (item != null && item.name == record.Name && item.GetType().FullName == record.TypeFullName) return item;
            }

            return null;
        }
    }
}
