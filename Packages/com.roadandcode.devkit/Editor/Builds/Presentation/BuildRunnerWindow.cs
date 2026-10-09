using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>
    /// The Build Runner's window: one row per profile with where it builds to and how the last
    /// build went, and below it what is wrong with the plan and what the last run said.
    /// <see cref="BuildRunnerPresenter"/> decides what is shown.
    /// </summary>
    public sealed class BuildRunnerWindow : EditorWindow, IBuildRunnerView
    {
        private const string Title = "Build Runner";

        private BuildRunnerPresenter _presenter;
        private VisualElement _planned;
        private VisualElement _noPlan;
        private VisualElement _profiles;
        private FindingsView _findings;
        private Label _status;

        public event Action CreatePlanRequested;

        public event Action EditPlanRequested;

        public event Action RefreshRequested;

        public event Action BuildEnabledRequested;

        public event Action<int> BuildOneRequested;

        public event Action<int, bool> ProfileToggled;

        public event Action<int> RevealRequested;

        public static BuildRunnerWindow Open()
        {
            var window = GetWindow<BuildRunnerWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(600f, 300f);
            return window;
        }

        public void ShowNoPlan()
        {
            _planned.style.display = DisplayStyle.None;
            _noPlan.style.display = DisplayStyle.Flex;
        }

        public void ShowPlan(IReadOnlyList<ProfileRow> profiles, IReadOnlyList<Finding> findings)
        {
            _planned.style.display = DisplayStyle.Flex;
            _noPlan.style.display = DisplayStyle.None;

            _profiles.Clear();
            for (int i = 0; i < profiles.Count; i++) _profiles.Add(MakeRow(profiles[i], i));
            _findings.Show(findings, "The plan has no problems and nothing has been built from this window yet.");
        }

        public void ShowStatus(string text) => _status.text = text;

        [MenuItem(DevKitMenu.Root + "Build Runner", priority = 20)]
        private static void OpenFromMenu() => Open();

        [Shortcut(DevKitMenu.Shortcuts + "Build Runner", KeyCode.B, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        private static void OpenFromShortcut() => Open();

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            DevKitStyle.Apply(root);

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => Later(() => BuildEnabledRequested?.Invoke())) { text = "Build Enabled", name = "build-enabled" });
            toolbar.Add(new ToolbarButton(() => EditPlanRequested?.Invoke()) { text = "Edit Plan", name = "edit-plan" });
            var spacer = new VisualElement();
            spacer.AddToClassList("devkit-toolbar__spacer");
            toolbar.Add(spacer);
            toolbar.Add(new ToolbarButton(() => RefreshRequested?.Invoke()) { text = "Refresh", name = "refresh" });
            root.Add(toolbar);

            _planned = new VisualElement();
            _planned.AddToClassList("devkit-root");
            _profiles = new VisualElement();
            _profiles.AddToClassList("devkit-section");
            _planned.Add(_profiles);
            _findings = new FindingsView();
            _planned.Add(_findings);
            root.Add(_planned);

            _noPlan = new VisualElement();
            _noPlan.AddToClassList("devkit-root");
            var hint = new Label("A build plan lists the builds this project makes and where each one goes. It is an asset, so it is versioned with the project and the command line uses the same one.");
            hint.AddToClassList("devkit-empty");
            _noPlan.Add(hint);
            var create = new Button(() => CreatePlanRequested?.Invoke()) { text = "Create Build Plan", name = "create-plan" };
            create.style.alignSelf = Align.Center;
            create.style.marginBottom = 24f;
            _noPlan.Add(create);
            root.Add(_noPlan);

            var statusBar = new VisualElement();
            statusBar.AddToClassList("devkit-status");
            _status = new Label();
            _status.AddToClassList("devkit-status__text");
            statusBar.Add(_status);
            root.Add(statusBar);

            _presenter = BuildRunnerComposition.Present(this);
        }

        private void OnDestroy() => _presenter?.Dispose();

        // The plan is edited in the Inspector, so coming back to this window shows it as it is now.
        private void OnFocus() => _presenter?.Refresh();

        private VisualElement MakeRow(ProfileRow profile, int index)
        {
            var row = new VisualElement();
            row.AddToClassList("devkit-row");

            var enabled = new Toggle { value = profile.Enabled, tooltip = "Include in Build Enabled" };
            enabled.RegisterValueChangedCallback(change => ProfileToggled?.Invoke(index, change.newValue));
            row.Add(enabled);

            var name = new Label(profile.Name) { tooltip = profile.Platform };
            name.AddToClassList("devkit-cell");
            name.style.width = 110f;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);

            var location = new Label(profile.Location) { tooltip = profile.Location };
            location.AddToClassList("devkit-cell");
            location.AddToClassList("devkit-cell--dim");
            location.AddToClassList("devkit-row__grow");
            row.Add(location);

            var last = new Label(profile.LastResult);
            last.AddToClassList("devkit-badge");
            if (profile.LastSucceeded.HasValue) last.AddToClassList(profile.LastSucceeded.Value ? "devkit-badge--good" : "devkit-badge--bad");
            row.Add(last);

            row.Add(new Button(() => Later(() => BuildOneRequested?.Invoke(index))) { text = "Build" });
            row.Add(new Button(() => RevealRequested?.Invoke(index)) { text = "Show" });
            return row;
        }

        // A build takes over the editor for minutes. Starting it after the click has been handled
        // keeps it out of the middle of the UI's event dispatch.
        private static void Later(Action action) => EditorApplication.delayCall += () => action();
    }
}
