using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.DataBrowser.Tests
{
    internal static class Sample
    {
        public static AssetRecord Item(string name, string folder = "Assets/Data/Items")
        {
            return new AssetRecord(name + "-guid", $"{folder}/{name}.asset", name, "ItemDefinition", "Game.ItemDefinition", true);
        }

        public static AssetRecord Enemy(string name)
        {
            return new AssetRecord(name + "-guid", $"Assets/Data/Enemies/{name}.asset", name, "EnemyDefinition", "Game.EnemyDefinition", true);
        }

        public static AssetRecord PackageSettings(string name)
        {
            return new AssetRecord(name + "-guid", $"Assets/Settings/{name}.asset", name, "PipelineAsset", "Vendor.PipelineAsset", false);
        }

        public static Catalog Catalog()
        {
            return new Catalog(new[]
            {
                Item("Sword"), Item("Shield"), Item("apple", "Assets/Data/Food"),
                Enemy("Grunt"), Enemy("Archer"),
                PackageSettings("Renderer"),
                AssetRecord.MissingScript("orphan-guid", "Assets/Data/Orphan.asset", "Orphan"),
            });
        }
    }

    public sealed class CatalogTests
    {
        [Test]
        public void Types_are_listed_by_name_with_their_counts()
        {
            List<TypeEntry> types = Sample.Catalog().Types(projectTypesOnly: false);

            Assert.That(types.ConvertAll(type => type.TypeName), Is.EqualTo(new[] { "(missing script)", "EnemyDefinition", "ItemDefinition", "PipelineAsset" }));
            Assert.That(types[1].Count, Is.EqualTo(2));
            Assert.That(types[2].Count, Is.EqualTo(3));
        }

        [Test]
        public void Package_types_are_hidden_when_only_the_projects_own_are_wanted()
        {
            Catalog catalog = Sample.Catalog();

            Assert.That(catalog.Types(projectTypesOnly: true).ConvertAll(type => type.TypeName), Has.No.Member("PipelineAsset"));
            Assert.That(catalog.Count(projectTypesOnly: true), Is.EqualTo(6));
            Assert.That(catalog.Count(projectTypesOnly: false), Is.EqualTo(7));
        }

        [Test]
        public void Two_types_with_the_same_short_name_stay_apart()
        {
            var catalog = new Catalog(new[]
            {
                new AssetRecord("a", "Assets/A.asset", "A", "Settings", "Game.Audio.Settings", true),
                new AssetRecord("b", "Assets/B.asset", "B", "Settings", "Game.Video.Settings", true),
            });

            Assert.That(catalog.Types(true), Has.Count.EqualTo(2));
        }

        [Test]
        public void An_empty_query_returns_everything_sorted_by_name_without_regard_to_case()
        {
            List<AssetRecord> found = Sample.Catalog().Find(new CatalogQuery { ProjectTypesOnly = false });

            Assert.That(found.ConvertAll(record => record.Name), Is.EqualTo(new[] { "apple", "Archer", "Grunt", "Orphan", "Renderer", "Shield", "Sword" }));
        }

        [Test]
        public void A_chosen_type_narrows_the_list()
        {
            List<AssetRecord> found = Sample.Catalog().Find(new CatalogQuery { TypeFullName = "Game.EnemyDefinition" });

            Assert.That(found.ConvertAll(record => record.Name), Is.EqualTo(new[] { "Archer", "Grunt" }));
        }

        [Test]
        public void Search_words_match_the_name_or_the_path()
        {
            Catalog catalog = Sample.Catalog();

            Assert.That(catalog.Find(new CatalogQuery { Text = "sw" }).ConvertAll(record => record.Name), Is.EqualTo(new[] { "Sword" }));
            Assert.That(catalog.Find(new CatalogQuery { Text = "food" }).ConvertAll(record => record.Name), Is.EqualTo(new[] { "apple" }));
            Assert.That(catalog.Find(new CatalogQuery { Text = "items sh" }).ConvertAll(record => record.Name), Is.EqualTo(new[] { "Shield" }),
                "every word has to match somewhere in the name or path");
            Assert.That(catalog.Find(new CatalogQuery { Text = "enemies sh" }), Is.Empty);
        }

        [Test]
        public void A_word_with_the_type_prefix_matches_the_type_instead()
        {
            Catalog catalog = Sample.Catalog();

            Assert.That(catalog.Find(new CatalogQuery { Text = "t:enemy" }), Has.Count.EqualTo(2));
            Assert.That(catalog.Find(new CatalogQuery { Text = "t:item sh" }).ConvertAll(record => record.Name), Is.EqualTo(new[] { "Shield" }));
            Assert.That(catalog.Find(new CatalogQuery { Text = "T:Nothing" }), Is.Empty);
        }

        [Test]
        public void An_asset_with_a_missing_script_is_always_listed()
        {
            List<AssetRecord> found = Sample.Catalog().Find(new CatalogQuery { Text = "orphan", ProjectTypesOnly = true });

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].IsMissingScript, Is.True);
        }

        [Test]
        public void A_record_knows_its_folder()
        {
            Assert.That(Sample.Item("Sword").Folder, Is.EqualTo("Assets/Data/Items"));
        }

        [Test]
        public void The_report_counts_by_type_and_flags_missing_scripts()
        {
            ScanReport report = CatalogReport.From(Sample.Catalog(), projectTypesOnly: true, TimeSpan.FromMilliseconds(5));

            Assert.That(report.Tool, Is.EqualTo("ScriptableObject Browser"));
            Assert.That(report.Facts[0].Name, Is.EqualTo("Assets"));
            Assert.That(report.Facts[0].Value, Is.EqualTo("6"));
            Assert.That(report.Facts[1].Value, Is.EqualTo("3"));
            Assert.That(report.Facts[3].Name, Is.EqualTo("Game.EnemyDefinition"));
            Assert.That(report.Facts[3].Value, Is.EqualTo("2"));
            Assert.That(report.Findings, Has.Count.EqualTo(1));
            Assert.That(report.Findings[0].AssetPath, Is.EqualTo("Assets/Data/Orphan.asset"));
            Assert.That(report.Findings[0].Severity, Is.EqualTo(Severity.Error));
        }
    }
}
