using System;
using System.Collections.Generic;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Which findings a list shows: by severity and by words typed into its search field.</summary>
    public sealed class FindingFilter
    {
        private static readonly char[] Spaces = { ' ' };

        private string[] _terms = new string[0];

        public bool ShowErrors { get; set; } = true;

        public bool ShowWarnings { get; set; } = true;

        public bool ShowInfo { get; set; } = true;

        public string Text
        {
            get => string.Join(" ", _terms);
            set => _terms = (value ?? string.Empty).Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
        }

        public bool Matches(Finding finding)
        {
            if (!Shows(finding.Severity)) return false;

            // Every word has to be somewhere in the finding; where does not matter.
            for (int i = 0; i < _terms.Length; i++)
            {
                if (!Contains(finding, _terms[i])) return false;
            }

            return true;
        }

        public List<Finding> Apply(IReadOnlyList<Finding> findings)
        {
            var visible = new List<Finding>(findings.Count);
            for (int i = 0; i < findings.Count; i++)
            {
                if (Matches(findings[i])) visible.Add(findings[i]);
            }

            return visible;
        }

        private bool Shows(Severity severity)
        {
            if (severity == Severity.Error) return ShowErrors;
            return severity == Severity.Warning ? ShowWarnings : ShowInfo;
        }

        private static bool Contains(Finding finding, string term)
        {
            return Has(finding.Message, term) || Has(finding.Rule, term) || Has(finding.AssetPath, term)
                   || Has(finding.ObjectPath, term) || Has(finding.Detail, term);
        }

        private static bool Has(string text, string term) => text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
