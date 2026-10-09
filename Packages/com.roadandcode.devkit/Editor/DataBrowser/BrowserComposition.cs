using RoadAndCode.DevKit.Common;
using UnityEditor;
using UnityEngine;

namespace RoadAndCode.DevKit.DataBrowser
{
    /// <summary>The one place the browser's concrete types are named and wired together.</summary>
    public static class BrowserComposition
    {
        public static BrowserPresenter Present(IBrowserView view)
        {
            return new BrowserPresenter(view, new UnityCatalogReader(), new EditorPreferenceStore(), new ProjectWindowRevealer(),
                () => new EditorProgressBar("Reading ScriptableObjects"));
        }
    }

    public sealed class ProjectWindowRevealer : IAssetRevealer
    {
        public void Reveal(AssetRecord record)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(record.Path);
            if (asset == null) return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
