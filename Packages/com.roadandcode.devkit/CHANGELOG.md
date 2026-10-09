# Changelog

## 0.1.0

First version. Four tools, each with a window, a shortcut, a preferences page and a command-line entry point.

- **Scene and Asset Validator.** Missing scripts, missing references, missing prefabs, reference fields left empty on the project's own scripts (with a list of field names that may be empty) and empty objects. Scope: open scenes, scenes in Build Settings, every scene, prefabs, ScriptableObjects and materials.
- **Build Runner.** A Build Plan asset lists the project's builds for Web, Windows and Android with an output path each. The plan is checked before anything is built. Web compression, the decompression fallback and the Android app bundle switch are applied for the length of a build and put back afterwards. The editor is switched to a platform before that platform is built, so Addressables content is built for the right one.
- **Addressables Audit.** Groups nothing references, AssetReferences whose target is gone or not addressable, dependencies copied into more than one bundle, bundles over a size limit (measured from the last build, estimated from source files when there is none), reused addresses, empty groups, and scenes that are addressable and in Build Settings at once. Compiled only when Addressables is installed.
- **ScriptableObject Browser.** Every ScriptableObject under Assets by type, without loading them, with search, a filter for the project's own types and an inline inspector. Assets whose script is missing are listed as such.
- Reports as JSON or plain text, and exit codes for CI.
