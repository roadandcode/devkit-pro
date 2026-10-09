using System;
using System.Diagnostics;
using RoadAndCode.DevKit.Common;
using UnityEditor;

namespace RoadAndCode.DevKit.DataBrowser
{
    /// <summary>
    /// Command-line entry point. Writes an inventory of the project's ScriptableObjects by type and
    /// fails when an asset's script is missing:
    /// <code>Unity -batchmode -projectPath . -executeMethod RoadAndCode.DevKit.DataBrowser.CatalogCommand.Run</code>
    /// Optional: <c>-devkitAllTypes</c> (include types from Unity and installed packages),
    /// <c>-devkitSubAssets</c>, <c>-devkitReport &lt;path&gt;</c>, <c>-devkitFailOn error|warning|info|never</c>.
    /// </summary>
    public static class CatalogCommand
    {
        public const string AllTypesArgument = "-devkitAllTypes";
        public const string SubAssetsArgument = "-devkitSubAssets";

        public static void Run()
        {
            EditorApplication.Exit(Execute(CommandLineArguments.FromProcess(), new UnityCatalogReader(), UnityEngine.Debug.Log));
        }

        public static int Execute(CommandLineArguments arguments, ICatalogReader reader, Action<string> log)
        {
            var watch = Stopwatch.StartNew();
            var catalog = new Catalog(reader.Read(arguments.Has(SubAssetsArgument), new NullProgress()));
            ScanReport report = CatalogReport.From(catalog, !arguments.Has(AllTypesArgument), watch.Elapsed);
            return CommandOutcome.Finish(report, arguments, text => log("[DevKit] " + text));
        }
    }
}
