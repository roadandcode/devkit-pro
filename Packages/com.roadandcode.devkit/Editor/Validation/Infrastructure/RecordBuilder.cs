using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>Reads Unity objects through their serialized form and turns them into records.</summary>
    public sealed class RecordBuilder
    {
        private const string ProjectFolder = "Assets/";

        // References Unity keeps on every object for its own bookkeeping. They are not fields anyone assigns.
        private static readonly HashSet<string> EngineFields = new HashSet<string>
        {
            "m_Script", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Father",
        };

        private readonly Dictionary<Type, bool> _projectTypes = new Dictionary<Type, bool>();

        public ObjectRecord ForGameObject(GameObject gameObject, string assetPath, string objectPath, ObjectKind kind)
        {
            // A missing script comes back as a null entry.
            Component[] components = gameObject.GetComponents<Component>();
            var records = new ComponentRecord[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                records[i] = components[i] == null ? ComponentRecord.MissingScript() : ForObject(components[i]);
            }

            return new ObjectRecord(assetPath, objectPath, kind, records, gameObject.transform.childCount,
                PrefabUtility.IsPrefabAssetMissing(gameObject));
        }

        public ObjectRecord ForAsset(Object asset, string assetPath, string objectPath)
        {
            ComponentRecord record = asset == null ? ComponentRecord.MissingScript() : ForObject(asset);
            return new ObjectRecord(assetPath, objectPath, ObjectKind.Asset, new[] { record });
        }

        public ComponentRecord ForObject(Object target)
        {
            List<ReferenceRecord> references = null;
            using (var serialized = new SerializedObject(target))
            {
                SerializedProperty property = serialized.GetIterator();
                bool enter = true;
                while (property.Next(enter))
                {
                    enter = false;
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (property.depth == 0 && EngineFields.Contains(property.name)) continue;

                        if (references == null) references = new List<ReferenceRecord>();
                        references.Add(new ReferenceRecord(property.propertyPath, StateOf(property)));
                    }
                    else
                    {
                        enter = MayHoldReferences(property);
                    }
                }
            }

            return new ComponentRecord(target.GetType().Name, IsProjectScript(target), references);
        }

        private static ReferenceState StateOf(SerializedProperty property)
        {
            if (property.objectReferenceValue != null) return ReferenceState.Assigned;

            // An empty field has no id at all. A field whose target was deleted still remembers the id.
            return property.objectReferenceEntityIdValue == EntityId.None ? ReferenceState.None : ReferenceState.Missing;
        }

        // Stepping into every array would walk each float of a curve or each vertex of a mesh, so
        // an array is only entered when its elements could be, or could contain, references.
        private static bool MayHoldReferences(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.ManagedReference) return true;
            if (property.propertyType != SerializedPropertyType.Generic) return false;
            if (!property.isArray || property.name == "Array") return true;
            if (property.arraySize == 0) return false;

            SerializedPropertyType element = property.GetArrayElementAtIndex(0).propertyType;
            return element == SerializedPropertyType.ObjectReference || element == SerializedPropertyType.Generic
                                                                     || element == SerializedPropertyType.ManagedReference;
        }

        private bool IsProjectScript(Object target)
        {
            Type type = target.GetType();
            if (_projectTypes.TryGetValue(type, out bool known)) return known;

            MonoScript script = null;
            if (target is MonoBehaviour behaviour) script = MonoScript.FromMonoBehaviour(behaviour);
            else if (target is ScriptableObject asset) script = MonoScript.FromScriptableObject(asset);

            bool project = script != null && IsProjectPath(AssetDatabase.GetAssetPath(script));
            _projectTypes[type] = project;
            return project;
        }

        // Scripts under Assets, and packages kept inside the project or on this disk, are the project's own.
        private static bool IsProjectPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.StartsWith(ProjectFolder, StringComparison.Ordinal)) return true;

            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            return package != null && (package.source == PackageSource.Embedded || package.source == PackageSource.Local);
        }
    }
}
