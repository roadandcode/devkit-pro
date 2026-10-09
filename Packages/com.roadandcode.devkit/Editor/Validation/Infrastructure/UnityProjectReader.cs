using System;
using System.Collections.Generic;
using System.IO;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>
    /// Reads scenes, prefabs and assets under <c>Assets</c>. Scenes that are not open are opened
    /// additively, read and closed again, so the scenes the person has open are left as they were.
    /// </summary>
    public sealed class UnityProjectReader : IProjectReader
    {
        public const string UntitledScene = "(untitled scene)";

        private static readonly string[] Roots = { "Assets" };

        public ReadSummary Read(ValidationScope scope, Action<ObjectRecord> visit, IProgressSink progress)
        {
            var builder = new RecordBuilder();
            var summary = new ReadSummary();
            var seenScenes = new HashSet<string>();

            if (Has(scope, ValidationScope.OpenScenes)) ReadOpenScenes(builder, visit, summary, seenScenes);

            List<string> scenes = ScenePaths(scope, seenScenes);
            string[] prefabs = Has(scope, ValidationScope.Prefabs) ? Paths("t:Prefab") : new string[0];
            string[] materials = Has(scope, ValidationScope.Assets) ? Paths("t:Material") : new string[0];
            List<string> assets = Has(scope, ValidationScope.Assets) ? AssetFiles() : new List<string>();

            float total = Math.Max(1, scenes.Count + prefabs.Length + materials.Length + assets.Count);
            int done = 0;

            foreach (string path in scenes)
            {
                if (!progress.Report(path, done++ / total)) return Cancel(summary);
                ReadSceneAt(path, builder, visit, summary);
            }

            foreach (string path in prefabs)
            {
                if (!progress.Report(path, done++ / total)) return Cancel(summary);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                Walk(prefab, path, prefab.name, ObjectKind.PrefabObject, builder, visit, summary);
                summary.Prefabs++;
            }

            foreach (string path in materials)
            {
                if (!progress.Report(path, done++ / total)) return Cancel(summary);
                Visit(builder.ForAsset(AssetDatabase.LoadMainAssetAtPath(path), path, string.Empty), visit, summary);
                summary.Assets++;
            }

            foreach (string path in assets)
            {
                if (!progress.Report(path, done++ / total)) return Cancel(summary);
                if (ReadAssetFile(path, builder, visit, summary)) summary.Assets++;
            }

            return summary;
        }

        private static bool Has(ValidationScope scope, ValidationScope part) => (scope & part) != 0;

        private static ReadSummary Cancel(ReadSummary summary)
        {
            summary.Cancelled = true;
            return summary;
        }

        private static void ReadOpenScenes(RecordBuilder builder, Action<ObjectRecord> visit, ReadSummary summary, HashSet<string> seen)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                string path = string.IsNullOrEmpty(scene.path) ? UntitledScene : scene.path;
                ReadScene(scene, path, builder, visit, summary);
                seen.Add(path);
            }
        }

        private static List<string> ScenePaths(ValidationScope scope, HashSet<string> seen)
        {
            var paths = new List<string>();
            if (Has(scope, ValidationScope.AllScenes))
            {
                foreach (string path in Paths("t:Scene"))
                {
                    if (seen.Add(path)) paths.Add(path);
                }
            }
            else if (Has(scope, ValidationScope.BuildScenes))
            {
                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                {
                    if (scene.enabled && File.Exists(scene.path) && seen.Add(scene.path)) paths.Add(scene.path);
                }
            }

            return paths;
        }

        private static void ReadSceneAt(string path, RecordBuilder builder, Action<ObjectRecord> visit, ReadSummary summary)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;

            // Opening a scene is not allowed in play mode; what is loaded can still be read.
            if (!wasLoaded && EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                ReadScene(scene, path, builder, visit, summary);
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ReadScene(Scene scene, string path, RecordBuilder builder, Action<ObjectRecord> visit, ReadSummary summary)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Walk(root, path, root.name, ObjectKind.SceneObject, builder, visit, summary);
            }

            summary.Scenes++;
        }

        private static void Walk(GameObject gameObject, string assetPath, string objectPath, ObjectKind kind,
            RecordBuilder builder, Action<ObjectRecord> visit, ReadSummary summary)
        {
            Visit(builder.ForGameObject(gameObject, assetPath, objectPath, kind), visit, summary);

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                Walk(child.gameObject, assetPath, objectPath + "/" + child.name, kind, builder, visit, summary);
            }
        }

        // A .asset file can hold a ScriptableObject, several of them, or something else entirely
        // (a mesh, lighting data). Only the first two are read.
        private static bool ReadAssetFile(string path, RecordBuilder builder, Action<ObjectRecord> visit, ReadSummary summary)
        {
            Type mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (mainType != null && !typeof(ScriptableObject).IsAssignableFrom(mainType)) return false;

            // A null entry is an object whose script could not be found. It has no name to report,
            // so the ones inside a healthy asset are counted and reported once for the file.
            Object main = AssetDatabase.LoadMainAssetAtPath(path);
            int missing = 0;
            foreach (Object item in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (item == null) missing++;
                else if (item is ScriptableObject) Visit(builder.ForAsset(item, path, item == main ? string.Empty : item.name), visit, summary);
            }

            if (main == null)
            {
                Visit(builder.ForAsset(null, path, string.Empty), visit, summary);
                missing--;
            }

            if (missing > 0) Visit(builder.ForAsset(null, path, missing == 1 ? "1 sub-asset" : $"{missing} sub-assets"), visit, summary);
            return true;
        }

        private static void Visit(ObjectRecord record, Action<ObjectRecord> visit, ReadSummary summary)
        {
            visit(record);
            summary.Objects++;
        }

        private static string[] Paths(string filter)
        {
            string[] guids = AssetDatabase.FindAssets(filter, Roots);
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++) paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            Array.Sort(paths, StringComparer.Ordinal);
            return paths;
        }

        // Found on disk, not through a type search: an asset whose script is missing has no type to search for.
        private static List<string> AssetFiles()
        {
            var paths = new List<string>();
            foreach (string file in Directory.EnumerateFiles(Roots[0], "*.asset", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                // Folders Unity does not import (hidden, or ending in ~) have no GUID.
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets))) paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }
    }
}
