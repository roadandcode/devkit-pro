using UnityEditor;
using UnityEngine.UIElements;

namespace RoadAndCode.DevKit.Common
{
    /// <summary>Menu and shortcut names, so every tool files itself under the same headings.</summary>
    public static class DevKitMenu
    {
        public const string Root = "Tools/DevKit Pro/";

        public const string Shortcuts = "DevKit Pro/";
    }

    /// <summary>The style sheet the windows and preference pages share.</summary>
    public static class DevKitStyle
    {
        // Found by GUID, not by path: the package can sit under Packages/ (Git URL) or under Assets/ (.unitypackage).
        private const string SheetGuid = "7d3f0c1a5b8e4f2a9c6d1e0b3a4f5c6d";

        public static void Apply(VisualElement root)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(SheetGuid));
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            root.AddToClassList("devkit-root");
        }
    }
}
