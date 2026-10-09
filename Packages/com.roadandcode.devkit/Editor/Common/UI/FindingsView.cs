using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>
    /// The list of findings every tool shows: severity toggles with counts, a search field and a
    /// table. It only displays and filters; what a click does is up to whoever listens.
    /// </summary>
    public sealed class FindingsView : VisualElement
    {
        private const float RowHeight = 20f;

        private readonly FindingFilter _filter = new FindingFilter();
        private readonly ToolbarToggle _errors;
        private readonly ToolbarToggle _warnings;
        private readonly ToolbarToggle _info;
        private readonly MultiColumnListView _list;
        private readonly Label _empty;

        private IReadOnlyList<Finding> _all = new Finding[0];
        private List<Finding> _visible = new List<Finding>();
        private string _emptyText = string.Empty;

        public FindingsView()
        {
            AddToClassList("devkit-findings");

            var toolbar = new Toolbar();
            _errors = AddToggle(toolbar, value => _filter.ShowErrors = value);
            _warnings = AddToggle(toolbar, value => _filter.ShowWarnings = value);
            _info = AddToggle(toolbar, value => _filter.ShowInfo = value);

            var spacer = new VisualElement();
            spacer.AddToClassList("devkit-toolbar__spacer");
            toolbar.Add(spacer);

            var search = new ToolbarSearchField();
            search.AddToClassList("devkit-toolbar__search");
            search.RegisterValueChangedCallback(change =>
            {
                _filter.Text = change.newValue;
                Refresh();
            });
            toolbar.Add(search);
            Add(toolbar);

            _list = new MultiColumnListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            _list.AddToClassList("devkit-findings__list");
            _list.columns.Add(new Column { name = "severity", title = string.Empty, width = 28, resizable = false, makeCell = MakeIcon, bindCell = BindIcon });
            _list.columns.Add(TextColumn("rule", "Check", 150, false, finding => finding.Rule));
            _list.columns.Add(TextColumn("message", "What", 380, true, finding => finding.Message));
            _list.columns.Add(TextColumn("location", "Where", 320, true, finding => finding.Location));
            _list.selectionChanged += OnSelectionChanged;
            Add(_list);

            _empty = new Label();
            _empty.AddToClassList("devkit-empty");
            Add(_empty);

            Refresh();
        }

        /// <summary>Raised when a row is selected.</summary>
        public event Action<Finding> Chosen;

        /// <summary>The findings currently listed, after the filter.</summary>
        public IReadOnlyList<Finding> Visible => _visible;

        public void Show(IReadOnlyList<Finding> findings, string emptyText)
        {
            _all = findings ?? new Finding[0];
            _emptyText = emptyText ?? string.Empty;
            _list.ClearSelection();
            Refresh();
        }

        private ToolbarToggle AddToggle(Toolbar toolbar, Action<bool> apply)
        {
            var toggle = new ToolbarToggle { value = true };
            toggle.RegisterValueChangedCallback(change =>
            {
                apply(change.newValue);
                Refresh();
            });
            toolbar.Add(toggle);
            return toggle;
        }

        private void Refresh()
        {
            _errors.text = Count(Severity.Error) + " Errors";
            _warnings.text = Count(Severity.Warning) + " Warnings";
            _info.text = Count(Severity.Info) + " Notes";

            _visible = _filter.Apply(_all);
            _list.itemsSource = _visible;
            _list.RefreshItems();

            bool any = _visible.Count > 0;
            _list.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
            _empty.text = _all.Count > 0 ? "Nothing matches the filter." : _emptyText;
        }

        private int Count(Severity severity)
        {
            int count = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].Severity == severity) count++;
            }

            return count;
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                if (item is Finding finding) Chosen?.Invoke(finding);
                return;
            }
        }

        private Column TextColumn(string name, string title, float width, bool stretch, Func<Finding, string> text)
        {
            return new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = 60,
                stretchable = stretch,
                makeCell = MakeText,
                bindCell = (cell, row) =>
                {
                    var label = (Label)cell;
                    label.text = text(_visible[row]);
                    label.tooltip = label.text;
                },
            };
        }

        private static VisualElement MakeText()
        {
            var label = new Label();
            label.AddToClassList("devkit-cell");
            return label;
        }

        private static VisualElement MakeIcon()
        {
            var icon = new Image();
            icon.AddToClassList("devkit-findings__icon");
            return icon;
        }

        private void BindIcon(VisualElement cell, int row)
        {
            Severity severity = _visible[row].Severity;
            ((Image)cell).image = Icon(severity);
            cell.tooltip = ReportFormatter.Name(severity);
        }

        // The console's own icons, so a finding reads the same way a log entry does.
        private static Texture Icon(Severity severity)
        {
            if (severity == Severity.Error) return EditorGUIUtility.IconContent("console.erroricon.sml").image;
            return EditorGUIUtility.IconContent(severity == Severity.Warning ? "console.warnicon.sml" : "console.infoicon.sml").image;
        }
    }
}
