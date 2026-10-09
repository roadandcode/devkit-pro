using System;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>The validator's window. It draws and forwards clicks; <see cref="ValidatorPresenter"/> decides.</summary>
    public sealed class ValidatorWindow : EditorWindow, IValidatorView
    {
        private const string Title = "Validator";
        private const string EmptyText = "Press Validate to check the scope for missing scripts, missing and unassigned references, missing prefabs and empty objects.";

        private ValidatorPresenter _presenter;
        private ToolbarMenu _scopeMenu;
        private FindingsView _findings;
        private Label _status;
        private ValidationScope _scope;

        public event Action ScanRequested;

        public event Action<ValidationScope> ScopeToggled;

        public event Action<Finding> FindingChosen;

        public event Action SaveRequested;

        public static ValidatorWindow Open()
        {
            var window = GetWindow<ValidatorWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(520f, 240f);
            return window;
        }

        /// <summary>Scans a scope without changing the one the window is set to.</summary>
        public void Run(ValidationScope scope) => _presenter?.Scan(scope);

        public void ShowScope(ValidationScope scope)
        {
            _scope = scope;
            _scopeMenu.text = "Scope: " + ValidationScopes.Describe(scope);
        }

        public void ShowReport(ScanReport report) => _findings.Show(report.Findings, "Nothing found in " + ValidationScopes.Describe(_scope) + ".");

        public void ShowStatus(string text) => _status.text = text;

        [MenuItem(DevKitMenu.Root + "Scene and Asset Validator", priority = 10)]
        private static void OpenFromMenu() => Open();

        [Shortcut(DevKitMenu.Shortcuts + "Validate Open Scenes", KeyCode.V, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        private static void ValidateOpenScenes()
        {
            ValidatorWindow window = Open();

            // A window that was just created builds its content on the next editor update.
            EditorApplication.delayCall += () => window.Run(ValidationScope.OpenScenes);
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            DevKitStyle.Apply(root);

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => ScanRequested?.Invoke()) { text = "Validate", name = "validate" });

            _scopeMenu = new ToolbarMenu { name = "scope" };
            AddScope("Open scenes", ValidationScope.OpenScenes);
            AddScope("Scenes in Build Settings", ValidationScope.BuildScenes);
            AddScope("Every scene under Assets", ValidationScope.AllScenes);
            AddScope("Prefabs", ValidationScope.Prefabs);
            AddScope("ScriptableObjects and materials", ValidationScope.Assets);
            toolbar.Add(_scopeMenu);

            var spacer = new VisualElement();
            spacer.AddToClassList("devkit-toolbar__spacer");
            toolbar.Add(spacer);
            toolbar.Add(new ToolbarButton(() => SaveRequested?.Invoke()) { text = "Save Report", name = "save" });
            root.Add(toolbar);

            _findings = new FindingsView();
            _findings.Chosen += finding => FindingChosen?.Invoke(finding);
            _findings.Show(null, EmptyText);
            root.Add(_findings);

            var statusBar = new VisualElement();
            statusBar.AddToClassList("devkit-status");
            _status = new Label();
            _status.AddToClassList("devkit-status__text");
            statusBar.Add(_status);
            root.Add(statusBar);

            _presenter = ValidatorComposition.Present(this);
        }

        private void OnDestroy() => _presenter?.Dispose();

        private void AddScope(string label, ValidationScope part)
        {
            _scopeMenu.menu.AppendAction(
                label,
                action => ScopeToggled?.Invoke(part),
                action => (_scope & part) != 0 ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }
    }
}
