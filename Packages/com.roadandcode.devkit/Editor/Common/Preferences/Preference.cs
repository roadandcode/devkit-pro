using System;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>
    /// One setting: its key, how it is labelled and what it defaults to. A tool declares its settings
    /// as a list of these; the preferences page draws whatever is in the list.
    /// </summary>
    public abstract class Preference
    {
        protected Preference(string key, string label, string tooltip)
        {
            Key = key;
            Label = label;
            Tooltip = tooltip;
        }

        public string Key { get; }

        public string Label { get; }

        public string Tooltip { get; }

        /// <summary>A field that shows the stored value and writes changes straight back.</summary>
        public abstract VisualElement CreateField(IPreferenceStore store);

        public void Reset(IPreferenceStore store) => store.Remove(Key);
    }

    public sealed class BoolPreference : Preference
    {
        public BoolPreference(string key, string label, string tooltip, bool fallback) : base(key, label, tooltip)
        {
            Default = fallback;
        }

        public bool Default { get; }

        public bool Get(IPreferenceStore store) => store.GetBool(Key, Default);

        public void Set(IPreferenceStore store, bool value) => store.SetBool(Key, value);

        public override VisualElement CreateField(IPreferenceStore store)
        {
            var field = new Toggle(Label) { value = Get(store), tooltip = Tooltip };
            field.RegisterValueChangedCallback(change => Set(store, change.newValue));
            return field;
        }
    }

    public sealed class NumberPreference : Preference
    {
        public NumberPreference(string key, string label, string tooltip, float fallback, float minimum, float maximum) : base(key, label, tooltip)
        {
            Default = fallback;
            Minimum = minimum;
            Maximum = maximum;
        }

        public float Default { get; }

        public float Minimum { get; }

        public float Maximum { get; }

        public float Get(IPreferenceStore store) => Clamp(store.GetNumber(Key, Default));

        public void Set(IPreferenceStore store, float value) => store.SetNumber(Key, Clamp(value));

        public override VisualElement CreateField(IPreferenceStore store)
        {
            var field = new FloatField(Label) { value = Get(store), tooltip = Tooltip, isDelayed = true };
            field.RegisterValueChangedCallback(change =>
            {
                Set(store, change.newValue);
                field.SetValueWithoutNotify(Get(store));
            });
            return field;
        }

        private float Clamp(float value) => Math.Min(Maximum, Math.Max(Minimum, value));
    }

    public sealed class TextPreference : Preference
    {
        public TextPreference(string key, string label, string tooltip, string fallback) : base(key, label, tooltip)
        {
            Default = fallback;
        }

        public string Default { get; }

        public string Get(IPreferenceStore store) => store.GetText(Key, Default);

        public void Set(IPreferenceStore store, string value) => store.SetText(Key, value ?? string.Empty);

        public override VisualElement CreateField(IPreferenceStore store)
        {
            var field = new TextField(Label) { value = Get(store), tooltip = Tooltip, isDelayed = true };
            field.RegisterValueChangedCallback(change => Set(store, change.newValue));
            return field;
        }
    }
}
