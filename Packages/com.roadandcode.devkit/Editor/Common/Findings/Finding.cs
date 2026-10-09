namespace RoadAndCode.DevKit.Common
{
    public enum Severity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    /// <summary>One thing a tool found, as plain data. Windows list it and reports write it out.</summary>
    public sealed class Finding
    {
        public Finding(string rule, Severity severity, string message, string assetPath = "", string objectPath = "", string detail = "")
        {
            Rule = rule ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            ObjectPath = objectPath ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        /// <summary>Stable id of the check that produced this, such as <c>missing-script</c>.</summary>
        public string Rule { get; }

        public Severity Severity { get; }

        public string Message { get; }

        /// <summary>Project-relative path of the scene, prefab or asset, or empty.</summary>
        public string AssetPath { get; }

        /// <summary>Path of the object inside the scene or prefab, or empty.</summary>
        public string ObjectPath { get; }

        /// <summary>The component and property, or whatever narrows the location further. May be empty.</summary>
        public string Detail { get; }

        /// <summary>Asset and object path joined for display.</summary>
        public string Location
        {
            get
            {
                if (ObjectPath.Length == 0) return AssetPath;
                return AssetPath.Length == 0 ? ObjectPath : AssetPath + " > " + ObjectPath;
            }
        }

        public override string ToString() => $"[{Severity}] {Rule}: {Message} ({Location})";
    }
}
