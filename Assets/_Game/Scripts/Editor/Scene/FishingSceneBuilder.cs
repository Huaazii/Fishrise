using System;
using System.IO;
using Fishing.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Creates missing milestone-one assets through Unity APIs, preserving existing asset GUIDs.</summary>
    public static class FishingSceneBuilder
    {
        private const string Root = "Assets/_Game/";
        private static Material _material;
        /// <summary>Prepares saved scenes and bindings without overwriting existing scenes or configuration.</summary>
        [MenuItem("Fishing/Prepare milestone 01")]
        public static string Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before preparing scenes.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before preparing fishing scenes.");
            foreach (string folder in new[] { "Art", "Prefabs", "Configs", "Scenes" }) Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();
            var disk = Shape("Disk", 0); var square = Shape("Square", 1); var ring = Shape("Ring", 2);
            _material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/Materials/SpriteUnlit.mat");
            if (_material == null)
            {
                _material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(_material, Root + "Art/Materials/SpriteUnlit.mat");
            }
            var config = AssetDatabase.LoadAssetAtPath<FishingConfig>(Root + "Configs/FishingConfig.asset");
            if (config == null)
            { config = ScriptableObject.CreateInstance<FishingConfig>(); AssetDatabase.CreateAsset(config, Root + "Configs/FishingConfig.asset"); }
            var species = new[]
            {
                Species("blue", "Bluefish", 10, 10, 1, .45f, new Color(.2f, .8f, 1), false),
                Species("red", "Redfish", 15, 10, .85f, .75f, new Color(1, .4f, .3f), false),
                Species("small", "Little redfish", 5, 5, .5f, 1.1f, new Color(1, .65f, .3f), true),
                Species("puffer", "Pufferfish", 10, 10, 1.25f, .25f, new Color(.95f, .8f, .35f), false)
            };
            if (species[0].sprite == null && File.Exists(Root + "Art/Textures/Generated/FishBlue.png"))
            {
                AssetDatabase.ImportAsset(Root + "Art/Textures/Generated/FishBlue.png");
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Textures/Generated/FishBlue.png");
                var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "Art/Textures/Generated/FishBlue.png");
                importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = texture.width;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
                species[0].sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Textures/Generated/FishBlue.png");
                EditorUtility.SetDirty(species[0]);
            }
            foreach (var definition in species) FishingFishPrefabBuilder.CreateOrUpdate(definition);
            var hook = HookPrefab(disk, ring, square);
            CreateScene("Menu", true, config, species, hook, square);
            CreateScene("Main", false, config, species, hook, square);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(Root + "Scenes/Menu.unity", true),
                new EditorBuildSettingsScene(Root + "Scenes/Main.unity", true)
            };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 450; PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.runInBackground = true;
            // Active Input Handling is intentionally left untouched: unity-cli-bridge
            // depends on com.unity.inputsystem, so forcing "Input Manager (Old)" only
            // makes the Editor warn on every domain reload.
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Root + "Scenes/Main.unity");
            return "Prepared Menu/Main, 4 species prefabs, Hook and portrait settings. Existing assets preserved.";
        }
        private static Sprite Shape(string name, int kind)
        {
            string path = Root + "Art/Textures/Generated/Placeholder" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                var pixels = new Color[4096];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float radius = new Vector2(x - 31.5f, y - 31.5f).magnitude;
                    bool filled = kind == 1 || (kind == 0 ? radius <= 30 : radius <= 30 && radius >= 27);
                    pixels[y * 64 + x] = filled ? Color.white : Color.clear;
                }
                texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 64;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static FishDefinition Species(string id, string name, int gold, int shells, float size, float speed, Color color, bool small)
        {
            string path = Root + "Configs/Fish_" + id + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<FishDefinition>(path);
            if (definition != null) return definition;
            definition = ScriptableObject.CreateInstance<FishDefinition>();
            definition.speciesId = id; definition.displayName = name; definition.gold = gold; definition.shells = shells;
            definition.size = size; definition.swimSpeed = speed; definition.color = color; definition.isSmall = small;
            AssetDatabase.CreateAsset(definition, path); return definition;
        }
        private static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 scale, Color color, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>(); renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = position; renderer.transform.localScale = new Vector3(scale.x, scale.y, 1);
            renderer.sprite = sprite; renderer.sharedMaterial = _material; renderer.color = color; renderer.sortingOrder = order;
            return renderer;
        }
        private static HookMotor HookPrefab(Sprite disk, Sprite ring, Sprite square)
        {
            string path = Root + "Prefabs/Hook.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<HookMotor>();
            var root = new GameObject("Hook"); var motor = root.AddComponent<HookMotor>();
            var body = root.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.useFullKinematicContacts = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var head = root.AddComponent<CircleCollider2D>(); head.radius = .22f; head.isTrigger = true;
            Sprite("Stem", root.transform, square, new Vector2(0, .19f), new Vector2(.12f, .65f), new Color(.85f, .94f, 1), 20);
            Sprite("Head", root.transform, ring, Vector2.zero, new Vector2(.5f, .5f), new Color(.85f, .94f, 1), 20);
            var lamp = Sprite("Lamp", root.transform, disk, new Vector2(0, .18f), Vector2.one * .2f, Color.green, 21);
            var shield = Sprite("Protection", root.transform, ring, Vector2.zero, Vector2.one * 1.05f, new Color(.4f, .85f, 1, .5f), 19);
            var line = new GameObject("Rope").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform, false);
            line.sharedMaterial = _material; line.positionCount = 2; line.useWorldSpace = true;
            line.startWidth = .035f; line.endWidth = .025f; line.startColor = line.endColor = new Color(.8f, .8f, .6f);
            line.sortingOrder = 0; motor.Bind(lamp, shield, head, line);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<HookMotor>();
        }
        private static void CreateScene(string name, bool menu, FishingConfig config, FishDefinition[] fish, HookMotor hook, Sprite square)
        {
            string path = Root + "Scenes/" + name + ".unity";
            if (File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 9.15f; camera.transform.position = new Vector3(0, -4.4f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .13f, .22f);
            camera.gameObject.AddComponent<AudioListener>();
            var bootstrap = new GameObject("Fishing Game").AddComponent<GameBootstrap>();
            bootstrap.Bind(config, fish, hook, camera, menu);
            var world = new GameObject("Placeholder Environment").transform;
            for (int i = 0; i < 4; i++)
                Sprite("Water " + i, world, square, new Vector2(0, -(i + .5f) * 25), new Vector2(9.8f, 25),
                    Color.Lerp(new Color(.08f, .39f, .52f), new Color(.025f, .12f, .23f), i / 3f), -30);
            Sprite("Left rock", world, square, new Vector2(-4.9f, -50), new Vector2(.5f, 101), new Color(.07f, .17f, .22f), -10);
            Sprite("Right rock", world, square, new Vector2(4.9f, -50), new Vector2(.5f, 101), new Color(.07f, .17f, .22f), -10);
            Sprite("Ice", world, square, new Vector2(0, .6f), new Vector2(10, .8f), new Color(.7f, .9f, .95f), -5);
            Sprite("Bottom", world, square, new Vector2(0, -101), new Vector2(10, 1), new Color(.13f, .23f, .24f), -10);
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
