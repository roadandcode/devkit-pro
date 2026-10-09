using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.AddressablesAudit;
using RoadAndCode.DevKit.Common;
using RoadAndCode.DevKit.Sandbox.Editor;

namespace RoadAndCode.DevKit.Sandbox.Tests
{
    /// <summary>The audit against the sample Addressables setup, through the real adapter.</summary>
    public sealed class AuditFixtureTests
    {
        // Small enough that the group with the noise texture is over it, built or not.
        private const float LimitMegabytes = 0.15f;

        private AuditResult _result;

        [OneTimeSetUp]
        public void AuditOnce()
        {
            AuditRunner runner = AuditComposition.Runner();
            Assert.That(runner.IsAvailable, Is.True, "the adapter assembly should be compiled when Addressables is installed");

            var options = new AuditOptions { MaxBundleMegabytes = LimitMegabytes };
            Assert.That(runner.TryRun(options, new NullProgress(), out _result, out string problem), Is.True, problem);
        }

        private List<Finding> Of(string rule)
        {
            var found = new List<Finding>();
            foreach (Finding finding in _result.Report.Findings)
            {
                if (finding.Rule == rule) found.Add(finding);
            }

            return found;
        }

        private GroupSummary Group(string name)
        {
            foreach (GroupSummary group in _result.Groups)
            {
                if (group.Name == name) return group;
            }

            Assert.Fail($"No group called '{name}'.");
            return null;
        }

        [Test]
        public void Every_group_is_in_the_overview()
        {
            Assert.That(_result.Groups, Has.Count.EqualTo(5));
            Assert.That(Group(SandboxContent.PropsGroup).Entries, Is.EqualTo(2));
            Assert.That(Group(SandboxContent.OrphansGroup).Entries, Is.EqualTo(2));
        }

        [Test]
        public void The_two_asset_references_that_lead_nowhere_are_errors_at_the_scene_that_holds_them()
        {
            List<Finding> findings = Of(DanglingReferenceRule.RuleId);

            Assert.That(findings, Has.Count.EqualTo(2));
            Assert.That(findings, Has.All.Matches<Finding>(finding => finding.AssetPath == SandboxContent.BrokenScene));
            Assert.That(findings, Has.Some.Matches<Finding>(finding => finding.Message.Contains("Lantern.prefab, which is not addressable")));
            Assert.That(findings, Has.Some.Matches<Finding>(finding => finding.Message.Contains("no longer exists")));
        }

        [Test]
        public void The_asset_reference_in_the_clean_scene_is_fine()
        {
            Assert.That(Of(DanglingReferenceRule.RuleId), Has.None.Matches<Finding>(finding => finding.AssetPath == SandboxContent.CleanScene));
            Assert.That(Of(MissingAssetRule.RuleId), Is.Empty);
        }

        [Test]
        public void Only_the_orphans_group_is_unreferenced()
        {
            List<Finding> findings = Of(UnreferencedRule.GroupRuleId);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].ObjectPath, Is.EqualTo(SandboxContent.OrphansGroup));
            Assert.That(Group(SandboxContent.OrphansGroup).UsedEntries, Is.EqualTo(0));
        }

        [Test]
        public void An_asset_reference_a_label_and_an_address_each_count_as_a_reference()
        {
            // The crate is held by an AssetReference in the Clean scene and the barrel is loaded by
            // label; the pillar and the arena scene are loaded by address.
            Assert.That(Group(SandboxContent.PropsGroup).UsedEntries, Is.EqualTo(2));
            Assert.That(Group(SandboxContent.ScenesGroup).UsedEntries, Is.EqualTo(1));
            Assert.That(Group(SandboxContent.SceneryGroup).UsedEntries, Is.EqualTo(1));
        }

        [Test]
        public void The_bench_nothing_loads_is_noted_inside_its_used_group()
        {
            List<Finding> findings = Of(UnreferencedRule.EntryRuleId);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Message, Does.Contain("scenery/bench"));
        }

        [Test]
        public void The_default_group_is_empty()
        {
            List<Finding> findings = Of(EmptyGroupRule.RuleId);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].ObjectPath, Is.EqualTo("Default Local Group"));
        }

        [Test]
        public void The_wood_shared_by_two_groups_is_duplicated()
        {
            List<Finding> findings = Of(DuplicatedDependencyRule.RuleId);

            // The paint is a second, unplanned case: the arena scene and the orphans both use it.
            Assert.That(findings.ConvertAll(finding => finding.AssetPath), Is.EquivalentTo(new[]
            {
                SandboxContent.Root + "/Materials/Paint.mat",
                SandboxContent.Root + "/Materials/Wood.mat",
                SandboxContent.Root + "/Textures/Wood.png",
            }));
            Finding wood = findings.Find(finding => finding.AssetPath.EndsWith("Wood.mat"));
            Assert.That(wood.Detail, Does.Contain(SandboxContent.PropsGroup).And.Contain(SandboxContent.SceneryGroup));
        }

        [Test]
        public void The_reused_address_is_reported()
        {
            List<Finding> findings = Of(DuplicateAddressRule.RuleId);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Message, Does.Contain("'props/barrel'"));
        }

        [Test]
        public void The_addressable_scene_is_not_in_build_settings_so_it_is_not_built_twice()
        {
            // Addressables keeps the two lists apart while the editor is open, so the sample
            // project cannot hold this state. The rule itself is covered by the package's tests.
            Assert.That(Of(SceneInBuildRule.RuleId), Is.Empty);
        }

        [Test]
        public void The_scenery_is_over_the_size_limit_measured_or_estimated()
        {
            // With bundles from a build the real file is measured; without, the source files stand in.
            List<Finding> measured = Of(OversizedBundleRule.BundleRuleId);
            List<Finding> estimated = Of(OversizedBundleRule.EstimateRuleId);

            Assert.That(measured.Count + estimated.Count, Is.GreaterThanOrEqualTo(1));
            if (measured.Count == 0) Assert.That(estimated[0].ObjectPath, Is.EqualTo(SandboxContent.SceneryGroup));
            else Assert.That(measured, Has.Some.Matches<Finding>(finding => finding.AssetPath.ToLowerInvariant().Contains("scenery")));

            // The noise texture is not an entry, but it is what the group weighs.
            Assert.That(Group(SandboxContent.SceneryGroup).SourceBytes, Is.GreaterThan(150 * 1024));
        }
    }
}
