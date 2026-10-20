using System;
using System.Collections.Generic;
using System.IO;
using Fishing.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Builds the shared ice/water environment and binds it into the existing Menu and Main scenes.</summary>
    public static class FishingEnvironmentBuilder
    {
        private const string Root = "Assets/_Game/";
        private static Material _material;

        /// <summary>Imports the supplied environment art and saves the second stage-02 checkpoint.</summary>
        [MenuItem("Fishing/Prepare stage 02 environment")]
        public static string Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing the environment.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before preparing the environment.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            _material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/SpriteUnlit.mat");
            var config = AssetDatabase.LoadAssetAtPath<FishingConfig>(Root + "Configs/FishingConfig.asset");
            var prefab = BuildPrefab(config);
            try
            {
                foreach (string name in new[] { "Menu", "Main" })
                {
                    var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity", OpenSceneMode.Single);
                    GameBootstrap game = null; FishingSceneView view = null;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        if (root.name == "Placeholder Environment") root.SetActive(false);
                        if (root.GetComponent<GameBootstrap>() != null) game = root.GetComponent<GameBootstrap>();
                        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == Root + "Prefabs/Environment.prefab")
                        {
                            if (view == null) view = root.GetComponent<FishingSceneView>();
                            else UnityEngine.Object.DestroyImmediate(root);
                        }
                    }
                    if (view == null) view = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<FishingSceneView>();
                    view.gameObject.name = "Fishing Environment";
                    game.BindSceneView(view); EditorUtility.SetDirty(game);
                    var camera = Camera.main;
                    camera.backgroundColor = new Color(.025f, .1f, .18f); new CameraRig(camera, config).Reset();
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return "Saved shared ice surface, fisherman poses, props, continuous water and ice walls in Menu/Main.";
        }

        private static GameObject BuildPrefab(FishingConfig config)
        {
            var idle = Art("B01_Fisherman_Idle", 2.414f, true, new Vector2(.5f, .028f), false);
            var pull = Art("B02_Fisherman_Pull", 2.414f, true, new Vector2(.5f, .028f), false);
            var rod = Art("B03_FishingRod", 2.8f, false, new Vector2(.32f, .35f));
            var iceLeft = Art("B07_Ice_Surface_Left", 5.9f, false, new Vector2(.5f, 1));
            var iceRight = Art("B08_Ice_Surface_Right", 5.9f, false, new Vector2(.5f, 1));
            var wallLeft = Art("B09_Ice_Wall_Left", 5, true, Vector2.one * .5f);
            var wallRight = Art("B10_Ice_Wall_Right", 5, true, Vector2.one * .5f);
            var mountains = Art("B11_Mountains_Background", 13, false, new Vector2(.5f, 0));
            var clouds = Art("B12_Clouds_Overcast", 14, false, Vector2.one * .5f);
            var pole = Art("B13_Weather_Pole", 2.9f, true, new Vector2(.5f, 0));
            var anemometer = Art("B14_Anemometer", .9f, false, new Vector2(.5f, .08f));
            var windsock = Art("B15_Windsock", 1.5f, false, new Vector2(1, .5f));
            var lantern = Art("B16_Lantern", 1, true, new Vector2(.5f, 0));
            var barrel = Art("B17_Barrel_Snow", 1.2f, true, new Vector2(.5f, 0));
            var stand = Art("B18_Wooden_Stand", 1.2f, false, new Vector2(.5f, 0));
            var largeFish = Art("A09_Background_LargeFish", 3.6f, false, Vector2.one * .5f);
            var shoal = Art("A10_Background_Shoal", 2.8f, false, Vector2.one * .5f);
            var sky = Gradient("SkyGradient", new Color(.42f, .61f, .75f), new Color(.74f, .86f, .9f));
            var water = Gradient("WaterGradient", new Color(.1f, .43f, .55f), new Color(.018f, .065f, .15f));
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/PlaceholderSquare.png");
            string path = Root + "Prefabs/Environment.prefab";
            bool existing = File.Exists(path);
            var root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Fishing Environment");
            try
            {
                var view = root.GetComponent<FishingSceneView>(); if (view == null) view = root.AddComponent<FishingSceneView>();
                var surface = Child(root.transform, "Surface");
                Sized("Sky", surface, sky, new Vector2(0, 12.85f), new Vector2(14, 25), Color.white, -50);
                Place("Mountains", surface, mountains, new Vector2(0, 1.05f), Color.white, -45);
                Place("Clouds", surface, clouds, new Vector2(0, 5.4f), new Color(.92f, .97f, 1, .8f), -44);
                Place("Ice Left", surface, iceLeft, new Vector2(-3.25f, .98f), Color.white, 25);
                Place("Ice Right", surface, iceRight, new Vector2(3.25f, .98f), Color.white, 25);
                var fisherman = Place("Fisherman", surface, idle, new Vector2(-2.35f, .76f), Color.white, 30);
                var rodView = Place("Rod", fisherman.transform, rod, new Vector2(.5f, 1.12f), Color.white, 29);
                var tip = Child(rodView.transform, "Rod Tip");
                tip.localPosition = new Vector3(rod.bounds.max.x - .04f, rod.bounds.max.y - .04f, 0);
                var guide = Child(surface, "Line Guide"); guide.localPosition = new Vector3(0, .35f, 0);
                Place("Weather Pole", surface, pole, new Vector2(3.1f, .76f), Color.white, 27);
                Place("Anemometer", surface, anemometer, new Vector2(3.1f, 3.59f), Color.white, 28);
                Place("Windsock", surface, windsock, new Vector2(3.1f, 3.18f), Color.white, 28);
                Place("Barrel", surface, barrel, new Vector2(4.05f, .78f), Color.white, 28);
                Place("Stand", surface, stand, new Vector2(1.65f, .78f), Color.white, 27);
                Place("Lantern", surface, lantern, new Vector2(1.65f, 1.4f), Color.white, 28);

                var underwater = Child(root.transform, "Underwater");
                float depth = config.maxDepth * config.unitsPerMetre + 30;
                Sized("Water", underwater, water, new Vector2(0, (.35f - depth) * .5f),
                    new Vector2(14, depth + .35f), Color.white, -40);
                Sized("Seabed", underwater, square, new Vector2(0, -(depth + config.maxDepth * config.unitsPerMetre + 3) * .5f),
                    new Vector2(14, depth - config.maxDepth * config.unitsPerMetre - 3), new Color(.045f, .12f, .17f), -15);
                var walls = Child(underwater, "Walls");
                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? -1 : 1; var sprite = side == 0 ? wallLeft : wallRight;
                    Sized(side == 0 ? "Left Backing" : "Right Backing", underwater, square,
                        new Vector2(sign * 5.45f, -depth * .5f), new Vector2(1.5f, depth), new Color(.08f, .22f, .31f), -14);
                    int count = Mathf.CeilToInt(depth / 4.9f);
                    for (int i = 0; i < count; i++)
                    {
                        var wall = Place((side == 0 ? "Left " : "Right ") + i.ToString("00"), walls, sprite,
                            new Vector2(sign * (4.72f + sprite.bounds.extents.x), -.9f - 2.5f - i * 4.9f), Color.white, 6);
                        wall.flipY = i % 2 == 1;
                    }
                }
                var distant = Child(underwater, "Distant Fish"); var backgrounds = new List<Transform>();
                for (int i = 0; i < 8; i++)
                {
                    var fish = Place("Shadow " + i, distant, i % 2 == 0 ? largeFish : shoal,
                        new Vector2(i % 2 == 0 ? -2.1f : 2.2f, -16 - i * 12), new Color(.45f, .65f, .75f, .23f), -25);
                    fish.flipX = i % 3 == 0; backgrounds.Add(fish.transform);
                }
                view.Bind(fisherman, idle, pull, rodView.transform, tip, guide, backgrounds.ToArray()); view.ApplyState(Fishing.Core.RunState.Menu);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            { if (existing) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Sprite Art(string name, float size, bool height, Vector2 pivot, bool trim = true) =>
            FishingCoreArtImporter.Import(name, "Environment", size, height, pivot, trim);

        private static Sprite Gradient(string name, Color top, Color bottom)
        {
            string path = Root + "Art/Environment/" + name + ".png";
            var texture = new Texture2D(4, 512, TextureFormat.RGBA32, false); var pixels = new Color[2048];
            for (int y = 0; y < 512; y++) for (int x = 0; x < 4; x++) pixels[y * 4 + x] = Color.Lerp(bottom, top, y / 511f);
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 4;
            importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name); if (child != null) return child;
            child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }

        private static SpriteRenderer Place(string name, Transform parent, Sprite sprite, Vector2 position, Color color, int order)
        {
            var child = Child(parent, name); child.localPosition = position; child.localScale = Vector3.one; child.localRotation = Quaternion.identity;
            var renderer = child.GetComponent<SpriteRenderer>(); if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = _material; renderer.color = color; renderer.sortingOrder = order;
            return renderer;
        }

        private static void Sized(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
        {
            var renderer = Place(name, parent, sprite, position, color, order);
            renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
        }
    }
}
