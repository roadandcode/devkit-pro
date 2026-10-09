using System.Collections.Generic;
using System.Diagnostics;
using RoadAndCode.DevKit.Common;
using UnityEditor;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>Reads the project and audits it. The window and the command line both go through here.</summary>
    public sealed class AuditRunner
    {
        public const string NotInstalled = "The Addressables package is not installed in this project, so there is nothing to audit.";

        private readonly IAddressablesReader _reader;
        private readonly Auditor _auditor;

        /// <param name="reader">Null when the Addressables package is not installed.</param>
        public AuditRunner(IAddressablesReader reader, Auditor auditor)
        {
            _reader = reader;
            _auditor = auditor;
        }

        public bool IsAvailable => _reader != null;

        public bool TryRun(AuditOptions options, IProgressSink progress, out AuditResult result, out string problem)
        {
            result = null;
            if (_reader == null)
            {
                problem = NotInstalled;
                return false;
            }

            var watch = Stopwatch.StartNew();
            if (!_reader.TryRead(options, progress, out AddressablesSnapshot snapshot, out problem)) return false;

            result = _auditor.Audit(snapshot, options, watch.Elapsed);
            return true;
        }
    }

    /// <summary>Per-user settings of the audit.</summary>
    public static class AuditPreferences
    {
        public static readonly NumberPreference MaxBundleMegabytes = new NumberPreference(
            "AddressablesAudit.MaxBundleMegabytes",
            "Bundle size limit (MB)",
            "A built bundle larger than this is reported. Without built bundles the same limit is applied to each group's source files.",
            AuditOptions.DefaultMaxBundleMegabytes, 0.01f, 4096f);

        public static readonly BoolPreference IncludePackageAssets = new BoolPreference(
            "AddressablesAudit.IncludePackageAssets",
            "Count package assets as duplicates",
            "Also report shared dependencies that live in packages, such as a render pipeline's shaders.",
            false);

        public static readonly IReadOnlyList<Preference> Page = new Preference[] { MaxBundleMegabytes, IncludePackageAssets };

        public static AuditOptions Options(IPreferenceStore store)
        {
            return new AuditOptions
            {
                MaxBundleMegabytes = MaxBundleMegabytes.Get(store),
                IncludePackageAssets = IncludePackageAssets.Get(store),
            };
        }
    }

    public static class AuditPreferencesPage
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return PreferencesPage.Create(
                PreferencesPage.RootPath + "/Addressables Audit",
                "Addressables Audit",
                "Limits the Addressables Audit window uses. A command-line run takes its limit from -devkitMaxBundleMb instead.",
                AuditPreferences.Page,
                new EditorPreferenceStore());
        }
    }
}
