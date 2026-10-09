# Architecture

DevKit Pro is an editor-only package, `com.roadandcode.devkit`, and the small Unity project it is developed in. The package lives at `Packages/com.roadandcode.devkit`; everything under `Assets/Sandbox` is sample content built to trip the tools, and it never ships.

The package follows the same engineering standards as my game projects. Some of those rules were written for games and have no meaning in a package that never runs in a player. Those are listed at the end with the reason, rather than quietly skipped.

## Module map

Every module is one assembly, and every assembly is editor-only. Nothing is in `Assembly-CSharp`.

| Assembly | Folder | What it is responsible for | May reference |
| --- | --- | --- | --- |
| `RoadAndCode.DevKit.Common` | `Editor/Common` | What the tools agree on: a finding, a report and how it is written to a file, command-line arguments and exit codes, the preference store, the findings list they all show | Unity only |
| `RoadAndCode.DevKit.Validation` | `Editor/Validation` | Scene and Asset Validator | Common |
| `RoadAndCode.DevKit.Builds` | `Editor/Builds` | Build Runner and the build plan asset | Common |
| `RoadAndCode.DevKit.AddressablesAudit` | `Editor/AddressablesAudit` | Addressables Audit: the rules, the window, the command. Knows nothing about the Addressables package | Common |
| `RoadAndCode.DevKit.AddressablesAudit.Adapter` | `Editor/AddressablesAudit.Adapter` | Reads the Addressables settings into the audit's own data. Compiled only when Addressables is installed | Common, AddressablesAudit, Addressables |
| `RoadAndCode.DevKit.DataBrowser` | `Editor/DataBrowser` | ScriptableObject Browser | Common |
| `RoadAndCode.DevKit.<Module>.Tests` | `Tests/<Module>` | EditMode tests, one assembly per module | The module under test, Common |

Sandbox, outside the package:

| Assembly | Folder | What it is |
| --- | --- | --- |
| `RoadAndCode.DevKit.Sandbox` | `Assets/Sandbox/Scripts` | A few components and ScriptableObject types for the sample content |
| `RoadAndCode.DevKit.Sandbox.Editor` | `Assets/Sandbox/Editor` | Builds the sample content, takes the README screenshots, exports the `.unitypackage` |
| `RoadAndCode.DevKit.Sandbox.Tests` | `Assets/Sandbox/Tests` | Runs each tool against the sample content end to end |

## Dependency rules

```
Validation   Builds   AddressablesAudit   DataBrowser      tools, never reference each other
     \          \           |      \           /
      \          \          |   AddressablesAudit.Adapter   only assembly that sees Addressables
       \          \         |         /
                    Common                                  references Unity and nothing else
```

- A tool references Common and nothing else. Two tools never reference each other, so any of them can be deleted without touching the rest.
- Common does not know which tools exist. A tool adds its own menu item, shortcut and preferences page.
- The package has no dependency on anything outside Unity's own modules. It does not use my `com.roadandcode.core` package and it does not use a DI container (see below). A tools package that pulls in a container and a runtime library would not get installed.
- The package never references the sandbox. The sandbox references the package.

Inside a tool the split is the one the standards ask for inside a feature:

| Folder | Holds | Rule |
| --- | --- | --- |
| `Analysis` (`Planning` in the Build Runner, `Catalog` in the browser) | Plain C# records and rules. Project data comes in as plain data, findings come out as data | No editor API. This is what the unit tests cover without a scene or a window |
| `Infrastructure` (the adapter assembly, for the audit) | Adapters that read the project through the editor API and hand the analysis its records. They implement interfaces the analysis owns | The only place that touches `AssetDatabase`, `SerializedObject`, `BuildPipeline`, the Addressables settings |
| `Presentation` | A UI Toolkit window as the view, and a presenter in plain C# | The view raises events and has setters. The presenter holds the behaviour and is tested with a fake view |
| module root | The composition class, the command-line entry point, the preferences | The only place that names concrete types |

One exception to "no editor API in the analysis": the Build Runner's plan is an asset, so `BuildPlan`, `BuildProfile` and `BuildOutcome` carry `[SerializeField]` and the plan derives from `ScriptableObject`. They are data and call nothing.

## Wiring without a container

The standards name VContainer as the container and say that one composition root per scope is the only place allowed to name concrete types. I kept the rule and dropped the container.

Each tool has one static `Composition` class. It builds the reader, the rules and the presenter with `new`, passing each its dependencies through the constructor. The window's `CreateGUI` and the command-line entry point both go through it and nothing else constructs anything. Every other class receives what it needs through its constructor and depends on interfaces the analysis layer owns.

A container would add nothing here. There are two scopes with a handful of objects each (a window that is open, a command that is running), nothing is ticked, and nothing has a lifetime longer than the call that created it. In return for doing it by hand the package has no third-party dependency.

The one place a concrete type cannot be named is the Addressables adapter: it lives in an assembly that may not exist. The audit's composition class finds the public implementation of `IAddressablesReader` through `TypeCache` and, when there is none, the tool says that Addressables is not installed. That lookup is used once, for that reason.

## Patterns used

| Pattern | Where | Why |
| --- | --- | --- |
| Model–View–Presenter | Every window | The windows cannot be opened in batch mode, so behaviour has to live somewhere a test can reach |
| Strategy | Validation rules, audit rules | A new check is a new class added to a list. The engine that runs them does not change |
| Adapter | `Infrastructure` in every tool, and the Addressables adapter assembly | The analysis sees my records, not `SerializedProperty` or `AddressableAssetGroup`, so it is testable and survives API changes |
| Repository | `IPreferenceStore`, `IBuildHistory`, `IBuildPlanStore` | Settings, the plan and the last build results sit behind interfaces with in-memory implementations for tests |
| Null object | `NullProgress`, `MemoryPreferenceStore` | The command line has no progress bar, and it must not read one machine's preferences |
| Plug-in discovery | The audit looking up its reader through `TypeCache` | The optional assembly cannot be referenced at compile time |
| Table instead of a switch | `PlatformTraits`, `PlatformTargets` | What differs between Web, Windows and Android is data in two tables; a new platform is a new row in each |

Not used, because nothing called for them: State (no tool has more than "idle" and "has results"), Command, Object Pool, Decorator, message bus.

## Command line

Every tool has a static `Run` method for `-executeMethod`. It builds the same objects the window does, runs the same analysis, writes a report (`.json` or text, by the file extension) and ends the editor with an exit code:

| Code | Meaning |
| --- | --- |
| 0 | Ran, nothing at or above the failure threshold |
| 1 | Ran, found something at or above the threshold, or a build failed |
| 2 | Could not run (bad arguments, Addressables not installed, no build plan, a plan with errors) |

A command-line run starts from the default settings and its own arguments. It never reads the Preferences, which belong to one person on one machine; the same command has to give the same answer on a build server.

## Decisions that came from running it

These were not in the plan. Each one is something the sample project or one of my real projects showed me.

- **The Build Runner switches the editor to a platform before building it.** `BuildPipeline.BuildPlayer` will build a platform the editor is not on, and my first version relied on that. The web build it made from a Windows editor had an Addressables catalog and no bundles: Addressables builds its content for the platform the editor is on. The builder now calls `SwitchActiveBuildTarget` first, the same as the Build Settings window does, and the profiles are ordered so each platform is switched to once.
- **The audit reports AssetReferences that lead nowhere, not only entries whose asset is gone.** I planted an entry for a deleted asset in the sample project. Addressables removed it by itself the next time the project opened, and again when it built, without failing anything. What stays behind is the `AssetReference` that pointed at it, which fails at run time. The rule for the entry is still there for the short time in which that state exists; the rule that matters is the one for the reference.
- **Addressables keeps a scene out of Build Settings once it is addressable**, and the other way round, as long as the edit happens in the editor. The audit's check for a scene in both lists therefore only fires on a state left by a merge or a hand edit. It is kept, and tested with hand-made data, but the sample project cannot hold that state.
- **A group's weight counts what its entries pull in.** The first version summed the entries' own files, which for a prefab is a few kilobytes while the texture it uses is the megabytes.
- **The validator skips references under a field marked `[Obsolete]`.** A URP renderer asset in two of my projects still points, from a field URP itself has retired, at shaders the package no longer ships. True, and nothing anyone can act on.
- **Missing scripts inside a healthy asset are one finding per file.** The default volume profile from Unity's URP project template carries nine components made from Unity's own test scripts. Nine identical lines said less than one line with a count.
- **Paths from the Addressables settings are normalised.** `ConfigFolder` comes back with a backslash on Windows. Until that was handled the audit read the group files themselves as references, and every address looked used.
- **Screenshots are copied from the window's own view**, not grabbed from the screen, so they do not depend on what else is in front of the editor. The method that does it is internal to the editor; the capture script is sandbox code, not part of the package.

## Where the standards do not apply

| Rule | Status here | Reason |
| --- | --- | --- |
| Layers App / Features / Shared / Core (§2.1) | Mapped, not copied | The package has no app and no game flow. Common plays the part of Shared, each tool is a feature, and the per-tool `Composition` class is the composition root |
| VContainer as the DI container (§2.2, §9) | Not used | See "Wiring without a container" |
| `com.roadandcode.core` (§6) | Not used | An editor package has to install into any project on its own. Nothing in the core package is needed by editor tools |
| Game flow state machine, message bus, ticking, pooling (§2.2) | Not applicable | Nothing runs between frames. A scan is one synchronous call |
| Async with `Awaitable` (§2.2) | Not used | The editor API the tools read through is main-thread only, and a scan that returns its report is simpler to test than one that completes later. Long scans report to a progress bar that can cancel |
| Content through Addressables (§2.2) | Not applicable | The package loads no content. Addressables is optional and only audited |
| Data in ScriptableObjects, read-only at runtime (§2.2) | Applies in part | The build plan is a ScriptableObject asset. The Build Runner window edits it, which is what an editor tool for a settings asset is for. There is no runtime |
| Platforms: Android, Windows, Web (§2.3) | Not applicable | The package runs in the Unity editor only. The sandbox is built for the web once, to prove the Build Runner |
| Input layers (§2.4) | Not applicable | No gameplay input. Shortcuts are registered with the Shortcut Manager so they can be rebound there |
| No static state, no singletons (§2.5) | Kept | Menu items, shortcuts and `-executeMethod` entry points have to be static because Unity calls them by name. They hold no state. The preference definitions are static and read-only |
| A class past roughly 150 lines is a prompt to split (§3) | Three are over | `DataBrowserWindow` (about 230 lines of UI wiring), `UnityProjectReader` and `AddressablesSettingsReader` (about 200 each, one walk through the project). They read top to bottom and I left them whole; they are the first candidates if either tool grows |
| PlayMode smoke test (§7) | Replaced | Nothing runs in play mode. The equivalent is the sandbox test assembly, which runs every tool against the sample content, and the command-line runs recorded in `performance.md` |
| Frame time, GC per frame, draw calls (§8) | Replaced | The budget is scan time against a real project. See `performance.md` |
| URP (§9) | Not used in the sandbox | Nothing is rendered that needs it. The sandbox uses the built-in pipeline with unlit shaders so it opens and builds quickly |
