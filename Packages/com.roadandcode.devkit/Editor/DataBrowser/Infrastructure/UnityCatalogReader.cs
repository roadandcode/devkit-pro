using System;
using System.Collections.Generic;
using System.IO;
using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoadAndCode.DevKit.DataBrowser
{
    /// <summary>
    /// Finds ScriptableObject assets under <c>Assets</c> by their file's main type, without loading
    /// them. Sub-assets do need a load, so they are read only when asked for.
    /// </summary>
    public sealed class UnityCatalogReader : ICatalogReader
    {
        private const string Root = "Assets";
        private const int ProgressEvery = 64;

        public IReadOnlyList<AssetRecord> Read(bool includeSubAssets, IProgressSink progress)
        {
            HashSet<string> projectAssemblies = ProjectAssemblies();
            var files = new List<string>(Directory.EnumerateFiles(Root, "*.asset", SearchOption.AllDirectories));
            var records = new List<AssetRecord>();

            for (int i = 0; i < files.Count; i++)
            {
                if (i % ProgressEvery == 0 && !progress.Report(files[i], i / (float)files.Count)) break;

                string path = files[i].Replace('\\', '/');
                string guid = AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets);
                if (string.IsNullOrEmpty(guid)) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == null)
                {
                    records.Add(AssetRecord.MissingScript(guid, path, name));
                    continue;
                }

                if (!typeof(ScriptableObject).IsAssignableFrom(type)) continue;

                records.Add(Record(guid, path, name, type, projectAssemblies, false));
                if (includeSubAssets) AddSubAssets(guid, path, projectAssemblies, records);
            }

            return records;
        }

        private static void AddSubAssets(string guid, string path, HashSet<string> projectAssemblies, List<AssetRecord> records)
        {
            foreach (Object item in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                if (item is ScriptableObject) records.Add(Record(guid, path, item.name, item.GetType(), projectAssemblies, true));
            }
        }

        private static AssetRecord Record(string guid, string path, string name, Type type, HashSet<string> projectAssemblies, bool isSubAsset)
        {
            bool isProjectType = projectAssemblies.Contains(type.Assembly.GetName().Name);
            return new AssetRecord(guid, path, name, type.Name, type.FullName, isProjectType, isSubAsset);
        }

        // An assembly is the project's own when its source is under Assets or in a package kept
        // inside the project or on this disk.
        private static HashSet<string> ProjectAssemblies()
        {
            var names = new HashSet<string>();
            foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                if (assembly.sourceFiles.Length > 0 && IsProjectPath(assembly.sourceFiles[0])) names.Add(assembly.name);
            }

            return names;
        }

        private static bool IsProjectPath(string path)
        {
            if (path.StartsWith(Root + "/", StringComparison.Ordinal)) return true;

            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            return package != null && (package.source == PackageSource.Embedded || package.source == PackageSource.Local);
        }
    }
}
