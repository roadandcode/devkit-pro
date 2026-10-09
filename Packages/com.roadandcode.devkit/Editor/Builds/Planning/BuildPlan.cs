using System.Collections.Generic;
using UnityEngine;

namespace RoadAndCode.DevKit.Builds
{
    /// <summary>
    /// The builds a project makes, kept as an asset so the list is versioned with the project.
    /// The Build Runner window and the command line both read it.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildPlan", menuName = "DevKit Pro/Build Plan")]
    public sealed class BuildPlan : ScriptableObject
    {
        [Tooltip("Stop at the first build that fails instead of trying the rest.")]
        [SerializeField] private bool _stopOnFailure = true;

        [SerializeField] private List<BuildProfile> _profiles = new List<BuildProfile>();

        public bool StopOnFailure => _stopOnFailure;

        public IReadOnlyList<BuildProfile> Profiles => _profiles;

        /// <summary>Fills an empty plan with the three builds my projects make.</summary>
        public void AddDefaults()
        {
            _profiles.Add(new BuildProfile("Web", BuildPlatform.Web, "Builds/Web").WithWeb(WebCompression.Gzip, true));
            _profiles.Add(new BuildProfile("Windows", BuildPlatform.Windows, "Builds/Windows/{product}.exe"));
            _profiles.Add(new BuildProfile("Android", BuildPlatform.Android, "Builds/Android/{product}.apk"));
        }

        public void Add(BuildProfile profile) => _profiles.Add(profile);
    }
}
