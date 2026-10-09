using UnityEditor;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Where a long scan says how far it is. The answer says whether to carry on.</summary>
    public interface IProgressSink
    {
        /// <returns>False when the person asked to stop.</returns>
        bool Report(string activity, float fraction);

        void Done();
    }

    /// <summary>For the command line and for tests: nothing to draw, nobody to cancel.</summary>
    public sealed class NullProgress : IProgressSink
    {
        public bool Report(string activity, float fraction) => true;

        public void Done()
        {
        }
    }

    /// <summary>The editor's own progress bar with a cancel button.</summary>
    public sealed class EditorProgressBar : IProgressSink
    {
        private readonly string _title;

        public EditorProgressBar(string title)
        {
            _title = title;
        }

        public bool Report(string activity, float fraction) => !EditorUtility.DisplayCancelableProgressBar(_title, activity, fraction);

        public void Done() => EditorUtility.ClearProgressBar();
    }
}
