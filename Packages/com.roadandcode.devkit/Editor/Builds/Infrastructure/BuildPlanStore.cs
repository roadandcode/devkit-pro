using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>Where the project's build plan is found, created and saved.</summary>
    public interface IBuildPlanStore
    {
        /// <returns>The plan, or null when the project has none yet.</returns>
        BuildPlan Find();

        BuildPlan Create();

        void Save(BuildPlan plan);

        /// <summary>Shows the plan in the Inspector for editing.</summary>
        void Select(BuildPlan plan);
    }

    /// <summary>The plan is an asset. The first one found is used, so it can live wherever the project keeps its settings.</summary>
    public sealed class AssetBuildPlanStore : IBuildPlanStore
    {
        private const string DefaultFolder = "Assets/Settings/DevKit";
        private const string DefaultPath = DefaultFolder + "/BuildPlan.asset";

        public BuildPlan Find()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(BuildPlan));
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<BuildPlan>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public BuildPlan Find(string path) => AssetDatabase.LoadAssetAtPath<BuildPlan>(path);

        public BuildPlan Create()
        {
            Directory.CreateDirectory(DefaultFolder);
            AssetDatabase.Refresh();

            var plan = ScriptableObject.CreateInstance<BuildPlan>();
            plan.AddDefaults();
            AssetDatabase.CreateAsset(plan, DefaultPath);
            AssetDatabase.SaveAssets();
            return plan;
        }

        public void Save(BuildPlan plan)
        {
            EditorUtility.SetDirty(plan);
            AssetDatabase.SaveAssetIfDirty(plan);
        }

        public void Select(BuildPlan plan)
        {
            Selection.activeObject = plan;
            EditorGUIUtility.PingObject(plan);
        }
    }

    /// <summary>
    /// Keeps the last result of each profile in the Library folder: per machine, never committed,
    /// and still there after the script reload that follows a build.
    /// </summary>
    public sealed class LibraryBuildHistory : IBuildHistory
    {
        private const string FilePath = "Library/DevKitPro/BuildHistory.json";

        [Serializable]
        private sealed class Store
        {
            [SerializeField] private List<BuildOutcome> _outcomes = new List<BuildOutcome>();

            public List<BuildOutcome> Outcomes => _outcomes;
        }

        public BuildOutcome Last(string profile) => Load().Outcomes.Find(outcome => outcome.Profile == profile);

        public void Record(BuildOutcome outcome)
        {
            Store store = Load();
            store.Outcomes.RemoveAll(existing => existing.Profile == outcome.Profile);
            store.Outcomes.Add(outcome);

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(store, true));
        }

        private static Store Load()
        {
            if (!File.Exists(FilePath)) return new Store();
            return JsonUtility.FromJson<Store>(File.ReadAllText(FilePath)) ?? new Store();
        }
    }

    public interface IOutputRevealer
    {
        void Reveal(string location);
    }

    public sealed class FileBrowserRevealer : IOutputRevealer
    {
        public void Reveal(string location) => EditorUtility.RevealInFinder(location);
    }
}
