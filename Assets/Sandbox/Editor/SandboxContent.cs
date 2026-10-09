using System;
using System.IO;
using RoadAndCode.DevKit.Builds;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoadAndCode.DevKit.Sandbox.Editor
{
    /// <summary>
    /// Builds the sample content from nothing, then breaks it on purpose so every check in the
    /// package has something to find. The result is committed; run this again only to change it.
    /// <code>Unity -batchmode -quit -projectPath . -executeMethod RoadAndCode.DevKit.Sandbox.Editor.SandboxContent.BuildAll</code>
    /// </summary>
    public static partial class SandboxContent
    {
        public const string Root = "Assets/Sandbox/Content";
        public const string CleanScene = Root + "/Scenes/Clean.unity";
        public const string ArenaScene = Root + "/Scenes/Arena.unity";
        public const string BrokenScene = Root + "/Scenes/Broken.unity";
        public const string WaveTablePath = Root + "/Data/Waves/FirstWave.asset";
        public const string RetiredTablePath = Root + "/Data/Waves/Retired.asset";

        // Stands in for the GUID of something that was deleted.
        private const string GoneGuid = "deadbeefdeadbeefdeadbeefdeadbeef";
        private const string Identifier = "com.roadandcode.devkitsandbox";

        private static readonly string[] Folders = { "Textures", "Materials", "Prefabs", "Data/Items", "Data/Enemies", "Data/Waves", "Scenes" };

        public static void BuildAll()
        {
            Clear();
            ApplyProjectSettings();

            Texture2D wood = WriteTexture("Wood", 64, WoodPixel, false);
            Texture2D noise = WriteTexture("Noise", 256, NoisePixel(), true);

            Material woodMaterial = WriteMaterial("Wood", "Unlit/Texture", material => material.mainTexture = wood);
            Material stoneMaterial = WriteMaterial("Stone", "Unlit/Texture", material => material.mainTexture = noise);
            Material paintMaterial = WriteMaterial("Paint", "Unlit/Color", material => material.color = new Color(0.25f, 0.45f, 0.7f));
            Material doomedMaterial = WriteMaterial("Doomed", "Unlit/Color", material => material.color = Color.magenta);

            var prefabs = new Prefabs
            {
                Crate = WritePrefab("Crate", PrimitiveType.Cube, woodMaterial, Vector3.one),
                Barrel = WritePrefab("Barrel", PrimitiveType.Cylinder, woodMaterial, new Vector3(0.7f, 0.5f, 0.7f)),
                Bench = WritePrefab("Bench", PrimitiveType.Cube, woodMaterial, new Vector3(2f, 0.3f, 0.6f)),
                Pillar = WritePrefab("Pillar", PrimitiveType.Cylinder, stoneMaterial, new Vector3(0.6f, 1.5f, 0.6f)),
                OldCrate = WritePrefab("OldCrate", PrimitiveType.Cube, paintMaterial, Vector3.one),
                OldBarrel = WritePrefab("OldBarrel", PrimitiveType.Cylinder, paintMaterial, new Vector3(0.7f, 0.5f, 0.7f)),
                Doomed = WritePrefab("Doomed", PrimitiveType.Sphere, paintMaterial, Vector3.one),

                // Never made addressable, but an AssetReference in the broken scene points at it.
                Lantern = WritePrefab("Lantern", PrimitiveType.Capsule, paintMaterial, new Vector3(0.3f, 0.3f, 0.3f)),
            };

            WriteData(wood, prefabs);
            WriteScenes(prefabs, paintMaterial, doomedMaterial);
            WriteAddressables(prefabs);
            new AssetBuildPlanStore().Create();
            AssetDatabase.SaveAssets();

            Break();
            Debug.Log("[Sandbox] Sample content built and broken.");
        }

        private sealed class Prefabs
        {
            public GameObject Crate { get; set; }

            public GameObject Barrel { get; set; }

            public GameObject Bench { get; set; }

            public GameObject Pillar { get; set; }

            public GameObject OldCrate { get; set; }

            public GameObject OldBarrel { get; set; }

            public GameObject Doomed { get; set; }

            public GameObject Lantern { get; set; }
        }

        private static void Clear()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (string path in new[] { Root, "Assets/AddressableAssetsData", "Assets/Settings/DevKit" })
            {
                if (AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path);
            }

            foreach (string folder in Folders) Directory.CreateDirectory($"{Root}/{folder}");
            AssetDatabase.Refresh();
        }

        private static void ApplyProjectSettings()
        {
            PlayerSettings.companyName = "Ravi Chaudhary";
            PlayerSettings.productName = "DevKit Sandbox";
            PlayerSettings.bundleVersion = "0.1.0";
            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.WebGL })
            {
                PlayerSettings.SetApplicationIdentifier(target, Identifier);
            }
        }

        private static Color32 WoodPixel(int x, int y)
        {
            bool grain = (x + (y / 16) * 5) % 8 < 2;
            return grain ? new Color32(112, 78, 46, 255) : new Color32(150, 108, 66, 255);
        }

        // Noise does not compress, which is what makes the group that uses it heavy.
        private static Func<int, int, Color32> NoisePixel()
        {
            var random = new System.Random(7);
            return (x, y) => new Color32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255);
        }

        private static Texture2D WriteTexture(string name, int size, Func<int, int, Color32> pixel, bool uncompressed)
        {
            string path = $"{Root}/Textures/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) pixels[y * size + x] = pixel(x, y);
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material WriteMaterial(string name, string shader, Action<Material> fill)
        {
            var material = new Material(Shader.Find(shader));
            fill(material);
            AssetDatabase.CreateAsset(material, $"{Root}/Materials/{name}.mat");
            return material;
        }

        private static GameObject WritePrefab(string name, PrimitiveType shape, Material material, Vector3 scale)
        {
            GameObject instance = GameObject.CreatePrimitive(shape);
            instance.name = name;
            instance.transform.localScale = scale;
            Object.DestroyImmediate(instance.GetComponent<Collider>());
            instance.GetComponent<MeshRenderer>().sharedMaterial = material;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"{Root}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void WriteData(Texture2D icon, Prefabs prefabs)
        {
            ItemDefinition sword = WriteItem("Sword", 120, icon, prefabs.Crate);
            ItemDefinition shield = WriteItem("Shield", 90, icon, prefabs.Barrel);

            // No world prefab: the validator should say so.
            ItemDefinition potion = WriteItem("Potion", 15, icon, null);

            EnemyDefinition grunt = WriteEnemy("Grunt", 20, prefabs.Crate, potion);

            // Its prefab is deleted at the end, leaving a missing reference in an asset.
            EnemyDefinition archer = WriteEnemy("Archer", 12, prefabs.Doomed, sword, shield);

            WriteAsset<WaveTable>(WaveTablePath, serialized =>
            {
                SerializedProperty waves = serialized.FindProperty("_waves");
                waves.arraySize = 2;
                SetWave(waves.GetArrayElementAtIndex(0), grunt, 3);
                SetWave(waves.GetArrayElementAtIndex(1), archer, 2);
            });
        }

        private static ItemDefinition WriteItem(string name, int price, Texture2D icon, GameObject worldPrefab)
        {
            return WriteAsset<ItemDefinition>($"{Root}/Data/Items/{name}.asset", serialized =>
            {
                serialized.FindProperty("_displayName").stringValue = name;
                serialized.FindProperty("_price").intValue = price;
                serialized.FindProperty("_icon").objectReferenceValue = icon;
                serialized.FindProperty("_worldPrefab").objectReferenceValue = worldPrefab;
            });
        }

        private static EnemyDefinition WriteEnemy(string name, int health, GameObject prefab, params ItemDefinition[] drops)
        {
            return WriteAsset<EnemyDefinition>($"{Root}/Data/Enemies/{name}.asset", serialized =>
            {
                serialized.FindProperty("_displayName").stringValue = name;
                serialized.FindProperty("_health").intValue = health;
                serialized.FindProperty("_prefab").objectReferenceValue = prefab;
                SerializedProperty list = serialized.FindProperty("_drops");
                list.arraySize = drops.Length;
                for (int i = 0; i < drops.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = drops[i];
            });
        }

        private static void SetWave(SerializedProperty wave, EnemyDefinition enemy, int count)
        {
            wave.FindPropertyRelative("_enemy").objectReferenceValue = enemy;
            wave.FindPropertyRelative("_count").intValue = count;
        }

        private static T WriteAsset<T>(string path, Action<SerializedObject> fill) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            using (var serialized = new SerializedObject(asset))
            {
                fill(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
