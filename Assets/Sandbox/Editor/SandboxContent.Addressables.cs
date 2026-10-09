using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace RoadAndCode.DevKit.Sandbox.Editor
{
    public static partial class SandboxContent
    {
        public const string PropsGroup = "Props";
        public const string SceneryGroup = "Scenery";
        public const string ScenesGroup = "Scenes";
        public const string OrphansGroup = "Orphans";

        private const string PropsLabel = "props";

        // The default group that comes with new settings stays empty, for the audit's empty-group check.
        private static void WriteAddressables(Prefabs prefabs)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.AddLabel(PropsLabel);

            // Reached through an AssetReference in the Clean scene, and through a label in code.
            AddressableAssetGroup props = Group(settings, PropsGroup);
            Entry(settings, props, prefabs.Crate, "props/crate").SetLabel(PropsLabel, true);
            Entry(settings, props, prefabs.Barrel, "props/barrel").SetLabel(PropsLabel, true);

            // Reached by address. The pillar's noise texture makes this the heavy group, and the
            // bench shares its wood with the props group without the wood being addressable.
            AddressableAssetGroup scenery = Group(settings, SceneryGroup);
            Entry(settings, scenery, prefabs.Pillar, "scenery/pillar");
            Entry(settings, scenery, prefabs.Bench, "scenery/bench");

            // A scene loaded by address.
            AddressableAssetGroup scenes = Group(settings, ScenesGroup);
            Entry(settings, scenes, AssetDatabase.LoadMainAssetAtPath(ArenaScene), "scenes/arena");

            // Nothing loads these. One of them also reuses an address from the props group.
            AddressableAssetGroup orphans = Group(settings, OrphansGroup);
            Entry(settings, orphans, prefabs.OldCrate, "orphans/old-crate");
            Entry(settings, orphans, prefabs.OldBarrel, "props/barrel");

            EditorUtility.SetDirty(settings);
        }

        private static AddressableAssetGroup Group(AddressableAssetSettings settings, string name)
        {
            return settings.CreateGroup(name, false, false, false, settings.DefaultGroup.Schemas);
        }

        private static AddressableAssetEntry Entry(AddressableAssetSettings settings, AddressableAssetGroup group, Object asset, string address)
        {
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(Guid(asset), group, false, false);
            entry.address = address;
            EditorUtility.SetDirty(group);
            return entry;
        }

        // Everything is saved by now. These are the things that cannot be made through the editor
        // API, because they are exactly what the editor stops you from doing. An addressable entry
        // for a deleted asset is not among them: Addressables removes one the next time it imports
        // the group, so it would not survive a fresh clone.
        private static void Break()
        {
            string spinnerGuid = AssetDatabase.AssetPathToGUID("Assets/Sandbox/Scripts/Spinner.cs");

            AssetDatabase.DeleteAsset($"{Root}/Materials/Doomed.mat");
            AssetDatabase.DeleteAsset($"{Root}/Prefabs/Doomed.prefab");

            // The ghost's component keeps pointing at a script that is not there.
            string scene = File.ReadAllText(BrokenScene);
            File.WriteAllText(BrokenScene, scene.Replace("guid: " + spinnerGuid, "guid: " + GoneGuid));

            // An asset made from a script that is not there.
            File.WriteAllText($"{Root}/Data/Orphan.asset",
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                $"  m_Script: {{fileID: 11500000, guid: {GoneGuid}, type: 3}}\n  m_Name: Orphan\n  m_EditorClassIdentifier: \n  _displayName: Orphan\n");

            AssetDatabase.Refresh();
        }
    }
}
