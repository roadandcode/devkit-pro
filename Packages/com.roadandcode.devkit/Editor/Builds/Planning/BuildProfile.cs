using System;
using UnityEngine;

namespace RoadAndCode.DevKit.Builds
{
    public enum BuildPlatform
    {
        Web = 0,
        Windows = 1,
        Android = 2,
    }

    public enum WebCompression
    {
        /// <summary>Leave whatever the project's player settings say.</summary>
        ProjectSetting = 0,
        Disabled = 1,
        Gzip = 2,
        Brotli = 3,
    }

    /// <summary>One build the project can make: a platform, where the result goes and the options that differ per platform.</summary>
    [Serializable]
    public sealed class BuildProfile
    {
        [SerializeField] private string _name;
        [SerializeField] private bool _enabled = true;
        [SerializeField] private BuildPlatform _platform;

        [Tooltip("Relative to the project folder. Tokens: {product} {version} {platform} {profile}. For Windows and Android a path without a file name gets <product>.exe / .apk / .aab added.")]
        [SerializeField] private string _outputPath;

        [SerializeField] private bool _development;

        [Tooltip("Web only. Gzip with the fallback on loads from any static host without server configuration.")]
        [SerializeField] private WebCompression _webCompression = WebCompression.ProjectSetting;

        [Tooltip("Web only, used when the compression is set here. Ships a loader that unpacks the build in the browser when the host does not send the content-encoding header.")]
        [SerializeField] private bool _webDecompressionFallback = true;

        [Tooltip("Android only. Builds an .aab for the store, otherwise an .apk.")]
        [SerializeField] private bool _androidAppBundle;

        public BuildProfile(string name, BuildPlatform platform, string outputPath)
        {
            _name = name;
            _platform = platform;
            _outputPath = outputPath;
        }

        public string Name => _name ?? string.Empty;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public BuildPlatform Platform => _platform;

        public string OutputPath => _outputPath ?? string.Empty;

        public bool Development => _development;

        public WebCompression WebCompression => _webCompression;

        public bool WebDecompressionFallback => _webDecompressionFallback;

        public bool AndroidAppBundle => _androidAppBundle;

        public BuildProfile WithWeb(WebCompression compression, bool decompressionFallback)
        {
            _webCompression = compression;
            _webDecompressionFallback = decompressionFallback;
            return this;
        }

        public BuildProfile WithAppBundle(bool appBundle)
        {
            _androidAppBundle = appBundle;
            return this;
        }

        public BuildProfile WithDevelopment(bool development)
        {
            _development = development;
            return this;
        }
    }
}
