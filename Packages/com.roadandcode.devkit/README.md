# DevKit Pro

Four editor tools I reuse across my Unity projects. Each has a window under **Tools > DevKit Pro** and a command-line entry point that writes a report and sets the exit code.

| Tool | What it does | Shortcut |
| --- | --- | --- |
| Scene and Asset Validator | Finds missing scripts, missing references, missing prefabs, reference fields left empty on your own scripts and empty objects, in scenes, prefabs, ScriptableObjects and materials | Alt+Shift+V validates the open scenes |
| Build Runner | Builds the profiles listed in a Build Plan asset (Web, Windows, Android), each to its own output path, after checking the plan | Alt+Shift+B |
| Addressables Audit | Finds groups nothing references, AssetReferences that lead nowhere, dependencies copied into several bundles, bundles over a size limit, reused addresses and empty groups | Alt+Shift+G |
| ScriptableObject Browser | Lists every ScriptableObject by type with search and an inline inspector | Alt+Shift+O |

Settings are under **Preferences > DevKit Pro**. Shortcuts can be changed under **Edit > Shortcuts**, in the DevKit Pro category.

The package needs Unity 6000.5 and nothing else. The Addressables Audit is only compiled when the Addressables package is installed.

## Command line

```
Unity -batchmode -projectPath <project> -executeMethod <entry point> [arguments]
```

| Entry point | Arguments |
| --- | --- |
| `RoadAndCode.DevKit.Validation.ValidatorCommand.Run` | `-devkitScope open,build,scenes,prefabs,assets,project,all` (default `project`: build scenes, prefabs, assets), `-devkitRules <ids>`, `-devkitOptionalFields <patterns>` |
| `RoadAndCode.DevKit.Builds.BuildCommand.Run` | `-devkitProfiles <names>` (default: every enabled profile), `-devkitPlan <asset path>` |
| `RoadAndCode.DevKit.AddressablesAudit.AuditCommand.Run` | `-devkitMaxBundleMb <number>` (default 10), `-devkitPackageAssets` |
| `RoadAndCode.DevKit.DataBrowser.CatalogCommand.Run` | `-devkitAllTypes`, `-devkitSubAssets` |

Every entry point also takes `-devkitReport <path>` (a `.json` path gets JSON, anything else plain text) and `-devkitFailOn error|warning|info|never` (default `error`).

Exit codes: `0` nothing at or above the threshold, `1` something was found (or a build failed), `2` the tool could not run.

A command-line run does not read the Preferences. It starts from the defaults so that the same command gives the same answer on every machine.

The full write-up, with screenshots, is in the repository: https://github.com/roadandcode/devkit-pro
