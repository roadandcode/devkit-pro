using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Validation.Tests
{
    public sealed class RuleTests
    {
        private static readonly NamePatterns NoOptionalFields = new NamePatterns(string.Empty);

        [Test]
        public void A_missing_script_is_an_error_that_names_the_component_slot()
        {
            ObjectRecord record = Records.SceneObject("Enemy", Records.Transform(), ComponentRecord.MissingScript());

            List<Finding> findings = Records.Check(new MissingScriptRule(), record);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
            Assert.That(findings[0].Rule, Is.EqualTo("missing-script"));
            Assert.That(findings[0].Detail, Is.EqualTo("component 2"));
            Assert.That(findings[0].ObjectPath, Is.EqualTo("Enemy"));
        }

        [Test]
        public void An_asset_with_a_missing_script_is_worded_as_an_asset()
        {
            var record = new ObjectRecord("Assets/Data/Orphan.asset", string.Empty, ObjectKind.Asset, new[] { ComponentRecord.MissingScript() });

            Assert.That(Records.Check(new MissingScriptRule(), record)[0].Message, Does.Contain("asset"));
        }

        [Test]
        public void Intact_components_are_not_reported_as_missing()
        {
            ObjectRecord record = Records.SceneObject("Enemy", Records.Transform(), Records.Script("Spawner"));

            Assert.That(Records.Check(new MissingScriptRule(), record), Is.Empty);
        }

        [Test]
        public void A_missing_reference_is_an_error_on_any_component()
        {
            ObjectRecord record = Records.SceneObject("Crate",
                Records.Transform(),
                Records.BuiltIn("MeshRenderer", Records.Reference("m_Materials.Array.data[0]", ReferenceState.Missing)),
                Records.Script("Spawner", Records.Reference("_prefab", ReferenceState.Assigned)));

            List<Finding> findings = Records.Check(new MissingReferenceRule(), record);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
            Assert.That(findings[0].Message, Does.StartWith("MeshRenderer.m_Materials[0] "));
            Assert.That(findings[0].Detail, Is.EqualTo("MeshRenderer.m_Materials.Array.data[0]"));
        }

        [Test]
        public void An_empty_field_is_not_a_missing_reference()
        {
            ObjectRecord record = Records.SceneObject("Crate", Records.Script("Spawner", Records.Reference("_prefab", ReferenceState.None)));

            Assert.That(Records.Check(new MissingReferenceRule(), record), Is.Empty);
        }

        [Test]
        public void An_empty_field_on_a_project_script_is_a_warning()
        {
            ObjectRecord record = Records.SceneObject("Gate", Records.Script("Spawner",
                Records.Reference("_prefab", ReferenceState.None),
                Records.Reference("_spawnPoint", ReferenceState.Assigned)));

            List<Finding> findings = Records.Check(new UnassignedReferenceRule(NoOptionalFields), record);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Severity, Is.EqualTo(Severity.Warning));
            Assert.That(findings[0].Message, Is.EqualTo("Spawner._prefab is not assigned"));
        }

        [Test]
        public void Empty_fields_on_components_that_are_not_the_projects_own_are_left_alone()
        {
            ObjectRecord record = Records.SceneObject("Crate", Records.BuiltIn("BoxCollider", Records.Reference("m_Material", ReferenceState.None)));

            Assert.That(Records.Check(new UnassignedReferenceRule(NoOptionalFields), record), Is.Empty);
        }

        [Test]
        public void A_field_named_as_optional_may_be_empty()
        {
            ObjectRecord record = Records.SceneObject("Gate", Records.Script("Spawner",
                Records.Reference("_optionalTint", ReferenceState.None),
                Records.Reference("_prefab", ReferenceState.None)));

            List<Finding> findings = Records.Check(new UnassignedReferenceRule(new NamePatterns("*optional*")), record);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Detail, Is.EqualTo("Spawner._prefab"));
        }

        [Test]
        public void The_optional_list_is_matched_against_the_field_the_script_declares()
        {
            // The element of an optional list is optional too, however deep the empty slot is.
            ObjectRecord record = Records.SceneObject("Gate", Records.Script("Spawner",
                Records.Reference("_optionalDrops.Array.data[2].icon", ReferenceState.None)));

            Assert.That(Records.Check(new UnassignedReferenceRule(new NamePatterns("_optional*")), record), Is.Empty);
        }

        [Test]
        public void An_object_with_only_a_transform_and_no_children_is_noted()
        {
            List<Finding> findings = Records.Check(new EmptyObjectRule(), Records.SceneObject("Leftover", Records.Transform()));

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Severity, Is.EqualTo(Severity.Info));
        }

        [Test]
        public void A_parent_a_component_holder_and_an_asset_are_not_empty_objects()
        {
            var rule = new EmptyObjectRule();
            var parent = new ObjectRecord("Assets/Scene.unity", "Group", ObjectKind.SceneObject, new[] { Records.Transform() }, childCount: 2);
            var asset = new ObjectRecord("Assets/Item.asset", string.Empty, ObjectKind.Asset, new[] { Records.Script("ItemDefinition") });

            Assert.That(Records.Check(rule, parent), Is.Empty);
            Assert.That(Records.Check(rule, Records.SceneObject("Crate", Records.Transform(), Records.BuiltIn("MeshFilter"))), Is.Empty);
            Assert.That(Records.Check(rule, asset), Is.Empty);
        }

        [Test]
        public void A_missing_prefab_is_an_error_and_is_not_also_called_empty()
        {
            var record = new ObjectRecord("Assets/Scene.unity", "Missing Prefab", ObjectKind.SceneObject, new[] { Records.Transform() },
                childCount: 0, isMissingPrefab: true);

            List<Finding> findings = Records.Check(new MissingPrefabRule(), record);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
            Assert.That(Records.Check(new EmptyObjectRule(), record), Is.Empty);
        }

        [TestCase("_prefab", "_prefab")]
        [TestCase("waves.Array.data[1].enemy", "waves[1].enemy")]
        [TestCase("m_Materials.Array.data[0]", "m_Materials[0]")]
        public void Property_paths_read_the_way_the_inspector_shows_them(string path, string readable)
        {
            Assert.That(FieldNames.Readable(path), Is.EqualTo(readable));
        }

        [TestCase("*optional*", "_optionalTint", true)]
        [TestCase("*optional*", "tintOptional", true)]
        [TestCase("*OPTIONAL*", "_optionalTint", true)]
        [TestCase("_fx*", "_fxPrefab", true)]
        [TestCase("_fx*", "_sfxPrefab", false)]
        [TestCase("icon", "icon", true)]
        [TestCase("icon", "icons", false)]
        [TestCase("a*c*e", "abcde", true)]
        [TestCase("a*c*e", "abcd", false)]
        [TestCase("first, second ;third", "third", true)]
        [TestCase("", "anything", false)]
        public void Name_patterns_match_with_stars(string patterns, string name, bool expected)
        {
            Assert.That(new NamePatterns(patterns).Matches(name), Is.EqualTo(expected));
        }

        [Test]
        public void Every_rule_is_on_by_default_and_each_can_be_switched_off()
        {
            var store = new MemoryPreferenceStore();
            Assert.That(ValidationPreferences.Rules(store), Has.Count.EqualTo(5));

            ValidationPreferences.EmptyObjects.Set(store, false);
            ValidationPreferences.UnassignedReferences.Set(store, false);

            IReadOnlyList<IValidationRule> rules = ValidationPreferences.Rules(store);
            Assert.That(rules, Has.Count.EqualTo(3));
            Assert.That(rules, Has.None.InstanceOf<EmptyObjectRule>());
        }
    }
}
