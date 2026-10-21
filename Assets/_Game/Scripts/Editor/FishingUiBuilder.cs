using System;
using Fishing.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Imports existing UI/tutorial art and connects the shared Chinese UI assets to saved scenes.</summary>
    public static class FishingUiBuilder
    {
        private const string Root = "Assets/_Game/";
        /// <summary>Updates UI references while preserving existing gameplay, environment and scene identities.</summary>
        [MenuItem("Fishing/Prepare stage 02 Chinese UI")]
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing Chinese UI.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before preparing Chinese UI.");
            string path = Root + "Configs/GameUiAssets.asset";
            var assets = AssetDatabase.LoadAssetAtPath<GameUiAssets>(path);
            if (assets == null) { assets = ScriptableObject.CreateInstance<GameUiAssets>(); AssetDatabase.CreateAsset(assets, path); }
            assets.font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Art/Fonts/NotoSansSC-Regular.otf");
            if (assets.font == null) throw new InvalidOperationException("Missing bundled Noto Sans SC font.");
            assets.panel = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            assets.coin = Import("C01_Icon_Coin"); assets.shell = Import("C02_Icon_Shell"); assets.star = Import("C03_Icon_Star");
            assets.protection = Import("C04_Icon_Protection"); assets.depth = Import("C06_Icon_Depth"); assets.rod = Import("C07_Result_Rod");
            assets.hook = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Hook/B04_Hook_Main.png");
            assets.bubble = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Hook/B05_Bubble_Shield.png");
            assets.frame = Import("C08_Tutorial_Frame");
            var frameImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(assets.frame));
            frameImporter.spriteBorder = Vector4.one * 120; frameImporter.SaveAndReimport();
            assets.coral = Import("C09_Tutorial_Coral"); assets.blueFish = Import("C10_Tutorial_Fish_Blue");
            assets.purpleFish = Import("C11_Tutorial_Fish_Purple");
            assets.silverFish = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Fish/C12_Tutorial_Fish_Silver.png");
            assets.spottedFish = Import("C13_Tutorial_Fish_Spotted"); assets.orangeConch = Import("C14_Tutorial_Conch_Orange");
            assets.greyConch = Import("C15_Tutorial_Conch_Grey"); assets.rocks = Import("C16_Tutorial_Rocks"); assets.barrel = Import("C17_Tutorial_Barrel");
            EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in new[] { "Main", "Menu" })
                {
                    var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
                    var bootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
                    if (bootstrap == null) throw new InvalidOperationException("Missing bootstrap in " + name);
                    var serialized = new SerializedObject(bootstrap); serialized.FindProperty("uiAssets").objectReferenceValue = assets;
                    serialized.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return "Connected Chinese UI font, 15 existing UI/tutorial images, reused main hook and silver fish, and Main/Menu UI references.";
        }
        private static Sprite Import(string source) => FishingCoreArtImporter.Import(source, "UI", 10, false, Vector2.one * .5f);
    }
}
