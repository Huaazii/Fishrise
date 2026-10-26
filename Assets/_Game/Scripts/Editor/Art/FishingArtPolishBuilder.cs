using System;
using System.IO;
using Fishing.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Connects small CC0 art copies to shared presentation assets without rebuilding gameplay or scenes.</summary>
    public static class FishingArtPolishBuilder
    {
        private const string Root = "Assets/_Game/";
        /// <summary>Prepares the common button skin and bounded ambient water pool in place.</summary>
        [MenuItem("Fishing/Prepare button and ambient details")]
        public static string Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing art.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before preparing art.");
            var ui = AssetDatabase.LoadAssetAtPath<GameUiAssets>(Root + "Configs/GameUiAssets.asset");
            ui.buttonFace = Import("SourceSprites/UI/button_rectangle_gradient.png", "ButtonIceFace", true);
            ui.buttonRim = Import("SourceSprites/UI/button_rectangle_border.png", "ButtonIceRim", true);
            var bubbles = new[] {
                Import("SourceSprites/Bubbles/bubble_2_grayscale.png", "BubbleSoft", false),
                Import("SourceSprites/Bubbles/bubble_4_grayscale.png", "BubbleClear", false) };
            EditorUtility.SetDirty(ui);
            string path = Root + "Prefabs/Environment.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var parent = root.transform.Find("Underwater/Ambient details");
                if (parent == null)
                { parent = new GameObject("Ambient details").transform; parent.SetParent(root.transform.Find("Underwater"), false); }
                var details = parent.GetComponent<AmbientWaterDetails>();
                if (details == null) details = parent.gameObject.AddComponent<AmbientWaterDetails>();
                details.Bind(bubbles, AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Textures/Generated/PlaceholderDisk.png"),
                    AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/Materials/SpriteUnlit.mat"),
                    root.transform.Find("Surface/Line Guide").position.y,
                    root.transform.Find("Underwater/Seabed").GetComponent<SpriteRenderer>().bounds.max.y);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            return "Connected ice-blue nine-slice buttons and 30 pooled water details; selected source sprites and gameplay preserved.";
        }

        private static Sprite Import(string source, string name, bool button)
        {
            string directory = Root + (button ? "Art/UI/Controls" : "Art/Textures/Bubbles");
            Directory.CreateDirectory(directory);
            string path = directory + "/" + name + ".png";
            byte[] data = File.ReadAllBytes(Root + "Art/" + source);
            if (!File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(data, File.ReadAllBytes(path)))
                File.WriteAllBytes(path, data);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256; importer.spriteBorder = button ? Vector4.one * 8 : Vector4.zero;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
