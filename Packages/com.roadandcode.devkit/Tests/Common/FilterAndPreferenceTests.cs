using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.Common.Tests
{
    public sealed class FindingFilterTests
    {
        private static readonly Finding[] Findings =
        {
            new Finding("missing-script", Severity.Error, "Script is gone", "Assets/Scenes/Arena.unity", "Enemies/Grunt"),
            new Finding("unassigned-reference", Severity.Warning, "Prefab is not set", "Assets/Prefabs/Spawner.prefab", "Spawner", "Spawner.prefab"),
            new Finding("empty-object", Severity.Info, "Nothing on it", "Assets/Scenes/Arena.unity", "Marker"),
        };

        [Test]
        public void Everything_shows_by_default()
        {
            Assert.That(new FindingFilter().Apply(Findings), Has.Count.EqualTo(3));
        }

        [Test]
        public void A_severity_can_be_hidden()
        {
            var filter = new FindingFilter { ShowWarnings = false, ShowInfo = false };

            List<Finding> visible = filter.Apply(Findings);

            Assert.That(visible, Has.Count.EqualTo(1));
            Assert.That(visible[0].Severity, Is.EqualTo(Severity.Error));
        }

        [Test]
        public void Search_looks_at_every_field_and_ignores_case()
        {
            Assert.That(new FindingFilter { Text = "ARENA" }.Apply(Findings), Has.Count.EqualTo(2));
            Assert.That(new FindingFilter { Text = "grunt" }.Apply(Findings), Has.Count.EqualTo(1));
            Assert.That(new FindingFilter { Text = "spawner.prefab" }.Apply(Findings), Has.Count.EqualTo(1));
            Assert.That(new FindingFilter { Text = "empty-object" }.Apply(Findings), Has.Count.EqualTo(1));
        }

        [Test]
        public void Every_word_has_to_match()
        {
            Assert.That(new FindingFilter { Text = "arena marker" }.Apply(Findings), Has.Count.EqualTo(1));
            Assert.That(new FindingFilter { Text = "arena spawner" }.Apply(Findings), Is.Empty);
        }

        [Test]
        public void Blank_search_text_matches_everything()
        {
            Assert.That(new FindingFilter { Text = "   " }.Apply(Findings), Has.Count.EqualTo(3));
            Assert.That(new FindingFilter { Text = null }.Apply(Findings), Has.Count.EqualTo(3));
        }
    }

    public sealed class PreferenceTests
    {
        private static readonly BoolPreference Flag = new BoolPreference("Test.Flag", "Flag", "tip", true);
        private static readonly NumberPreference Size = new NumberPreference("Test.Size", "Size", "tip", 10f, 1f, 100f);
        private static readonly TextPreference Folder = new TextPreference("Test.Folder", "Folder", "tip", "Logs");

        [Test]
        public void An_unset_preference_reads_its_default()
        {
            var store = new MemoryPreferenceStore();

            Assert.That(Flag.Get(store), Is.True);
            Assert.That(Size.Get(store), Is.EqualTo(10f));
            Assert.That(Folder.Get(store), Is.EqualTo("Logs"));
        }

        [Test]
        public void A_set_value_is_read_back_and_reset_restores_the_default()
        {
            var store = new MemoryPreferenceStore();

            Flag.Set(store, false);
            Size.Set(store, 25f);
            Folder.Set(store, "Reports");

            Assert.That(Flag.Get(store), Is.False);
            Assert.That(Size.Get(store), Is.EqualTo(25f));
            Assert.That(Folder.Get(store), Is.EqualTo("Reports"));

            Flag.Reset(store);
            Size.Reset(store);
            Folder.Reset(store);

            Assert.That(Flag.Get(store), Is.True);
            Assert.That(Size.Get(store), Is.EqualTo(10f));
            Assert.That(Folder.Get(store), Is.EqualTo("Logs"));
        }

        [Test]
        public void A_number_is_kept_inside_its_range()
        {
            var store = new MemoryPreferenceStore();

            Size.Set(store, 5000f);
            Assert.That(Size.Get(store), Is.EqualTo(100f));

            store.SetNumber(Size.Key, -3f);
            Assert.That(Size.Get(store), Is.EqualTo(1f));
        }

        [Test]
        public void A_field_shows_the_stored_value_with_its_label_and_tooltip()
        {
            // A field only raises change events once it is in a window, so writing back is not covered here.
            var store = new MemoryPreferenceStore();
            Flag.Set(store, false);
            Size.Set(store, 40f);
            Folder.Set(store, "Reports");

            var toggle = (Toggle)Flag.CreateField(store);
            Assert.That(toggle.value, Is.False);
            Assert.That(toggle.label, Is.EqualTo("Flag"));
            Assert.That(toggle.tooltip, Is.EqualTo("tip"));
            Assert.That(((FloatField)Size.CreateField(store)).value, Is.EqualTo(40f));
            Assert.That(((TextField)Folder.CreateField(store)).value, Is.EqualTo("Reports"));
        }

        [Test]
        public void A_page_has_a_field_per_preference()
        {
            var store = new MemoryPreferenceStore();
            var root = new VisualElement();

            PreferencesPage.Build(root, "Title", "Summary", new Preference[] { Flag, Size, Folder }, store);

            Assert.That(root.Query<VisualElement>(className: "devkit-page__field").ToList(), Has.Count.EqualTo(3));
            Assert.That(root.Q<Label>(className: "devkit-page__title").text, Is.EqualTo("Title"));
        }

        [Test]
        public void The_shared_style_sheet_is_found()
        {
            var root = new VisualElement();

            DevKitStyle.Apply(root);

            Assert.That(root.styleSheets.count, Is.EqualTo(1));
        }
    }
}
