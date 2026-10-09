using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;
using RoadAndCode.DevKit.Sandbox.Editor;
using RoadAndCode.DevKit.Validation;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoadAndCode.DevKit.Sandbox.Tests
{
    /// <summary>The validator against the sample content, through the real reader.</summary>
    public sealed class ValidatorFixtureTests
    {
        private const string Data = SandboxContent.Root + "/Data";

        private ScanReport _report;
        private int _scenesBefore;
        private int _scenesAfter;

        [OneTimeSetUp]
        public void ScanOnce()
        {
            // Unity itself logs the missing script and prefab when it opens the broken scene.
            LogAssert.ignoreFailingMessages = true;

            var rules = ValidationPreferences.Rules(new MemoryPreferenceStore());
            var scanner = new ValidationScanner(new UnityProjectReader(), () => rules);
            _scenesBefore = SceneManager.sceneCount;
            _report = scanner.Scan(ValidationScopes.Everything, new NullProgress());
            _scenesAfter = SceneManager.sceneCount;
        }

        [SetUp]
        public void IgnoreUnityLogs() => LogAssert.ignoreFailingMessages = true;

        private List<Finding> Of(string rule)
        {
            var found = new List<Finding>();
            foreach (Finding finding in _report.Findings)
            {
                if (finding.Rule == rule) found.Add(finding);
            }

            return found;
        }

        private static void AssertOne(List<Finding> findings, string assetPath, string objectPath)
        {
            Assert.That(findings, Has.Some.Matches<Finding>(finding => finding.AssetPath == assetPath && finding.ObjectPath == objectPath),
                $"expected a finding at {assetPath} > {objectPath}");
        }

        [Test]
        public void The_whole_project_was_read()
        {
            Assert.That(Fact("Scenes"), Is.EqualTo("3"));
            Assert.That(Fact("Prefabs"), Is.EqualTo("6"));
            Assert.That(int.Parse(Fact("Objects")), Is.GreaterThan(20));
        }

        [Test]
        public void The_ghosts_script_and_the_orphan_asset_are_missing_scripts()
        {
            List<Finding> findings = Of(MissingScriptRule.RuleId);

            AssertOne(findings, SandboxContent.BrokenScene, "Ghost");
            AssertOne(findings, Data + "/Orphan.asset", string.Empty);
            Assert.That(findings, Has.Count.EqualTo(2));
        }

        [Test]
        public void The_deleted_material_and_the_deleted_prefab_are_missing_references()
        {
            List<Finding> findings = Of(MissingReferenceRule.RuleId);

            AssertOne(findings, SandboxContent.BrokenScene, "Statue");
            AssertOne(findings, Data + "/Enemies/Archer.asset", string.Empty);
            Assert.That(findings, Has.Count.EqualTo(2));
            Assert.That(findings, Has.Some.Matches<Finding>(finding => finding.Detail == "MeshRenderer.m_Materials.Array.data[0]"));
            Assert.That(findings, Has.Some.Matches<Finding>(finding => finding.Detail == "EnemyDefinition._prefab"));
        }

        [Test]
        public void The_instance_of_the_deleted_prefab_is_a_missing_prefab()
        {
            List<Finding> findings = Of(MissingPrefabRule.RuleId);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].AssetPath, Is.EqualTo(SandboxContent.BrokenScene));
        }

        [Test]
        public void Empty_fields_on_project_scripts_are_reported_unless_named_optional()
        {
            List<Finding> findings = Of(UnassignedReferenceRule.RuleId);

            AssertOne(findings, SandboxContent.BrokenScene, "Gate");
            AssertOne(findings, Data + "/Items/Potion.asset", string.Empty);
            Assert.That(findings, Has.Count.EqualTo(2));
            Assert.That(findings, Has.None.Matches<Finding>(finding => finding.Detail.Contains("_optionalTint")));
        }

        [Test]
        public void The_leftover_and_the_spawn_point_marker_are_noted_as_empty()
        {
            List<Finding> findings = Of(EmptyObjectRule.RuleId);

            AssertOne(findings, SandboxContent.BrokenScene, "Leftover");
            AssertOne(findings, SandboxContent.BrokenScene, "Gate/SpawnPoint");
            Assert.That(findings, Has.Count.EqualTo(2));
        }

        [Test]
        public void The_scenes_that_ship_have_nothing_wrong_with_them()
        {
            foreach (Finding finding in _report.Findings)
            {
                Assert.That(finding.AssetPath, Is.Not.EqualTo(SandboxContent.CleanScene), finding.ToString());
                Assert.That(finding.AssetPath, Is.Not.EqualTo(SandboxContent.ArenaScene), finding.ToString());
            }
        }

        [Test]
        public void Scanning_leaves_the_open_scenes_as_they_were()
        {
            Assert.That(_scenesAfter, Is.EqualTo(_scenesBefore));
        }

        [Test]
        public void The_default_command_line_scope_fails_on_the_broken_assets_only()
        {
            var log = new List<string>();

            // Build scenes, prefabs and assets: the broken scene is not in the build, the broken assets are.
            int code = ValidatorCommand.Execute(new CommandLineArguments(new string[0]), new UnityProjectReader(), log.Add);

            Assert.That(code, Is.EqualTo(ExitCode.Findings));
            Assert.That(string.Join("\n", log), Does.Contain("Orphan.asset").And.Not.Contain("Broken.unity"));
        }

        private string Fact(string name)
        {
            foreach (ReportFact fact in _report.Facts)
            {
                if (fact.Name == name) return fact.Value;
            }

            return null;
        }
    }
}
