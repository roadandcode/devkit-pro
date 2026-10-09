using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RoadAndCode.DevKit.AddressablesAudit;
using RoadAndCode.DevKit.Builds;
using RoadAndCode.DevKit.Common;
using RoadAndCode.DevKit.DataBrowser;
using RoadAndCode.DevKit.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.Sandbox.Editor
{
    /// <summary>
    /// Takes the README's screenshots: opens each tool on the sample content, lets it draw and
    /// saves what the window shows. Windows are not drawn in batch mode, so this needs a normal
    /// editor on a display:
    /// <code>Unity -projectPath . -executeMethod RoadAndCode.DevKit.Sandbox.Editor.Screenshots.CaptureAll</code>
    /// </summary>
    public static class Screenshots
    {
        private const string Folder = "docs/media";
        private const double SettleSeconds = 1.5;
        private const double GiveUpSeconds = 240;

        // The sample project's heavy group is a quarter of a megabyte, so the limit is lowered to show the check firing.
        private const float SampleBundleLimit = 0.15f;

        public static void CaptureAll()
        {
            var preferences = new EditorPreferenceStore();
            AuditPreferences.MaxBundleMegabytes.Set(preferences, SampleBundleLimit);
            ValidationPreferences.Scope.Set(preferences, (int)ValidationScopes.Everything);

            var shots = new Queue<Shot>();
            shots.Enqueue(new Shot("validator", new Vector2(1040f, 430f), ValidatorWindow.Open,
                window => ((ValidatorWindow)window).Run(ValidationScopes.Everything)));
            shots.Enqueue(new Shot("addressables-audit", new Vector2(1040f, 520f), AuditWindow.Open, window => ((AuditWindow)window).Run()));
            shots.Enqueue(new Shot("build-runner", new Vector2(900f, 330f), BuildRunnerWindow.Open, null));
            shots.Enqueue(new Shot("scriptableobject-browser", new Vector2(1040f, 430f), DataBrowserWindow.Open, SelectTheSword));
            shots.Enqueue(new Shot("preferences", new Vector2(860f, 420f),
                () => SettingsService.OpenUserPreferences(PreferencesPage.RootPath + "/Validator"), null));

            new CaptureRun(shots, () =>
            {
                AuditPreferences.MaxBundleMegabytes.Reset(preferences);
                ValidationPreferences.Scope.Reset(preferences);
            }).Start();
        }

        // Picks the item type on the left and one item in the middle, the way two clicks would.
        private static void SelectTheSword(EditorWindow window)
        {
            List<ListView> lists = window.rootVisualElement.Query<ListView>().ToList();
            Select(lists[0], item => item is TypeEntry type && type.TypeName == nameof(ItemDefinition));
            Select(lists[1], item => item is AssetRecord record && record.Name == "Sword");
        }

        private static void Select(ListView list, Func<object, bool> wanted)
        {
            for (int i = 0; i < list.itemsSource.Count; i++)
            {
                if (!wanted(list.itemsSource[i])) continue;
                list.selectedIndex = i;
                return;
            }
        }

        private sealed class Shot
        {
            public Shot(string name, Vector2 size, Func<EditorWindow> open, Action<EditorWindow> prepare)
            {
                Name = name;
                Size = size;
                Open = open;
                Prepare = prepare;
            }

            public string Name { get; }

            public Vector2 Size { get; }

            public Func<EditorWindow> Open { get; }

            /// <summary>What to do in the window once it has drawn itself. May be null.</summary>
            public Action<EditorWindow> Prepare { get; }
        }

        /// <summary>Works through the shots one editor update at a time, then ends the editor.</summary>
        private sealed class CaptureRun
        {
            private readonly Queue<Shot> _shots;
            private readonly Action _cleanUp;

            private Shot _current;
            private EditorWindow _window;
            private bool _prepared;
            private double _readyAt;
            private double _giveUpAt;

            public CaptureRun(Queue<Shot> shots, Action cleanUp)
            {
                _shots = shots;
                _cleanUp = cleanUp;
            }

            public void Start()
            {
                Directory.CreateDirectory(Folder);
                _giveUpAt = EditorApplication.timeSinceStartup + GiveUpSeconds;
                EditorApplication.update += Tick;
            }

            private void Tick()
            {
                try
                {
                    Step();
                }
                catch (Exception exception)
                {
                    Debug.LogError("[Capture] " + exception);
                    Finish(1);
                }
            }

            private void Step()
            {
                double now = EditorApplication.timeSinceStartup;
                if (now > _giveUpAt)
                {
                    Debug.LogError("[Capture] Gave up waiting.");
                    Finish(1);
                    return;
                }

                if (_current == null)
                {
                    if (_shots.Count == 0)
                    {
                        Finish(0);
                        return;
                    }

                    _current = _shots.Dequeue();
                    _window = _current.Open();
                    _window.position = new Rect(60f, 60f, _current.Size.x, _current.Size.y);
                    _window.Show();
                    _window.Focus();
                    _prepared = _current.Prepare == null;
                    _readyAt = now + SettleSeconds;
                    return;
                }

                _window.Repaint();
                if (now < _readyAt) return;

                if (!_prepared)
                {
                    _current.Prepare(_window);
                    _prepared = true;
                    _readyAt = now + SettleSeconds;
                    return;
                }

                Save(_window, _current.Name);
                _window.Close();
                _current = null;
            }

            private void Finish(int exitCode)
            {
                EditorApplication.update -= Tick;
                _cleanUp();
                EditorApplication.Exit(exitCode);
            }
        }

        // The copy arrives bottom row first.
        private static void FlipRows(Texture2D image)
        {
            Color32[] pixels = image.GetPixels32();
            int width = image.width;
            int height = image.height;
            var row = new Color32[width];
            for (int y = 0; y < height / 2; y++)
            {
                int top = y * width;
                int bottom = (height - 1 - y) * width;
                Array.Copy(pixels, top, row, 0, width);
                Array.Copy(pixels, bottom, pixels, top, width);
                Array.Copy(row, 0, pixels, bottom, width);
            }

            image.SetPixels32(pixels);
            image.Apply();
        }

        // The view that hosts a window can copy its own pixels, whatever else is on the screen in
        // front of it. That method is internal to the editor, hence the reflection.
        private static void Save(EditorWindow window, string name)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object host = typeof(EditorWindow).GetField("m_Parent", Flags)?.GetValue(window);
            MethodInfo grab = host?.GetType().GetMethod("GrabPixels", Flags);
            if (grab == null) throw new InvalidOperationException("This editor version has no way to copy a window's pixels.");

            float scale = EditorGUIUtility.pixelsPerPoint;
            int width = Mathf.RoundToInt(window.position.width * scale);
            int height = Mathf.RoundToInt(window.position.height * scale);

            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                grab.Invoke(host, new object[] { target, new Rect(0f, 0f, width, height) });
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                FlipRows(image);
                File.WriteAllBytes($"{Folder}/{name}.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                Debug.Log($"[Capture] {name}.png {width}x{height} at scale {scale}");
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
