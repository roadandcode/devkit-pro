using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;
using UnityEngine;

namespace RoadAndCode.DevKit.Validation.Tests
{
    /// <summary>The part that touches Unity: objects made in memory, read back as records.</summary>
    public sealed class ReaderTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created)
            {
                if (item != null) Object.DestroyImmediate(item);
            }

            _created.Clear();
        }

        private T Track<T>(T item) where T : Object
        {
            _created.Add(item);
            return item;
        }

        private static ReferenceState StateOf(ComponentRecord component, string path)
        {
            foreach (ReferenceRecord reference in component.References)
            {
                if (reference.PropertyPath == path) return reference.State;
            }

            Assert.Fail($"No reference at '{path}'.");
            return ReferenceState.None;
        }

        [Test]
        public void An_asset_is_read_field_by_field()
        {
            var material = Track(new Material(Shader.Find("Sprites/Default")));
            var texture = Track(new Texture2D(2, 2));
            var asset = Track(ScriptableObject.CreateInstance<ProbeAsset>());
            asset.Fill(material, texture);

            ComponentRecord record = new RecordBuilder().ForObject(asset);

            Assert.That(record.TypeName, Is.EqualTo("ProbeAsset"));
            Assert.That(record.IsMissingScript, Is.False);
            Assert.That(StateOf(record, "_assigned"), Is.EqualTo(ReferenceState.Assigned));
            Assert.That(StateOf(record, "_empty"), Is.EqualTo(ReferenceState.None));
            Assert.That(StateOf(record, "_list.Array.data[0]"), Is.EqualTo(ReferenceState.Assigned));
            Assert.That(StateOf(record, "_list.Array.data[1]"), Is.EqualTo(ReferenceState.None));
            Assert.That(StateOf(record, "_slots.Array.data[0]._icon"), Is.EqualTo(ReferenceState.Assigned));
            Assert.That(StateOf(record, "_slots.Array.data[1]._icon"), Is.EqualTo(ReferenceState.None));
            Assert.That(record.References, Has.Count.EqualTo(6), "the script reference, the number array and the empty obsolete field are not fields to report");
        }

        [Test]
        public void A_reference_to_a_destroyed_object_reads_as_missing()
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            var asset = Track(ScriptableObject.CreateInstance<ProbeAsset>());
            asset.Fill(material, null);
            Object.DestroyImmediate(material);

            ComponentRecord record = new RecordBuilder().ForObject(asset);

            Assert.That(StateOf(record, "_assigned"), Is.EqualTo(ReferenceState.Missing));
        }

        [Test]
        public void A_dead_reference_under_an_obsolete_field_is_left_out()
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            var asset = Track(ScriptableObject.CreateInstance<ProbeAsset>());
            using (var serialized = new UnityEditor.SerializedObject(asset))
            {
                serialized.FindProperty("_retired").objectReferenceValue = material;
                serialized.FindProperty("_assigned").objectReferenceValue = material;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Object.DestroyImmediate(material);
            ComponentRecord record = new RecordBuilder().ForObject(asset);

            Assert.That(StateOf(record, "_assigned"), Is.EqualTo(ReferenceState.Missing));
            Assert.That(record.References, Has.None.Matches<ReferenceRecord>(reference => reference.PropertyPath == "_retired"));
        }

        [Test]
        public void A_game_object_is_read_with_its_components_and_child_count()
        {
            var parent = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);

            ObjectRecord record = new RecordBuilder().ForGameObject(parent, "Assets/Scene.unity", "Parent", ObjectKind.SceneObject);

            Assert.That(record.AssetPath, Is.EqualTo("Assets/Scene.unity"));
            Assert.That(record.ObjectPath, Is.EqualTo("Parent"));
            Assert.That(record.ChildCount, Is.EqualTo(1));
            Assert.That(record.IsMissingPrefab, Is.False);
            Assert.That(record.Components.Count, Is.EqualTo(4));
            Assert.That(record.Components[0].TypeName, Is.EqualTo("Transform"));
            Assert.That(record.Components[0].IsProjectScript, Is.False);
            Assert.That(StateOf(record.Components[1], "m_Mesh"), Is.EqualTo(ReferenceState.Assigned));
        }

        [Test]
        public void Open_scenes_are_walked_from_the_roots_down()
        {
            var root = Track(new GameObject("DevKitReaderRoot"));
            var middle = new GameObject("Middle");
            middle.transform.SetParent(root.transform);
            new GameObject("Leaf").transform.SetParent(middle.transform);

            var paths = new List<string>();
            ReadSummary summary = new UnityProjectReader().Read(ValidationScope.OpenScenes, record =>
            {
                if (record.ObjectPath.StartsWith("DevKitReaderRoot")) paths.Add(record.ObjectPath);
            }, new NullProgress());

            Assert.That(paths, Is.EqualTo(new[] { "DevKitReaderRoot", "DevKitReaderRoot/Middle", "DevKitReaderRoot/Middle/Leaf" }));
            Assert.That(summary.Scenes, Is.GreaterThanOrEqualTo(1));
            Assert.That(summary.Objects, Is.GreaterThanOrEqualTo(3));
            Assert.That(summary.Cancelled, Is.False);
        }

        [Test]
        public void The_empty_object_rule_fires_on_a_real_leaf()
        {
            var root = Track(new GameObject("DevKitLeftover"));
            var findings = new List<Finding>();
            var rule = new EmptyObjectRule();

            new UnityProjectReader().Read(ValidationScope.OpenScenes, record => rule.Check(record, findings), new NullProgress());

            Assert.That(findings, Has.Some.Matches<Finding>(finding => finding.ObjectPath == root.name));
        }
    }
}
