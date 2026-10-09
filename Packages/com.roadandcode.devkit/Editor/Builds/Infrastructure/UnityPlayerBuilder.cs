using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>How my platforms map onto Unity's build targets.</summary>
    public static class PlatformTargets
    {
        private static readonly Dictionary<BuildPlatform, BuildTarget> Targets = new Dictionary<BuildPlatform, BuildTarget>
        {
            { BuildPlatform.Web, BuildTarget.WebGL },
            { BuildPlatform.Windows, BuildTarget.StandaloneWindows64 },
            { BuildPlatform.Android, BuildTarget.Android },
        };

        public static IEnumerable<BuildPlatform> All => Targets.Keys;

        public static BuildTarget TargetFor(BuildPlatform platform) => Targets[platform];

        public static BuildPlatform? PlatformFor(BuildTarget target)
        {
            foreach (KeyValuePair<BuildPlatform, BuildTarget> pair in Targets)
            {
                if (pair.Value == target) return pair.Key;
            }

            return null;
        }
    }

    public sealed class UnityProjectInfo : IProjectInfo
    {
        public ProjectFacts Read()
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && File.Exists(scene.path)) scenes.Add(scene.path);
            }

            var installed = new HashSet<BuildPlatform>();
            foreach (BuildPlatform platform in PlatformTargets.All)
            {
                BuildTarget target = PlatformTargets.TargetFor(platform);
                if (BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target)) installed.Add(platform);
            }

            return new ProjectFacts(PlayerSettings.productName, PlayerSettings.bundleVersion, scenes, installed,
                PlatformTargets.PlatformFor(EditorUserBuildSettings.activeBuildTarget));
        }
    }

    /// <summary>
    /// Builds a player with <c>BuildPipeline</c>. A profile's per-platform options are applied to
    /// the player settings for the length of the build and put back afterwards, so building does
    /// not leave the project's settings changed.
    /// </summary>
    public sealed class UnityPlayerBuilder : IPlayerBuilder
    {
        public BuildOutcome Build(BuildRequest request)
        {
            BuildProfile profile = request.Profile;
            string[] scenes = new string[request.Scenes.Count];
            for (int i = 0; i < scenes.Length; i++) scenes[i] = request.Scenes[i];

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = PlatformTargets.TargetFor(profile.Platform),
                locationPathName = request.Location,
                options = profile.Development ? BuildOptions.Development : BuildOptions.None,
            };

            Action restore = ApplyOverrides(profile);
            try
            {
                BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
                return new BuildOutcome(profile.Name, summary.result == BuildResult.Succeeded, request.Location, (long)summary.totalSize,
                    (float)summary.totalTime.TotalSeconds, summary.totalErrors, summary.totalWarnings, DateTime.UtcNow);
            }
            finally
            {
                restore();
                AssetDatabase.SaveAssets();
            }
        }

        private static Action ApplyOverrides(BuildProfile profile)
        {
            WebGLCompressionFormat compression = PlayerSettings.WebGL.compressionFormat;
            bool fallback = PlayerSettings.WebGL.decompressionFallback;
            bool appBundle = EditorUserBuildSettings.buildAppBundle;

            if (profile.Platform == BuildPlatform.Web && profile.WebCompression != WebCompression.ProjectSetting)
            {
                PlayerSettings.WebGL.compressionFormat = CompressionFor(profile.WebCompression);
                PlayerSettings.WebGL.decompressionFallback = profile.WebDecompressionFallback;
            }

            if (profile.Platform == BuildPlatform.Android) EditorUserBuildSettings.buildAppBundle = profile.AndroidAppBundle;

            return () =>
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                EditorUserBuildSettings.buildAppBundle = appBundle;
            };
        }

        private static WebGLCompressionFormat CompressionFor(WebCompression compression)
        {
            if (compression == WebCompression.Gzip) return WebGLCompressionFormat.Gzip;
            return compression == WebCompression.Brotli ? WebGLCompressionFormat.Brotli : WebGLCompressionFormat.Disabled;
        }
    }
}
