using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>What differs between platforms, as data. A new platform is a new entry here and in the editor-side table.</summary>
    public sealed class PlatformTraits
    {
        private static readonly Dictionary<BuildPlatform, PlatformTraits> All = new Dictionary<BuildPlatform, PlatformTraits>
        {
            { BuildPlatform.Web, new PlatformTraits("Web", null, null) },
            { BuildPlatform.Windows, new PlatformTraits("Windows", ".exe", null) },
            { BuildPlatform.Android, new PlatformTraits("Android", ".apk", ".aab") },
        };

        private readonly string _extension;
        private readonly string _bundleExtension;

        private PlatformTraits(string name, string extension, string bundleExtension)
        {
            Name = name;
            _extension = extension;
            _bundleExtension = bundleExtension;
        }

        public string Name { get; }

        public static PlatformTraits For(BuildPlatform platform) => All[platform];

        /// <summary>The extension of the file the build produces, or null when it produces a folder.</summary>
        public string ExtensionFor(BuildProfile profile)
        {
            return profile.AndroidAppBundle && _bundleExtension != null ? _bundleExtension : _extension;
        }
    }

    /// <summary>What the planner needs to know about the project, read once.</summary>
    public sealed class ProjectFacts
    {
        public ProjectFacts(string productName, string version, IReadOnlyList<string> enabledScenes,
            ICollection<BuildPlatform> installedPlatforms, BuildPlatform? activePlatform)
        {
            ProductName = productName;
            Version = version;
            EnabledScenes = enabledScenes;
            InstalledPlatforms = installedPlatforms;
            ActivePlatform = activePlatform;
        }

        public string ProductName { get; }

        public string Version { get; }

        public IReadOnlyList<string> EnabledScenes { get; }

        public ICollection<BuildPlatform> InstalledPlatforms { get; }

        /// <summary>The platform the editor is switched to, when it is one the Build Runner knows.</summary>
        public BuildPlatform? ActivePlatform { get; }
    }

    /// <summary>Turns a profile's output template into the location the build is written to.</summary>
    public static class OutputPaths
    {
        public static bool TryResolve(BuildProfile profile, ProjectFacts project, out string location, out string problem)
        {
            location = null;
            problem = null;
            if (profile.OutputPath.Trim().Length == 0)
            {
                problem = "has no output path";
                return false;
            }

            PlatformTraits traits = PlatformTraits.For(profile.Platform);
            string product = FileName(project.ProductName);
            string path = profile.OutputPath.Trim().Replace('\\', '/')
                .Replace("{product}", product)
                .Replace("{version}", FileName(project.Version))
                .Replace("{platform}", traits.Name)
                .Replace("{profile}", FileName(profile.Name));

            int brace = path.IndexOf('{');
            if (brace >= 0)
            {
                int end = path.IndexOf('}', brace);
                problem = $"uses an unknown token '{(end < 0 ? path.Substring(brace) : path.Substring(brace, end - brace + 1))}'";
                return false;
            }

            path = path.TrimEnd('/');
            string extension = traits.ExtensionFor(profile);
            if (extension != null && !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                // A folder was given; the file inside it is named after the product.
                path = path + "/" + product + extension;
            }

            location = path;
            return true;
        }

        public static bool IsAbsolute(string path) => Path.IsPathRooted(path);

        public static bool IsInsideAssets(string path)
        {
            return path.Equals("Assets", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }

        // Product names have spaces and punctuation that do not belong in a file name.
        private static string FileName(string text)
        {
            var name = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.') name.Append(c);
            }

            return name.Length == 0 ? "Build" : name.ToString();
        }
    }
}
