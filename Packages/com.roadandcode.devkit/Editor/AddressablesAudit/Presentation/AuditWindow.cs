using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>
    /// The audit's window: the groups at a glance on top, the findings below.
    /// <see cref="AuditPresenter"/> decides what is shown.
    /// </summary>
    public sealed class AuditWindow : EditorWindow, IAuditView
    {
        private const string Title = "Addressables Audit";
        private const string EmptyText = "Press Audit to look for groups nothing references, entries whose asset is gone, dependencies copied into several bundles and bundles over the size limit.";
        private const float RowHeight = 22f;

        private AuditPresenter _presenter;
        private IReadOnlyList<GroupSummary> _groupRows = new GroupSummary[0];
        private VisualElement _content;
        private Label _unavailable;
        private ListView _groups;
        private FindingsView _findings;
        private Label _status;

        public event Action AuditRequested;

        public event Action<Finding> FindingChosen;

        public event Action SaveRequested;

        public static AuditWindow Open()
        {
            var window = GetWindow<AuditWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(560f, 300f);
            return window;
        }

        /// <summary>Runs the audit as if the button had been pressed.</summary>
        public void Run() => _presenter?.Audit();

        public void ShowResult(AuditResult result)
        {
            SetAvailable(true);
            _groupRows = result.Groups;
            _groups.itemsSource = (System.Collections.IList)result.Groups;
            _groups.RefreshItems();
            _findings.Show(result.Report.Findings, "Nothing found. Every group is referenced and within the size limit.");
        }

        public void ShowUnavailable(string reason)
        {
            SetAvailable(false);
            _unavailable.text = reason;
            _status.text = string.Empty;
        }

        public void ShowStatus(string text) => _status.text = text;

        [MenuItem(DevKitMenu.Root + "Addressables Audit", priority = 30)]
        private static void OpenFromMenu() => Open();

        [Shortcut(DevKitMenu.Shortcuts + "Addressables Audit", KeyCode.G, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        private static void OpenFromShortcut() => Open();

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            DevKitStyle.Apply(root);

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => AuditRequested?.Invoke()) { text = "Audit", name = "audit" });
            var spacer = new VisualElement();
            spacer.AddToClassList("devkit-toolbar__spacer");
            toolbar.Add(spacer);
            toolbar.Add(new ToolbarButton(() => SaveRequested?.Invoke()) { text = "Save Report", name = "save" });
            root.Add(toolbar);

            _groups = new ListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.None,
                makeItem = MakeGroupRow,
                bindItem = BindGroupRow,
            };

            _findings = new FindingsView();
            _findings.Chosen += finding => FindingChosen?.Invoke(finding);
            _findings.Show(null, EmptyText);

            var split = new TwoPaneSplitView(0, 136f, TwoPaneSplitViewOrientation.Vertical);
            split.Add(_groups);
            split.Add(_findings);
            _content = split;
            root.Add(_content);

            _unavailable = new Label();
            _unavailable.AddToClassList("devkit-empty");
            root.Add(_unavailable);

            var statusBar = new VisualElement();
            statusBar.AddToClassList("devkit-status");
            _status = new Label();
            _status.AddToClassList("devkit-status__text");
            statusBar.Add(_status);
            root.Add(statusBar);

            SetAvailable(true);
            _presenter = AuditComposition.Present(this);
        }

        private void OnDestroy() => _presenter?.Dispose();

        private void SetAvailable(bool available)
        {
            _content.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            _unavailable.style.display = available ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static VisualElement MakeGroupRow()
        {
            var row = new VisualElement();
            row.AddToClassList("devkit-row");
            var name = new Label { name = "name" };
            name.AddToClassList("devkit-cell");
            name.AddToClassList("devkit-row__grow");
            row.Add(name);
            var size = new Label { name = "size" };
            size.AddToClassList("devkit-cell");
            size.AddToClassList("devkit-cell--dim");
            row.Add(size);
            var use = new Label { name = "use" };
            use.AddToClassList("devkit-badge");
            row.Add(use);
            return row;
        }

        private void BindGroupRow(VisualElement row, int index)
        {
            GroupSummary group = _groupRows[index];
            row.Q<Label>("name").text = group.Name;
            row.Q<Label>("size").text = $"{group.Entries} entries, {Sizes.Megabytes(group.SourceBytes)} of source";

            var use = row.Q<Label>("use");
            bool unused = group.Entries > 0 && group.UsedEntries == 0;
            use.text = group.Entries == 0 ? "empty" : unused ? "unreferenced" : $"{group.UsedEntries} of {group.Entries} referenced";
            use.EnableInClassList("devkit-badge--bad", unused || group.Entries == 0);
            use.EnableInClassList("devkit-badge--good", group.Entries > 0 && group.UsedEntries == group.Entries);
        }
    }
}
