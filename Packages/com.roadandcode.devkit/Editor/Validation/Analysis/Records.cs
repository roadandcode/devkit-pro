using System.Collections.Generic;

namespace RoadAndCode.DevKit.Validation
{
    public enum ReferenceState
    {
        /// <summary>Points at an object that exists.</summary>
        Assigned,

        /// <summary>Was never set, or was cleared.</summary>
        None,

        /// <summary>Points at something that is no longer there.</summary>
        Missing,
    }

    /// <summary>One serialized object reference on a component or asset.</summary>
    public sealed class ReferenceRecord
    {
        public ReferenceRecord(string propertyPath, ReferenceState state)
        {
            PropertyPath = propertyPath;
            State = state;
        }

        /// <summary>The serialized path, such as <c>prefab</c> or <c>waves.Array.data[1].enemy</c>.</summary>
        public string PropertyPath { get; }

        public ReferenceState State { get; }
    }

    /// <summary>A component on an object, or the asset itself when the object is an asset.</summary>
    public sealed class ComponentRecord
    {
        private static readonly ReferenceRecord[] NoReferences = new ReferenceRecord[0];

        public ComponentRecord(string typeName, bool isProjectScript, IReadOnlyList<ReferenceRecord> references)
        {
            TypeName = typeName;
            IsProjectScript = isProjectScript;
            References = references ?? NoReferences;
        }

        private ComponentRecord()
        {
            TypeName = string.Empty;
            IsMissingScript = true;
            References = NoReferences;
        }

        public string TypeName { get; }

        /// <summary>The script this component was made from cannot be found.</summary>
        public bool IsMissingScript { get; }

        /// <summary>The script belongs to the project, not to Unity or an installed package.</summary>
        public bool IsProjectScript { get; }

        public IReadOnlyList<ReferenceRecord> References { get; }

        public static ComponentRecord MissingScript() => new ComponentRecord();
    }

    public enum ObjectKind
    {
        SceneObject,
        PrefabObject,
        Asset,
    }

    /// <summary>Everything the rules need to know about one object, read out of the project as plain data.</summary>
    public sealed class ObjectRecord
    {
        public ObjectRecord(string assetPath, string objectPath, ObjectKind kind, IReadOnlyList<ComponentRecord> components,
            int childCount = 0, bool isMissingPrefab = false)
        {
            AssetPath = assetPath;
            ObjectPath = objectPath;
            Kind = kind;
            Components = components;
            ChildCount = childCount;
            IsMissingPrefab = isMissingPrefab;
        }

        /// <summary>The scene, prefab or asset file.</summary>
        public string AssetPath { get; }

        /// <summary>Names from the root down, joined with a slash. Empty for a main asset.</summary>
        public string ObjectPath { get; }

        public ObjectKind Kind { get; }

        public IReadOnlyList<ComponentRecord> Components { get; }

        public int ChildCount { get; }

        /// <summary>A prefab instance whose prefab asset is gone.</summary>
        public bool IsMissingPrefab { get; }
    }
}
