using System;
using System.IO;
using Fishing.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Imports the first stage-02 art checkpoint while retaining existing gameplay asset identities.</summary>
    public static class FishingCoreArtImporter
    {
        private const string Root = "Assets/_Game/";
        private static Material _material;

        /// <summary>Creates trimmed Unity copies and updates the four existing fish definitions and hook prefab.</summary>
        [MenuItem("Fishing/Import stage 02 core art")]
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing core art.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before importing core art.");
            _material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/SpriteUnlit.mat");
            Fish("blue", "A02_Fish_Striped", new Vector2(.52f, -.025f), .14f, new Vector2(.61f, -.025f));
            Fish("red", "A03_Fish_Red", new Vector2(.55f, .015f), .12f, new Vector2(.61f, .015f));
            Fish("small", "A04_Fish_SmallSilver", new Vector2(.53f, -.025f), .14f, new Vector2(.61f, -.025f));
            Fish("puffer", "A01_Fish_Puffer", new Vector2(.5f, .07f), .15f, new Vector2(.61f, .07f));
            var hook = Import("B04_Hook_Main", "Hook", 1.3f, true, new Vector2(.5f, .22f));
            var shield = Import("B05_Bubble_Shield", "Hook", 1.8f, false, Vector2.one * .5f);
            var bubble = Import("B06_Bubble_Small", "Hook", .16f, false, Vector2.one * .5f);
            UpdateHook(hook, shield, bubble);
            AssetDatabase.SaveAssets();
            return "Imported 4 fish, main hook, shield and small bubble; existing species and prefab GUIDs retained.";
        }

        private static void Fish(string id, string source, Vector2 head, float radius, Vector2 mouth)
        {
            var definition = AssetDatabase.LoadAssetAtPath<FishDefinition>(Root + "Configs/Fish_" + id + ".asset");
            if (definition == null) throw new InvalidOperationException("Missing existing fish definition: " + id);
            definition.sprite = Import(source, "Fish", 1.25f, false, Vector2.one * .5f);
            definition.headPosition = head; definition.headRadius = radius; definition.mouthPosition = mouth;
            EditorUtility.SetDirty(definition);
        }

        internal static Sprite Import(string name, string category, float worldSize, bool fitHeight, Vector2 pivot, bool trim = true)
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Arts/" + name + ".png"));
            string folder = Root + "Art/" + category;
            Directory.CreateDirectory(folder);
            string path = folder + "/" + name + ".png";
            var input = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D cropped = null;
            try
            {
                if (!ImageConversion.LoadImage(input, File.ReadAllBytes(source))) throw new IOException("Cannot decode " + source);
                var pixels = input.GetPixels32();
                int left = input.width, bottom = input.height, right = -1, top = -1;
                for (int y = 0; y < input.height; y++) for (int x = 0; x < input.width; x++)
                {
                    if (pixels[y * input.width + x].a < 16) continue;
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
                if (right < left) throw new IOException("Empty sprite: " + source);
                left = Mathf.Max(0, left - 4); bottom = Mathf.Max(0, bottom - 4);
                right = Mathf.Min(input.width - 1, right + 4); top = Mathf.Min(input.height - 1, top + 4);
                if (!trim) { left = bottom = 0; right = input.width - 1; top = input.height - 1; }
                cropped = new Texture2D(right - left + 1, top - bottom + 1, TextureFormat.RGBA32, false);
                cropped.SetPixels(input.GetPixels(left, bottom, cropped.width, cropped.height)); cropped.Apply();
                File.WriteAllBytes(path, cropped.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = (fitHeight ? cropped.height : cropped.width) / worldSize;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.isReadable = false;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally
            { UnityEngine.Object.DestroyImmediate(input); if (cropped != null) UnityEngine.Object.DestroyImmediate(cropped); }
        }

        private static void UpdateHook(Sprite hookSprite, Sprite shieldSprite, Sprite bubbleSprite)
        {
            string path = Root + "Prefabs/Hook.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.transform.Find("Stem").GetComponent<SpriteRenderer>().enabled = false;
                var head = root.transform.Find("Head").GetComponent<SpriteRenderer>();
                head.sprite = hookSprite; head.color = Color.white; head.transform.localScale = Vector3.one;
                head.transform.localPosition = Vector3.zero; head.sharedMaterial = _material;
                var lamp = root.transform.Find("Lamp").GetComponent<SpriteRenderer>();
                lamp.transform.localPosition = new Vector3(0, .59f, 0); lamp.transform.localScale = Vector3.one * .18f;
                lamp.sortingOrder = 22;
                var shield = root.transform.Find("Protection").GetComponent<SpriteRenderer>();
                shield.sprite = shieldSprite; shield.color = Color.white; shield.transform.localScale = Vector3.one;
                shield.transform.localPosition = new Vector3(0, .38f, 0); shield.sortingOrder = 23;
                var anchor = Child(root.transform, "Rope Anchor"); anchor.localPosition = new Vector3(0, 1.02f, 0);
                var rope = root.transform.Find("Rope").GetComponent<LineRenderer>();
                // Remain above one pixel in the 450-wide, 12-world-unit portrait preview.
                rope.startWidth = .045f; rope.endWidth = .04f;
                var slots = Child(root.transform, "Hang Points");
                var points = new Transform[10];
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = Child(slots, "Slot " + i);
                    points[i].localPosition = new Vector3((i % 2 == 0 ? -1 : 1) * (.2f + i / 2 * .025f),
                        -.04f - i / 2 * .025f, 0);
                }
                var trail = Child(root.transform, "Bubbles"); var bubbles = new SpriteRenderer[3];
                for (int i = 0; i < bubbles.Length; i++)
                {
                    var child = Child(trail, "Bubble " + i);
                    var renderer = child.GetComponent<SpriteRenderer>();
                    if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = bubbleSprite; renderer.sharedMaterial = _material; renderer.sortingOrder = 18;
                    renderer.color = new Color(1, 1, 1, .8f); child.localScale = Vector3.one * (1 - i * .18f);
                    bubbles[i] = renderer;
                }
                var motor = root.GetComponent<HookMotor>(); motor.BindPresentation(anchor, points, bubbles); motor.ResetPresentation();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }
    }
}
