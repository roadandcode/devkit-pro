using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.AddressablesAudit.Tests
{
    /// <summary>Builds snapshots by hand.</summary>
    internal sealed class SnapshotBuilder
    {
        private readonly List<GroupRecord> _groups = new List<GroupRecord>();
        private readonly List<BundleRecord> _bundles = new List<BundleRecord>();
        private readonly List<string> _buildScenes = new List<string>();
        private readonly Dictionary<string, long> _dependencySizes = new Dictionary<string, long>();

        public ReferenceIndex References { get; } = new ReferenceIndex();

        public SnapshotBuilder Group(string name, params EntryRecord[] entries)
        {
            _groups.Add(new GroupRecord(name, entries));
            return this;
        }

        public SnapshotBuilder Bundle(string path, long megabytes)
        {
            _bundles.Add(new BundleRecord(path, megabytes * 1024 * 1024));
            return this;
        }

        public SnapshotBuilder BuildScene(string path)
        {
            _buildScenes.Add(path);
            return this;
        }

        public SnapshotBuilder Code(string source)
        {
            References.AddCode(source);
            return this;
        }

        public SnapshotBuilder Serialized(string yaml, params string[] knownNames)
        {
            References.AddSerialized(yaml, new HashSet<string>(knownNames));
            return this;
        }

        public SnapshotBuilder DependencySize(string path, long megabytes)
        {
            _dependencySizes[path] = megabytes * 1024 * 1024;
            return this;
        }

        public AddressablesSnapshot Build() => new AddressablesSnapshot(_groups, _bundles, References, _buildScenes, _dependencySizes);

        public static EntryRecord Entry(string address, string path = null, string[] labels = null, long megabytes = 0, params string[] dependencies)
        {
            return new EntryRecord(Guid(address), address, path ?? $"Assets/Content/{address}.prefab", labels, megabytes * 1024 * 1024, dependencies);
        }

        // 32 hex characters derived from the address, so a test can write the GUID it expects.
        public static string Guid(string address) => ((uint)address.GetHashCode()).ToString("x8") + "000000000000000000000000";
    }

    public sealed class RuleTests
    {
        private static List<Finding> Run(IAuditRule rule, AddressablesSnapshot snapshot, float maxMegabytes = 10f)
        {
            var findings = new List<Finding>();
            rule.Check(snapshot, new Usage(snapshot), new AuditOptions { MaxBundleMegabytes = maxMegabytes }, findings);
            return findings;
        }

        [Test]
        public void A_group_without_entries_is_reported()
        {
            AddressablesSnapshot snapshot = new SnapshotBuilder().Group("Empty").Group("Full", SnapshotBuilder.Entry("crate")).Build();

            List<Finding> findings = Run(new EmptyGroupRule(), snapshot);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].ObjectPath, Is.EqualTo("Empty"));
            Assert.That(findings[0].Severity, Is.EqualTo(Severity.Warning));
        }

        [Test]
        public void An_entry_whose_asset_is_gone_is_an_error()
        {
            var gone = new EntryRecord("0123", "old/crate", string.Empty, assetExists: false);
            AddressablesSnapshot snapshot = new SnapshotBuilder().Group("Props", gone, SnapshotBuilder.Entry("crate")).Build();

            List<Finding> findings = Run(new MissingAssetRule(), snapshot);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Severity, Is.EqualTo(Severity.Error));
            Assert.That(findings[0].Message, Does.Contain("old/crate"));
            Assert.That(findings[0].Detail, Is.EqualTo("0123"));
        }

        [Test]
        public void An_address_used_twice_is_reported_once_with_both_owners()
        {
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("A", SnapshotBuilder.Entry("crate", "Assets/A/Crate.prefab"), SnapshotBuilder.Entry("barrel"))
                .Group("B", SnapshotBuilder.Entry("crate", "Assets/B/Crate.prefab"))
                .Build();

            List<Finding> findings = Run(new DuplicateAddressRule(), snapshot);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Message, Is.EqualTo("Address 'crate' is used by 2 entries"));
            Assert.That(findings[0].Detail, Is.EqualTo("Assets/A/Crate.prefab (A), Assets/B/Crate.prefab (B)"));
        }

        [Test]
        public void A_group_nothing_points_at_is_unreferenced()
        {
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("Used", SnapshotBuilder.Entry("crate"))
                .Group("Orphans", SnapshotBuilder.Entry("old-crate"), SnapshotBuilder.Entry("old-barrel"))
                .Code("Addressables.LoadAssetAsync<GameObject>(\"crate\");")
                .Build();

            List<Finding> findings = Run(new UnreferencedRule(), snapshot);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Rule, Is.EqualTo("unreferenced-group"));
            Assert.That(findings[0].ObjectPath, Is.EqualTo("Orphans"));
            Assert.That(findings[0].Message, Does.Contain("2 entries"));
        }

        [Test]
        public void A_partly_used_group_gets_a_note_per_unused_entry()
        {
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("Props", SnapshotBuilder.Entry("crate"), SnapshotBuilder.Entry("barrel"))
                .Code("var key = \"crate\";")
                .Build();

            List<Finding> findings = Run(new UnreferencedRule(), snapshot);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Rule, Is.EqualTo("unreferenced-entry"));
            Assert.That(findings[0].Severity, Is.EqualTo(Severity.Info));
            Assert.That(findings[0].Message, Does.Contain("barrel"));
        }

        [Test]
        public void An_empty_group_is_left_to_the_empty_group_rule()
        {
            Assert.That(Run(new UnreferencedRule(), new SnapshotBuilder().Group("Empty").Build()), Is.Empty);
        }

        [Test]
        public void A_dependency_shared_by_two_groups_is_duplicated()
        {
            const string shared = "Assets/Textures/Shared.png";
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("A", SnapshotBuilder.Entry("crate", null, null, 0, shared, "Assets/Textures/OnlyA.png"))
                .Group("B", SnapshotBuilder.Entry("barrel", null, null, 0, shared), SnapshotBuilder.Entry("box", null, null, 0, shared))
                .Build();

            List<Finding> findings = Run(new DuplicatedDependencyRule(), snapshot);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].AssetPath, Is.EqualTo(shared));
            Assert.That(findings[0].Detail, Is.EqualTo("A, B"));
        }

        [Test]
        public void A_shared_dependency_that_is_addressable_itself_is_not_duplicated()
        {
            const string shared = "Assets/Textures/Shared.png";
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("A", SnapshotBuilder.Entry("crate", null, null, 0, shared))
                .Group("B", SnapshotBuilder.Entry("barrel", null, null, 0, shared))
                .Group("Shared", SnapshotBuilder.Entry("shared-texture", shared))
                .Build();

            Assert.That(Run(new DuplicatedDependencyRule(), snapshot), Is.Empty);
        }

        [Test]
        public void A_dependency_inside_an_addressable_folder_is_not_duplicated()
        {
            var folder = new EntryRecord("f0", "textures", "Assets/Textures", isFolder: true);
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("A", SnapshotBuilder.Entry("crate", null, null, 0, "Assets/Textures/Shared.png"))
                .Group("B", SnapshotBuilder.Entry("barrel", null, null, 0, "Assets/Textures/Shared.png"))
                .Group("Folders", folder)
                .Build();

            Assert.That(Run(new DuplicatedDependencyRule(), snapshot), Is.Empty);
        }

        [Test]
        public void A_built_bundle_over_the_limit_is_reported()
        {
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("Heavy", SnapshotBuilder.Entry("film", megabytes: 50))
                .Bundle("Library/com.unity.addressables/aa/WebGL/heavy.bundle", 24)
                .Bundle("Library/com.unity.addressables/aa/WebGL/light.bundle", 2)
                .Build();

            List<Finding> findings = Run(new OversizedBundleRule(), snapshot, maxMegabytes: 10f);

            Assert.That(findings, Has.Count.EqualTo(1), "with real bundles to measure, the source-size estimate is not used");
            Assert.That(findings[0].Rule, Is.EqualTo("oversized-bundle"));
            Assert.That(findings[0].AssetPath, Does.EndWith("heavy.bundle"));
            Assert.That(findings[0].Message, Is.EqualTo("Bundle is 24.00 MB, over the 10.00 MB limit"));
        }

        [Test]
        public void Without_built_bundles_the_groups_source_size_stands_in_as_a_note()
        {
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("Heavy", SnapshotBuilder.Entry("film", megabytes: 8), SnapshotBuilder.Entry("music", megabytes: 7))
                .Group("Light", SnapshotBuilder.Entry("icon", megabytes: 1))
                .Build();

            List<Finding> findings = Run(new OversizedBundleRule(), snapshot, maxMegabytes: 10f);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].Rule, Is.EqualTo("large-group"));
            Assert.That(findings[0].Severity, Is.EqualTo(Severity.Info));
            Assert.That(findings[0].Message, Does.Contain("15.00 MB").And.Contain("estimate"));
        }

        [Test]
        public void The_estimate_counts_what_the_entries_pull_in_but_not_what_is_addressable_itself()
        {
            const string texture = "Assets/Textures/Rock.png";
            const string shared = "Assets/Textures/Shared.png";
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("Scenery", SnapshotBuilder.Entry("pillar", null, null, 1, texture, shared), SnapshotBuilder.Entry("wall", null, null, 1, texture, shared))
                .Group("Shared", SnapshotBuilder.Entry("shared", shared, null, 30))
                .DependencySize(texture, 12)
                .DependencySize(shared, 30)
                .Build();

            var usage = new Usage(snapshot);

            Assert.That(usage.SourceBytes(snapshot.Groups[0]), Is.EqualTo(14L * 1024 * 1024), "two prefabs and one copy of the texture they share");

            List<Finding> findings = Run(new OversizedBundleRule(), snapshot, maxMegabytes: 13f);
            Assert.That(findings.ConvertAll(finding => finding.ObjectPath), Is.EqualTo(new[] { "Scenery", "Shared" }));
        }

        [Test]
        public void An_addressable_scene_that_is_also_in_build_settings_is_reported()
        {
            var arena = new EntryRecord("a0", "arena", "Assets/Scenes/Arena.unity", isScene: true);
            var menu = new EntryRecord("b0", "menu", "Assets/Scenes/Menu.unity", isScene: true);
            AddressablesSnapshot snapshot = new SnapshotBuilder().Group("Scenes", arena, menu).BuildScene("Assets/Scenes/Arena.unity").Build();

            List<Finding> findings = Run(new SceneInBuildRule(), snapshot);

            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].AssetPath, Is.EqualTo("Assets/Scenes/Arena.unity"));
        }
    }

    public sealed class UsageTests
    {
        [Test]
        public void An_asset_reference_field_marks_its_entry_as_used()
        {
            EntryRecord arena = SnapshotBuilder.Entry("arena");
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("Scenes", arena)
                .Serialized($"  _arena:\n    m_AssetGUID: {arena.Guid}\n    m_SubObjectName: \n")
                .Build();

            Assert.That(new Usage(snapshot).IsUsed(arena), Is.True);
        }

        [Test]
        public void A_label_in_code_or_in_a_label_reference_marks_every_entry_that_carries_it()
        {
            EntryRecord grunt = SnapshotBuilder.Entry("grunt", null, new[] { "enemies" });
            EntryRecord archer = SnapshotBuilder.Entry("archer", null, new[] { "enemies", "ranged" });
            EntryRecord crate = SnapshotBuilder.Entry("crate", null, new[] { "props" });
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("Things", grunt, archer, crate)
                .Code("Addressables.LoadAssetsAsync<GameObject>(\"enemies\", null);")
                .Serialized("  _label:\n    m_LabelString: props\n")
                .Build();

            var usage = new Usage(snapshot);

            Assert.That(usage.IsUsed(grunt), Is.True);
            Assert.That(usage.IsUsed(archer), Is.True);
            Assert.That(usage.IsUsed(crate), Is.True);
        }

        [Test]
        public void An_address_kept_in_a_string_field_of_an_asset_counts()
        {
            EntryRecord theme = SnapshotBuilder.Entry("music/theme");
            EntryRecord other = SnapshotBuilder.Entry("music/other");
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("Music", theme, other)
                .Serialized("  _trackAddress: music/theme\n  _names:\n  - \"unrelated\"\n", "music/theme", "music/other")
                .Build();

            var usage = new Usage(snapshot);

            Assert.That(usage.IsUsed(theme), Is.True);
            Assert.That(usage.IsUsed(other), Is.False);
        }

        [Test]
        public void What_a_used_entry_depends_on_is_used_too_and_so_on_down_the_chain()
        {
            EntryRecord level = SnapshotBuilder.Entry("level", "Assets/Level.prefab", null, 0, "Assets/Crate.prefab");
            EntryRecord crate = SnapshotBuilder.Entry("crate", "Assets/Crate.prefab", null, 0, "Assets/Wood.mat");
            EntryRecord wood = SnapshotBuilder.Entry("wood", "Assets/Wood.mat");
            EntryRecord stray = SnapshotBuilder.Entry("stray", "Assets/Stray.prefab", null, 0, "Assets/Wood.mat");
            AddressablesSnapshot snapshot = new SnapshotBuilder().Group("All", level, crate, wood, stray).Code("Load(\"level\");").Build();

            var usage = new Usage(snapshot);

            Assert.That(usage.IsUsed(crate), Is.True);
            Assert.That(usage.IsUsed(wood), Is.True);
            Assert.That(usage.IsUsed(stray), Is.False, "depending on something used does not make an entry used");
        }

        [Test]
        public void A_folder_entry_is_used_when_something_inside_it_is_addressed()
        {
            var folder = new EntryRecord("f0", "icons", "Assets/Icons", isFolder: true);
            AddressablesSnapshot snapshot = new SnapshotBuilder().Group("Folders", folder).Code("Load(\"icons/sword.png\");").Build();

            var usage = new Usage(snapshot);

            Assert.That(usage.IsUsed(folder), Is.True);
            Assert.That(usage.EntryFor("Assets/Icons/sword.png"), Is.SameAs(folder));
            Assert.That(usage.EntryFor("Assets/IconsOld/sword.png"), Is.Null);
        }

        [Test]
        public void String_literals_are_read_with_their_escapes_and_empty_ones_are_ignored()
        {
            var index = new ReferenceIndex();

            index.AddCode("var a = \"plain\"; var b = \"with \\\"quotes\\\"\"; var c = \"\"; // \"in a comment\"");

            Assert.That(index.HasName("plain"), Is.True);
            Assert.That(index.HasName("with \\\"quotes\\\""), Is.True);
            Assert.That(index.HasName("in a comment"), Is.True, "a name in a comment errs towards 'referenced'");
            Assert.That(index.HasName(string.Empty), Is.False);
        }
    }

    public sealed class AuditorTests
    {
        private static AddressablesSnapshot Messy()
        {
            return new SnapshotBuilder()
                .Group("Used", SnapshotBuilder.Entry("crate", megabytes: 1))
                .Group("Orphans", SnapshotBuilder.Entry("old-crate", megabytes: 2), SnapshotBuilder.Entry("old-barrel", megabytes: 3))
                .Group("Empty")
                .Group("Broken", new EntryRecord("0123", "gone", string.Empty, assetExists: false))
                .Code("Load(\"crate\"); Load(\"gone\");")
                .Build();
        }

        [Test]
        public void Findings_come_out_worst_first()
        {
            AuditResult result = new Auditor(Auditor.AllRules()).Audit(Messy(), new AuditOptions(), TimeSpan.Zero);

            Assert.That(result.Report.Tool, Is.EqualTo("Addressables Audit"));
            Assert.That(result.Report.Findings[0].Rule, Is.EqualTo("missing-asset"));
            Assert.That(result.Report.Count(Severity.Error), Is.EqualTo(1));
            Assert.That(result.Report.Count(Severity.Warning), Is.EqualTo(2));
            for (int i = 1; i < result.Report.Findings.Count; i++)
            {
                Assert.That(result.Report.Findings[i].Severity, Is.LessThanOrEqualTo(result.Report.Findings[i - 1].Severity));
            }
        }

        [Test]
        public void The_overview_has_a_line_per_group()
        {
            AuditResult result = new Auditor(Auditor.AllRules()).Audit(Messy(), new AuditOptions(), TimeSpan.Zero);

            Assert.That(result.Groups, Has.Count.EqualTo(4));
            Assert.That(result.Groups[0].UsedEntries, Is.EqualTo(1));
            Assert.That(result.Groups[1].Name, Is.EqualTo("Orphans"));
            Assert.That(result.Groups[1].Entries, Is.EqualTo(2));
            Assert.That(result.Groups[1].UsedEntries, Is.EqualTo(0));
            Assert.That(result.Groups[1].SourceBytes, Is.EqualTo(5L * 1024 * 1024));
        }

        [Test]
        public void The_report_states_what_was_looked_at()
        {
            AuditResult result = new Auditor(Auditor.AllRules()).Audit(Messy(), new AuditOptions { MaxBundleMegabytes = 4f }, TimeSpan.Zero);

            Assert.That(result.Report.Facts[0].Value, Is.EqualTo("4"));
            Assert.That(result.Report.Facts[1].Value, Is.EqualTo("4"));
            Assert.That(result.Report.Facts[2].Value, Is.EqualTo("none found"));
            Assert.That(result.Report.Facts[3].Value, Is.EqualTo("4.00 MB"));
        }

        [Test]
        public void A_tidy_setup_has_no_findings()
        {
            AddressablesSnapshot snapshot = new SnapshotBuilder()
                .Group("Props", SnapshotBuilder.Entry("crate"), SnapshotBuilder.Entry("barrel"))
                .Code("Load(\"crate\"); Load(\"barrel\");")
                .Build();

            Assert.That(new Auditor(Auditor.AllRules()).Audit(snapshot, new AuditOptions(), TimeSpan.Zero).Report.Findings, Is.Empty);
        }
    }
}
