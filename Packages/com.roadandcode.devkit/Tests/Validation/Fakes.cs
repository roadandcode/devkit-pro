using System;
using System.Collections.Generic;
using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Validation.Tests
{
    /// <summary>Shorthand for building records by hand.</summary>
    internal static class Records
    {
        public static ReferenceRecord Reference(string path, ReferenceState state) => new ReferenceRecord(path, state);

        public static ComponentRecord Script(string type, params ReferenceRecord[] references) => new ComponentRecord(type, true, references);

        public static ComponentRecord BuiltIn(string type, params ReferenceRecord[] references) => new ComponentRecord(type, false, references);

        public static ComponentRecord Transform() => new ComponentRecord("Transform", false, null);

        public static ObjectRecord SceneObject(string objectPath, params ComponentRecord[] components)
        {
            return new ObjectRecord("Assets/Scene.unity", objectPath, ObjectKind.SceneObject, components);
        }

        public static List<Finding> Check(IValidationRule rule, ObjectRecord record)
        {
            var findings = new List<Finding>();
            rule.Check(record, findings);
            return findings;
        }
    }

    internal sealed class FakeReader : IProjectReader
    {
        private readonly List<ObjectRecord> _records = new List<ObjectRecord>();

        public ValidationScope LastScope { get; private set; }

        public int Reads { get; private set; }

        public ReadSummary Summary { get; } = new ReadSummary();

        public FakeReader With(params ObjectRecord[] records)
        {
            _records.AddRange(records);
            return this;
        }

        public ReadSummary Read(ValidationScope scope, Action<ObjectRecord> visit, IProgressSink progress)
        {
            LastScope = scope;
            Reads++;
            foreach (ObjectRecord record in _records) visit(record);
            Summary.Objects = _records.Count;
            return Summary;
        }
    }

    internal sealed class FakeView : IValidatorView
    {
        public event Action ScanRequested;

        public event Action<ValidationScope> ScopeToggled;

        public event Action<Finding> FindingChosen;

        public event Action SaveRequested;

        public ValidationScope Scope { get; private set; }

        public ScanReport Report { get; private set; }

        public string Status { get; private set; }

        public void ShowScope(ValidationScope scope) => Scope = scope;

        public void ShowReport(ScanReport report) => Report = report;

        public void ShowStatus(string text) => Status = text;

        public void ClickValidate() => ScanRequested?.Invoke();

        public void Toggle(ValidationScope part) => ScopeToggled?.Invoke(part);

        public void Choose(Finding finding) => FindingChosen?.Invoke(finding);

        public void ClickSave() => SaveRequested?.Invoke();
    }

    internal sealed class FakeLocator : IFindingLocator
    {
        public Finding Revealed { get; private set; }

        public void Reveal(Finding finding) => Revealed = finding;
    }

    internal sealed class FakeSaver : IReportSaver
    {
        public ScanReport Saved { get; private set; }

        public string Save(ScanReport report)
        {
            Saved = report;
            return "Logs/DevKit/report.json";
        }
    }

    internal sealed class CountingProgress : IProgressSink
    {
        public int DoneCalls { get; private set; }

        public bool Report(string activity, float fraction) => true;

        public void Done() => DoneCalls++;
    }
}
