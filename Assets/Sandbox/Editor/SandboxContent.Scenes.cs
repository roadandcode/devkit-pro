using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadAndCode.DevKit.Sandbox.Editor
{
    public static partial class SandboxContent
    {
        private static void WriteScenes(Prefabs prefabs, Material paint, Material doomed)
        {
            WriteCleanScene(prefabs, paint);
            WriteArenaScene(paint);
            WriteBrokenScene(prefabs, doomed);

            // The arena scene is addressable instead. Addressables takes a scene out of this list
            // the moment it is made addressable, so it is not put in.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CleanScene, true) };
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // What the web build shows, and what the validator should have nothing to say about.
        private static void WriteCleanScene(Prefabs prefabs, Material paint)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" };
            camera.transform.position = new Vector3(0f, 1.6f, -6f);
            camera.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            var lens = camera.AddComponent<Camera>();
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = new Color(0.09f, 0.1f, 0.13f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.6f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.2f, 4f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = paint;

            var turntable = new GameObject("Turntable");
            turntable.AddComponent<Spinner>();

            var stage = new GameObject("Stage");
            var loader = stage.AddComponent<ContentLoader>();
            using (var serialized = new SerializedObject(loader))
            {
                // An AssetReference is its target's GUID, which is what the audit looks for.
                serialized.FindProperty("_crate").FindPropertyRelative("m_AssetGUID").stringValue = Guid(prefabs.Crate);
                serialized.FindProperty("_crateParent").objectReferenceValue = turntable.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene, CleanScene);
        }

        private static void WriteArenaScene(Material paint)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(12f, 0.2f, 12f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = paint;

            EditorSceneManager.SaveScene(scene, ArenaScene);
        }

        // One object per thing the validator checks for. Break() finishes the job after the save.
        private static void WriteBrokenScene(Prefabs prefabs, Material doomed)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded here, not passed in: an asset instance made earlier is unloaded by the scene changes in between.
            var waves = AssetDatabase.LoadAssetAtPath<WaveTable>(WaveTablePath);

            var gate = new GameObject("Gate");
            var spawnPoint = new GameObject("SpawnPoint");
            spawnPoint.transform.SetParent(gate.transform);
            var spawner = gate.AddComponent<Spawner>();
            using (var serialized = new SerializedObject(spawner))
            {
                // _prefab is left empty: an unassigned reference. _optionalTint is too, and is allowed to be.
                serialized.FindProperty("_spawnPoint").objectReferenceValue = spawnPoint.transform;
                serialized.FindProperty("_waves").objectReferenceValue = waves;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            // Its material is deleted afterwards: a missing reference.
            GameObject statue = GameObject.CreatePrimitive(PrimitiveType.Cube);
            statue.name = "Statue";
            statue.GetComponent<MeshRenderer>().sharedMaterial = doomed;

            // Its component's script is cut loose afterwards: a missing script.
            new GameObject("Ghost").AddComponent<Spinner>();

            // Nothing on it, nothing under it.
            new GameObject("Leftover");

            // Healthy prefab instances under a parent, which the validator should leave alone.
            var props = new GameObject("Props");
            ((GameObject)PrefabUtility.InstantiatePrefab(prefabs.Crate, scene)).transform.SetParent(props.transform);
            ((GameObject)PrefabUtility.InstantiatePrefab(prefabs.Barrel, scene)).transform.SetParent(props.transform);

            // Its prefab is deleted afterwards: a missing prefab.
            PrefabUtility.InstantiatePrefab(prefabs.Doomed, scene);

            EditorSceneManager.SaveScene(scene, BrokenScene);
        }

        private static string Guid(Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    }
}
