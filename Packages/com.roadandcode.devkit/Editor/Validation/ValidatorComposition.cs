using RoadAndCode.DevKit.Common;

namespace RoadAndCode.DevKit.Validation
{
    /// <summary>The one place the validator's concrete types are named and wired together.</summary>
    public static class ValidatorComposition
    {
        public static ValidatorPresenter Present(IValidatorView view)
        {
            var preferences = new EditorPreferenceStore();
            var scanner = new ValidationScanner(new UnityProjectReader(), () => ValidationPreferences.Rules(preferences));
            return new ValidatorPresenter(view, scanner, preferences, new EditorFindingLocator(preferences),
                new ReportFolderSaver(preferences), () => new EditorProgressBar("Validating"));
        }
    }
}
