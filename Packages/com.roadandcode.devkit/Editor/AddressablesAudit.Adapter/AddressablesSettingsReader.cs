using System;
using System.Collections.Generic;
using System.IO;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace RoadAndCode.DevKit.AddressablesAudit.Adapter
{
    /// <summary>
    /// Reads the project's Addressables settings into the audit's own records. This is the only
    /// code in the package that touches the Addressables API.
    /// </summary>
    public sealed class AddressablesSettingsReader : IAddressablesReader
    {
        private const string AssetsRoot = "Assets";
        private const string NoSettings = "This project has no Addressables settings yet. Create them from Window > Asset Management > Addressables > Groups.";
        private const int ProgressEvery = 32;

        // Where a build leaves its bundles: the local build path, and the default remote one.
        private static readonly string[] BundleFolders = { "Library/com.unity.addressables/aa", "ServerData" };

        public bool TryRead(AuditOptions options, IProgressSink progress, out AddressablesSnapshot snapshot, out string problem)
        {
            snapshot = null;
            problem = null;
            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
            {
                problem = NoSettings;
                return false;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            var groups = new List<GroupRecord>();
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (AddressableAssetGroup group in settings.groups)
            {
                // The built-in group lists Resources and the scene list; it is not content anyone packed.
                if (group == null || group.HasSchema<PlayerDataGroupSchema>()) continue;

                var entries = new List<EntryRecord>();
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    entries.Add(Read(entry, options.IncludePackageAssets));
                    names.Add(entry.address);
                    foreach (string label in entry.labels) names.Add(label);
                }

                groups.Add(new GroupRecord(group.Name, entries));
            }

            ReferenceIndex references = ReadReferences(settings.ConfigFolder, names, progress);
            snapshot = new AddressablesSnapshot(groups, ReadBundles(), references, BuildScenes());
            return true;
        }

        private static EntryRecord Read(AddressableAssetEntry entry, bool includePackageAssets)
        {
            string path = entry.AssetPath;
            bool isFolder = !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);
            bool exists = !string.IsNullOrEmpty(path) && (isFolder || File.Exists(path));
            var labels = new List<string>(entry.labels);
            if (!exists) return new EntryRecord(entry.guid, entry.address, path, labels, assetExists: false);

            return new EntryRecord(entry.guid, entry.address, path, labels, SizeOf(path, isFolder),
                Dependencies(path, isFolder, includePackageAssets), true, entry.IsScene, isFolder);
        }

        private static long SizeOf(string path, bool isFolder)
        {
            if (!isFolder) return new FileInfo(path).Length;

            long total = 0;
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) total += new FileInfo(file).Length;
            }

            return total;
        }

        private static List<string> Dependencies(string path, bool isFolder, bool includePackageAssets)
        {
            string[] roots = isFolder ? AssetsIn(path) : new[] { path };
            var dependencies = new List<string>();
            foreach (string dependency in AssetDatabase.GetDependencies(roots, true))
            {
                if (Array.IndexOf(roots, dependency) >= 0) continue;
                if (dependency.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || dependency.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                if (!includePackageAssets && !dependency.StartsWith(AssetsRoot + "/", StringComparison.Ordinal)) continue;
                dependencies.Add(dependency);
            }

            return dependencies;
        }

        private static string[] AssetsIn(string folder)
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            var paths = new List<string>(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path)) paths.Add(path);
            }

            return paths.ToArray();
        }

        private static List<BundleRecord> ReadBundles()
        {
            var bundles = new List<BundleRecord>();
            foreach (string folder in BundleFolders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.EnumerateFiles(folder, "*.bundle", SearchOption.AllDirectories))
                {
                    bundles.Add(new BundleRecord(file.Replace('\\', '/'), new FileInfo(file).Length));
                }
            }

            return bundles;
        }

        private static List<string> BuildScenes()
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) scenes.Add(scene.path);
            }

            return scenes;
        }

        // Scenes, prefabs and assets are read as text for the references they hold, scripts for
        // the names they mention. Editor and test code never runs in a player, so it is not asked.
        private static ReferenceIndex ReadReferences(string settingsFolder, HashSet<string> names, IProgressSink progress)
        {
            var index = new ReferenceIndex();
            var files = new List<string>(Directory.EnumerateFiles(AssetsRoot, "*.*", SearchOption.AllDirectories));
            string ownFolder = settingsFolder.TrimEnd('/') + "/";

            for (int i = 0; i < files.Count; i++)
            {
                if (i % ProgressEvery == 0 && !progress.Report(files[i], i / (float)files.Count)) break;

                string path = files[i].Replace('\\', '/');

                // The settings folder lists every address itself, which would make everything look referenced.
                if (path.StartsWith(ownFolder, StringComparison.OrdinalIgnoreCase)) continue;

                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".cs")
                {
                    if (!IsEditorOrTestCode(path)) index.AddCode(File.ReadAllText(path));
                }
                else if ((extension == ".unity" || extension == ".prefab" || extension == ".asset") && IsText(path))
                {
                    index.AddSerialized(File.ReadAllText(path), names);
                }
            }

            return index;
        }

        private static bool IsEditorOrTestCode(string path)
        {
            return path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Some .asset files are binary (lighting data, a terrain). Those start differently.
        private static bool IsText(string path)
        {
            var header = new byte[5];
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Read(header, 0, header.Length) < header.Length) return false;
            }

            return header[0] == '%' && header[1] == 'Y' && header[2] == 'A' && header[3] == 'M' && header[4] == 'L';
        }
    }
}
