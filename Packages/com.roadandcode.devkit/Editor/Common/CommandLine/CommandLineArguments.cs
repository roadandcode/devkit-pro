using System;
using System.Collections.Generic;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Reads <c>-name value</c> pairs from the editor's command line.</summary>
    public sealed class CommandLineArguments
    {
        /// <summary>Where to write the report. The extension picks the format.</summary>
        public const string Report = "-devkitReport";

        /// <summary>The lowest severity that makes the run fail: error (default), warning, info or never.</summary>
        public const string FailOn = "-devkitFailOn";

        private static readonly char[] Comma = { ',' };

        private readonly IReadOnlyList<string> _arguments;

        public CommandLineArguments(IReadOnlyList<string> arguments)
        {
            _arguments = arguments ?? new string[0];
        }

        public static CommandLineArguments FromProcess() => new CommandLineArguments(Environment.GetCommandLineArgs());

        public bool Has(string name) => IndexOf(name) >= 0;

        /// <summary>The value after <paramref name="name"/>, or the fallback when the name is absent or has no value.</summary>
        public string Value(string name, string fallback = null)
        {
            int index = IndexOf(name);
            if (index < 0 || index + 1 >= _arguments.Count) return fallback;

            string value = _arguments[index + 1];
            return value.StartsWith("-", StringComparison.Ordinal) ? fallback : value;
        }

        /// <summary>A comma-separated value as its parts, trimmed. Empty when the name is absent.</summary>
        public IReadOnlyList<string> List(string name)
        {
            string value = Value(name);
            if (string.IsNullOrEmpty(value)) return new string[0];

            string[] parts = value.Split(Comma, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
            return parts;
        }

        public bool TryNumber(string name, out float number)
        {
            return float.TryParse(Value(name), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out number);
        }

        private int IndexOf(string name)
        {
            for (int i = 0; i < _arguments.Count; i++)
            {
                if (string.Equals(_arguments[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }
    }
}
