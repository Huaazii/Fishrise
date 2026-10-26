using System;
using Fishing.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Connects the seven existing fish images and the four-band population without rebuilding scenes.</summary>
    public static class FishingPopulationBuilder
    {
        private const string Root = "Assets/_Game/";
        /// <summary>Updates population assets and only the species list on each saved scene bootstrap.</summary>
        [MenuItem("Fishing/Prepare stage 02 fish population")]
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing fish population.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before preparing fish population.");
            var blue = Configure("blue", "Bluefish", 10, 10, 1, 0, 1, .45f, .55f);
            var red = Configure("red", "Redfish", 15, 10, 2, 20, .85f, .75f, .9f);
            var small = Configure("small", "Little redfish", 5, 5, 1, 0, .5f, 1.1f, 1.3f, true);
            var puffer = Configure("puffer", "Pufferfish", 10, 10, 1, 0, 1.25f, .25f, .3f);
            var yellow = Configure("yellow", "Butterflyfish", 40, 30, 3, 60, 1.05f, .45f, .6f);
            var eel = Configure("eel", "Ribbon eel", 80, 50, 4, 85, 1.8f, .22f, .3f);
            var silver = Configure("silver", "Little silverfish", 3, 3, 1, 0, .4f, 1.2f, 1.4f, true);
            Art(yellow, "A05_Fish_Yellow", new Vector2(.53f, -.05f), .13f, new Vector2(.61f, -.05f));
            Art(eel, "A06_Fish_RibbonEel", new Vector2(.56f, .045f), .055f, new Vector2(.615f, .05f));
            Art(silver, "C12_Tutorial_Fish_Silver", new Vector2(.53f, -.015f), .12f, new Vector2(.61f, -.02f));
            var all = new[] { blue, red, small, puffer, yellow, eel, silver };
            foreach (var fish in all) FishingFishPrefabBuilder.CreateOrUpdate(fish);
            var config = AssetDatabase.LoadAssetAtPath<FishingConfig>(Root + "Configs/FishingConfig.asset");
            config.passageWidthMultiplier = 2.5f;
            config.depthBands = new[]
            {
                Band(0, 25, 5, Entry(puffer, 4), Entry(blue, 2), Entry(red, 1), Entry(small, 1, 3), Entry(silver, 4, 3)),
                Band(25, 50, 6, Entry(puffer, 4), Entry(blue, 3), Entry(red, 2), Entry(small, 1, 3), Entry(silver, 3, 3)),
                Band(50, 75, 6, Entry(puffer, 3), Entry(blue, 3), Entry(red, 2), Entry(small, 2, 3), Entry(silver, 3, 3), Entry(yellow, 1)),
                Band(75, 100, 6, Entry(puffer, 3), Entry(blue, 2), Entry(red, 1), Entry(small, 2, 3), Entry(silver, 5, 3), Entry(yellow, 1), Entry(eel, 1))
            };
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in new[] { "Main", "Menu" })
                {
                    var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
                    var bootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
                    if (bootstrap == null) throw new InvalidOperationException("Missing bootstrap in " + name);
                    var serialized = new SerializedObject(bootstrap); var array = serialized.FindProperty("species");
                    array.arraySize = all.Length;
                    for (int i = 0; i < all.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return "Configured 7 species and 4 depth bands: 12/13/14/15 fish (54 total); existing scene components retained.";
        }
        private static FishDefinition Configure(string id, string label, int gold, int shells, int rarity,
            float minDepth, float size, float speed, float maxSpeed, bool small = false)
        {
            string path = Root + "Configs/Fish_" + id + ".asset";
            var fish = AssetDatabase.LoadAssetAtPath<FishDefinition>(path);
            if (fish == null) { fish = ScriptableObject.CreateInstance<FishDefinition>(); AssetDatabase.CreateAsset(fish, path); }
            fish.speciesId = id; fish.displayName = label; fish.gold = gold; fish.shells = shells; fish.rarity = rarity;
            fish.minDepth = minDepth; fish.maxDepth = 100; fish.size = size;
            fish.swimSpeed = speed; fish.swimSpeedMax = maxSpeed; fish.isSmall = small; fish.isCatchable = true;
            EditorUtility.SetDirty(fish); return fish;
        }
        private static void Art(FishDefinition fish, string source, Vector2 head, float radius, Vector2 mouth)
        {
            fish.sprite = FishingCoreArtImporter.Import(source, "Fish", 1.25f, false, Vector2.one * .5f);
            fish.headPosition = head; fish.headRadius = radius; fish.mouthPosition = mouth;
            EditorUtility.SetDirty(fish);
        }
        private static FishPopulationEntry Entry(FishDefinition fish, int count, int schoolSize = 1) =>
            new FishPopulationEntry { fish = fish, count = count, schoolSize = schoolSize };
        private static FishDepthBand Band(float min, float max, int rows, params FishPopulationEntry[] population) =>
            new FishDepthBand { minDepth = min, maxDepth = max, rows = rows, population = population };
    }
}
