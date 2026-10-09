using System.Collections.Generic;
using UnityEditor;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Where per-user settings are kept. The tools never touch <c>EditorPrefs</c> directly.</summary>
    public interface IPreferenceStore
    {
        bool GetBool(string key, bool fallback);

        void SetBool(string key, bool value);

        float GetNumber(string key, float fallback);

        void SetNumber(string key, float value);

        string GetText(string key, string fallback);

        void SetText(string key, string value);

        void Remove(string key);
    }

    /// <summary>Backed by <c>EditorPrefs</c>, so a setting follows the person across projects on this machine.</summary>
    public sealed class EditorPreferenceStore : IPreferenceStore
    {
        private const string Prefix = "RoadAndCode.DevKit.";

        public bool GetBool(string key, bool fallback) => EditorPrefs.GetBool(Prefix + key, fallback);

        public void SetBool(string key, bool value) => EditorPrefs.SetBool(Prefix + key, value);

        public float GetNumber(string key, float fallback) => EditorPrefs.GetFloat(Prefix + key, fallback);

        public void SetNumber(string key, float value) => EditorPrefs.SetFloat(Prefix + key, value);

        public string GetText(string key, string fallback) => EditorPrefs.GetString(Prefix + key, fallback);

        public void SetText(string key, string value) => EditorPrefs.SetString(Prefix + key, value);

        public void Remove(string key) => EditorPrefs.DeleteKey(Prefix + key);
    }

    /// <summary>Holds values in memory. Used by tests, and by command-line runs that must not depend on one machine's settings.</summary>
    public sealed class MemoryPreferenceStore : IPreferenceStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public bool GetBool(string key, bool fallback) => _values.TryGetValue(key, out object value) && value is bool flag ? flag : fallback;

        public void SetBool(string key, bool value) => _values[key] = value;

        public float GetNumber(string key, float fallback) => _values.TryGetValue(key, out object value) && value is float number ? number : fallback;

        public void SetNumber(string key, float value) => _values[key] = value;

        public string GetText(string key, string fallback) => _values.TryGetValue(key, out object value) && value is string text ? text : fallback;

        public void SetText(string key, string value) => _values[key] = value;

        public void Remove(string key) => _values.Remove(key);
    }
}
