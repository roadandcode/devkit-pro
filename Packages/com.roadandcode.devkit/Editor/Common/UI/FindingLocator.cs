using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Takes the person to what a finding is about.</summary>
    public interface IFindingLocator
    {
        void Reveal(Finding finding);
    }

    /// <summary>
    /// Selects and pings the object when its scene is open, otherwise the asset the finding names.
    /// </summary>
    public sealed class EditorFindingLocator : IFindingLocator
    {
        private readonly IPreferenceStore _preferences;

        public EditorFindingLocator(IPreferenceStore preferences)
        {
            _preferences = preferences;
        }

        public void Reveal(Finding finding)
        {
            if (!GeneralPreferences.SelectOnClick.Get(_preferences)) return;

            Object target = Find(finding);
            if (target == null) return;

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static Object Find(Finding finding)
        {
            if (finding.AssetPath.Length == 0) return null;

            if (finding.ObjectPath.Length > 0)
            {
                Scene scene = SceneManager.GetSceneByPath(finding.AssetPath);
                if (scene.IsValid() && scene.isLoaded)
                {
                    GameObject inScene = FindInScene(scene, finding.ObjectPath);
                    if (inScene != null) return inScene;
                }
            }

            return AssetDatabase.LoadMainAssetAtPath(finding.AssetPath);
        }

        private static GameObject FindInScene(Scene scene, string objectPath)
        {
            int slash = objectPath.IndexOf('/');
            string rootName = slash < 0 ? objectPath : objectPath.Substring(0, slash);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != rootName) continue;
                if (slash < 0) return root;

                Transform child = root.transform.Find(objectPath.Substring(slash + 1));
                if (child != null) return child.gameObject;
            }

            return null;
        }
    }

    /// <summary>Saves a report from a window into the report folder and says where it went.</summary>
    public interface IReportSaver
    {
        string Save(ScanReport report);
    }

    public sealed class ReportFolderSaver : IReportSaver
    {
        private readonly IPreferenceStore _preferences;

        public ReportFolderSaver(IPreferenceStore preferences)
        {
            _preferences = preferences;
        }

        public string Save(ScanReport report)
        {
            string folder = GeneralPreferences.ReportFolder.Get(_preferences);
            string path = System.IO.Path.Combine(folder, ReportFile.NameFor(report, System.DateTime.Now)).Replace('\\', '/');
            ReportFile.Write(path, report);
            return path;
        }
    }
}
