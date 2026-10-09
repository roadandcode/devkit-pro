using System;
using System.Collections.Generic;

namespace RoadAndCode.DevKit.Validation
{
    [Flags]
    public enum ValidationScope
    {
        None = 0,

        /// <summary>The scenes loaded in the editor right now, saved or not.</summary>
        OpenScenes = 1,

        /// <summary>The enabled scenes in Build Settings.</summary>
        BuildScenes = 2,

        /// <summary>Every scene under Assets.</summary>
        AllScenes = 4,

        Prefabs = 8,

        /// <summary>ScriptableObjects and materials.</summary>
        Assets = 16,
    }

    public static class ValidationScopes
    {
        /// <summary>What a command-line run covers when it is not told otherwise.</summary>
        public const ValidationScope Project = ValidationScope.BuildScenes | ValidationScope.Prefabs | ValidationScope.Assets;

        public const ValidationScope Everything = ValidationScope.AllScenes | ValidationScope.Prefabs | ValidationScope.Assets;

        private static readonly Dictionary<string, ValidationScope> Names = new Dictionary<string, ValidationScope>(StringComparer.OrdinalIgnoreCase)
        {
            { "open", ValidationScope.OpenScenes },
            { "build", ValidationScope.BuildScenes },
            { "scenes", ValidationScope.AllScenes },
            { "prefabs", ValidationScope.Prefabs },
            { "assets", ValidationScope.Assets },
            { "project", Project },
            { "all", Everything },
        };

        /// <summary>Parses names such as <c>build,prefabs</c>. No names means the default project scope.</summary>
        public static bool TryParse(IReadOnlyList<string> names, out ValidationScope scope, out string unknown)
        {
            scope = ValidationScope.None;
            unknown = null;
            if (names.Count == 0)
            {
                scope = Project;
                return true;
            }

            for (int i = 0; i < names.Count; i++)
            {
                if (!Names.TryGetValue(names[i], out ValidationScope part))
                {
                    unknown = names[i];
                    return false;
                }

                scope |= part;
            }

            return true;
        }

        public static string Describe(ValidationScope scope)
        {
            var parts = new List<string>();
            if ((scope & ValidationScope.OpenScenes) != 0) parts.Add("open scenes");
            if ((scope & ValidationScope.AllScenes) != 0) parts.Add("all scenes");
            else if ((scope & ValidationScope.BuildScenes) != 0) parts.Add("build scenes");
            if ((scope & ValidationScope.Prefabs) != 0) parts.Add("prefabs");
            if ((scope & ValidationScope.Assets) != 0) parts.Add("assets");
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
        }
    }
}
