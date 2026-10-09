using System;
using RoadAndCode.DevKit.Common;
using UnityEditor;

namespace RoadAndCode.DevKit.AddressablesAudit
{
    /// <summary>The one place the audit's concrete types are named and wired together.</summary>
    public static class AuditComposition
    {
        public static AuditPresenter Present(IAuditView view)
        {
            var preferences = new EditorPreferenceStore();
            return new AuditPresenter(view, Runner(), preferences, new EditorFindingLocator(preferences),
                new ReportFolderSaver(preferences), () => new EditorProgressBar("Auditing Addressables"));
        }

        public static AuditRunner Runner() => new AuditRunner(FindReader(), new Auditor(Auditor.AllRules()));

        /// <summary>
        /// The reader lives in an assembly that only exists when Addressables is installed, so it
        /// cannot be named here. It is the public class with a public parameterless constructor
        /// that implements the interface; null when there is none.
        /// </summary>
        public static IAddressablesReader FindReader()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IAddressablesReader>())
            {
                if (!type.IsPublic || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) continue;
                return (IAddressablesReader)Activator.CreateInstance(type);
            }

            return null;
        }
    }
}
