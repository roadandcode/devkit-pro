# Performance and proof of use

An editor package has no frame time to budget. What matters is how long a scan keeps the editor busy, whether adding the package costs the project anything, and whether the tools hold up on a project they were not written against.

Everything here was measured on one machine: Windows 11, Unity 6000.5.8f1, projects on an SSD. No other Unity editor was running during the timed runs.

## Budget

Set before the first measurement, for a project the size of mine (a couple of scenes, a few hundred objects, a few dozen assets):

| What | Budget |
| --- | --- |
| Validate the whole project | under 1 s |
| Audit the Addressables setup | under 1 s |
| List every ScriptableObject | under 0.5 s |
| Compiler warnings the package adds to a project | 0 |

## Scan times on two real projects

The package was installed by Git URL into copies of two of my projects, Tactica AI and Plane Simulation. Each copy has the project's `Assets`, `Packages` and `ProjectSettings` and one added line in `Packages/manifest.json`; the originals were not written to. Each tool was run three times from the command line, a fresh editor process each time. "Scan" is the time the tool reports for its own work. "Launch" is the whole process, most of which is the editor starting.

| Project | Tool | What it read | Scan, 3 runs | Launch |
| --- | --- | --- | --- | --- |
| Tactica AI | Validator, every scene, prefab and asset | 2 scenes, 5 prefabs, 48 assets, 146 objects | 176, 175, 176 ms | 10.0 to 10.4 s |
| Tactica AI | Addressables Audit | 2 groups, 1 entry, 3 built bundles (1.07 MB) | 74, 71, 76 ms | 9.9 to 10.0 s |
| Tactica AI | ScriptableObject catalog with sub-assets | 35 assets in 11 types | 58, 53, 52 ms | 9.8 to 10.2 s |
| Plane Simulation | Validator, every scene, prefab and asset | 2 scenes, 4 prefabs, 42 assets, 163 objects | 169, 168, 177 ms | 10.0 to 10.1 s |
| Plane Simulation | Addressables Audit | 1 group, 1 entry, 9 built bundles (12.31 MB) | 63, 56, 56 ms | 9.9 to 10.1 s |
| Plane Simulation | ScriptableObject catalog with sub-assets | 12 assets in 8 types | 81, 48, 48 ms | 9.9 to 10.0 s |

All three are well inside the budget. Both projects are small, so this says the fixed cost is low; it says nothing yet about a project with thousands of objects.

Both copies opened with zero compiler warnings after the package was added.

## What the tools found there

| Project | Tool | Result |
| --- | --- | --- |
| Tactica AI | Validator | 1 error: `Assets/Settings/DefaultVolumeProfile.asset` holds 9 objects made from scripts that are missing. 2 notes: `Board/Terrain` and `Board/Units` in the match scene are empty |
| Tactica AI | Addressables Audit | 1 warning: `Default Local Group` has no entries |
| Tactica AI | Catalog | 35 ScriptableObjects in 11 types, none with a missing script |
| Plane Simulation | Validator | 1 error: the same volume profile, the same 9 objects. 18 notes: empty objects, among them the sockets on the jet's prefab |
| Plane Simulation | Addressables Audit | Nothing |
| Plane Simulation | Catalog | 12 ScriptableObjects in 8 types, none with a missing script |

The nine objects are components from Unity's own render pipeline tests (`VolumeComponentCopyPasteTests` and others), which the URP project template leaves in its default volume profile. Their scripts are in no project.

The first run reported 16 errors per project. Seven were references under `UniversalRendererData.probeVolumeResources`, a field URP marks obsolete, pointing at shaders the package no longer ships. The validator now leaves references under an obsolete field out, and reports missing scripts inside an asset once per file. The numbers above are from after both changes.

## The sample project

| What | Result |
| --- | --- |
| Validator, every scene, prefab and asset (3 scenes, 7 prefabs, 61 objects) | about 40 ms; 6 errors, 2 warnings, 2 notes, all of them planted |
| Addressables Audit (5 groups, 7 entries, 5 built bundles, size limit lowered to 0.15 MB) | about 20 ms; 2 errors, 7 warnings, 1 note |
| All 225 EditMode tests, including the editor starting | 8 to 10 s |
| Web build through the Build Runner, first time | 59 s, 5.7 MB |
| Web build through the Build Runner, again with nothing changed | 4 to 5 s, 6.1 MB with the Addressables bundles in it |

The first web build is the one that came out without its bundles; see the Build Runner note in `architecture.md`. The second figure is the build as it is now: Gzip, with the decompression fallback, from a profile that overrides the project's own settings and puts them back. It was loaded in a browser from a local server: the scene drew, the two objects that come from Addressables bundles appeared, and the console had no errors.

## Installing

| Route | Result |
| --- | --- |
| Git URL into an empty project with no Addressables | Resolved, compiled with zero warnings. Validator and catalog ran and exited with 0. The Build Runner exited with 2 for want of a plan, and the audit with 2 because Addressables is not installed, both as intended |
| Git URL into the two project copies | Resolved, compiled with zero warnings next to URP, the Input System, VContainer and Addressables |
| `.unitypackage` into an empty project | 68 files imported under `Packages/com.roadandcode.devkit`, picked up as an embedded package, compiled with zero warnings, commands ran |

## Not measured

- A large project. Everything above is a few hundred objects at most.
- Memory. The validator loads every prefab and ScriptableObject it reads and does not unload them.
- The windows under real use: redraw time with thousands of findings, the progress bar's cancel.
- A Windows or Android build through the Build Runner.
- Any machine but this one, and any Unity version but 6000.5.8f1.
