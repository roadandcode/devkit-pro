using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoadAndCode.DevKit.Sandbox.Editor
{
    /// <summary>
    /// Writes the package as a <c>.unitypackage</c> for people who would sooner import a file than
    /// add a Git URL. The file keeps the package's own paths, so importing it puts the package
    /// under <c>Packages/</c> just as an embedded package would sit there.
    /// <code>Unity -batchmode -quit -projectPath . -executeMethod RoadAndCode.DevKit.Sandbox.Editor.PackageExport.Export</code>
    /// </summary>
    public static class PackageExport
    {
        private const string PackageRoot = "Packages/com.roadandcode.devkit";
        private const string TestsFolder = PackageRoot + "/Tests";
        private const string OutputFolder = "Builds";

        public static void Export()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackageRoot + "/package.json");
            string output = $"{OutputFolder}/DevKitPro-{package.version}.unitypackage";

            // The tests are for working on the package, not for using it.
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { PackageRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || path.StartsWith(TestsFolder, StringComparison.Ordinal)) continue;
                paths.Add(path);
            }

            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.ExportPackage(paths.ToArray(), output, ExportPackageOptions.Default);
            Debug.Log($"[Sandbox] Exported {paths.Count} files to {output} ({new FileInfo(output).Length / 1024} KB)");
        }
    }
}
