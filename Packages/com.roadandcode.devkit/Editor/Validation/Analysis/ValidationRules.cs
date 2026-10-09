using System.Collections.Generic;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>One check. It looks at one object and adds whatever it finds.</summary>
    public interface IValidationRule
    {
        string Id { get; }

        void Check(ObjectRecord record, List<Finding> findings);
    }

    /// <summary>A component whose script file was deleted or never arrived.</summary>
    public sealed class MissingScriptRule : IValidationRule
    {
        public const string RuleId = "missing-script";

        public string Id => RuleId;

        public void Check(ObjectRecord record, List<Finding> findings)
        {
            for (int i = 0; i < record.Components.Count; i++)
            {
                if (!record.Components[i].IsMissingScript) continue;

                string what = record.Kind == ObjectKind.Asset ? "The script this asset was made from is missing" : "A component's script is missing";
                findings.Add(new Finding(RuleId, Severity.Error, what, record.AssetPath, record.ObjectPath, $"component {i + 1}"));
            }
        }
    }

    /// <summary>A reference that was set once and now points at nothing: a deleted asset, a deleted object.</summary>
    public sealed class MissingReferenceRule : IValidationRule
    {
        public const string RuleId = "missing-reference";

        public string Id => RuleId;

        public void Check(ObjectRecord record, List<Finding> findings)
        {
            for (int c = 0; c < record.Components.Count; c++)
            {
                ComponentRecord component = record.Components[c];
                for (int r = 0; r < component.References.Count; r++)
                {
                    ReferenceRecord reference = component.References[r];
                    if (reference.State != ReferenceState.Missing) continue;

                    string field = FieldNames.Readable(reference.PropertyPath);
                    findings.Add(new Finding(RuleId, Severity.Error, $"{component.TypeName}.{field} points at something that no longer exists",
                        record.AssetPath, record.ObjectPath, $"{component.TypeName}.{reference.PropertyPath}"));
                }
            }
        }
    }

    /// <summary>
    /// A reference field on one of the project's own scripts that was left empty. Fields that are
    /// allowed to be empty are named in a list of patterns.
    /// </summary>
    public sealed class UnassignedReferenceRule : IValidationRule
    {
        public const string RuleId = "unassigned-reference";

        private readonly NamePatterns _optional;

        public UnassignedReferenceRule(NamePatterns optional)
        {
            _optional = optional;
        }

        public string Id => RuleId;

        public void Check(ObjectRecord record, List<Finding> findings)
        {
            for (int c = 0; c < record.Components.Count; c++)
            {
                ComponentRecord component = record.Components[c];
                if (!component.IsProjectScript) continue;

                for (int r = 0; r < component.References.Count; r++)
                {
                    ReferenceRecord reference = component.References[r];
                    if (reference.State != ReferenceState.None) continue;
                    if (_optional.Matches(FieldNames.Root(reference.PropertyPath))) continue;

                    string field = FieldNames.Readable(reference.PropertyPath);
                    findings.Add(new Finding(RuleId, Severity.Warning, $"{component.TypeName}.{field} is not assigned",
                        record.AssetPath, record.ObjectPath, $"{component.TypeName}.{reference.PropertyPath}"));
                }
            }
        }
    }

    /// <summary>An object with nothing on it and nothing under it. Often a leftover; sometimes a marker.</summary>
    public sealed class EmptyObjectRule : IValidationRule
    {
        public const string RuleId = "empty-object";

        public string Id => RuleId;

        public void Check(ObjectRecord record, List<Finding> findings)
        {
            if (record.Kind == ObjectKind.Asset || record.IsMissingPrefab) return;
            if (record.ChildCount > 0 || record.Components.Count != 1 || record.Components[0].IsMissingScript) return;

            findings.Add(new Finding(RuleId, Severity.Info, "Has no components and no children", record.AssetPath, record.ObjectPath));
        }
    }

    /// <summary>An instance of a prefab that has been deleted.</summary>
    public sealed class MissingPrefabRule : IValidationRule
    {
        public const string RuleId = "missing-prefab";

        public string Id => RuleId;

        public void Check(ObjectRecord record, List<Finding> findings)
        {
            if (!record.IsMissingPrefab) return;

            findings.Add(new Finding(RuleId, Severity.Error, "The prefab this instance came from is missing", record.AssetPath, record.ObjectPath));
        }
    }

    /// <summary>Turns serialized property paths into something a person would recognise from the Inspector.</summary>
    public static class FieldNames
    {
        private const string ArrayMarker = ".Array.data[";

        /// <summary><c>waves.Array.data[1].enemy</c> becomes <c>waves[1].enemy</c>.</summary>
        public static string Readable(string propertyPath) => propertyPath.Replace(ArrayMarker, "[");

        /// <summary>The field declared on the script: <c>waves</c> for <c>waves.Array.data[1].enemy</c>.</summary>
        public static string Root(string propertyPath)
        {
            int dot = propertyPath.IndexOf('.');
            return dot < 0 ? propertyPath : propertyPath.Substring(0, dot);
        }
    }
}
