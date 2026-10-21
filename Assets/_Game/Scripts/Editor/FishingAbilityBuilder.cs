using System;
using Fishing.Core;
using Fishing.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Creates missing ability assets, assigns dedicated layers and binds existing scenes.</summary>
    public static class FishingAbilityBuilder
    {
        /// <summary>Prepares stage 03 without replacing gameplay, art, environment or scene identities.</summary>
        [MenuItem("Fishing/Prepare stage 03 abilities")]
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing abilities.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before preparing abilities.");
            int small = Layer(FishActor.SmallFishLayer), branchLayer = Layer("FishingBranchHook");
            BranchCollisionMatrix(small, branchLayer);
            var art = AssetDatabase.LoadAssetAtPath<GameUiAssets>("Assets/_Game/Configs/GameUiAssets.asset");
            var sonar = AssetDatabase.LoadAssetAtPath<SonarConfig>("Assets/_Game/Configs/SonarConfig.asset");
            if (sonar == null)
            {
                sonar = ScriptableObject.CreateInstance<SonarConfig>(); sonar.displayName = "声呐"; sonar.icon = art.bubble;
                sonar.uses = 2; sonar.cooldown = 4; sonar.allowedState = RunState.Diving;
                AssetDatabase.CreateAsset(sonar, "Assets/_Game/Configs/SonarConfig.asset");
            }
            var branches = AssetDatabase.LoadAssetAtPath<BranchHookConfig>("Assets/_Game/Configs/BranchHookConfig.asset");
            if (branches == null)
            {
                branches = ScriptableObject.CreateInstance<BranchHookConfig>(); branches.displayName = "分散鱼钩"; branches.icon = art.hook;
                branches.uses = 1; branches.allowedState = RunState.Ascending;
                AssetDatabase.CreateAsset(branches, "Assets/_Game/Configs/BranchHookConfig.asset");
            }
            AssetDatabase.SaveAssets(); var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in new[] { "Main", "Menu" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/" + name + ".unity");
                    var target = new SerializedObject(UnityEngine.Object.FindObjectOfType<GameBootstrap>());
                    target.FindProperty("sonarConfig").objectReferenceValue = sonar;
                    target.FindProperty("branchConfig").objectReferenceValue = branches;
                    target.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return "Bound sonar/branch snapshots, small-fish and branch layers, and both existing scenes.";
        }
        private static int Layer(string name)
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            for (int i = 8; i < 32; i++) if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
            for (int i = 8; i < 32; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i); if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = name; settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); return i;
            }
            throw new InvalidOperationException("No unused layer for " + name);
        }
        private static void BranchCollisionMatrix(int small, int branch)
        {
            const string path = "ProjectSettings/Physics2DSettings.asset";
            string original = System.IO.File.ReadAllText(path);
            for (int i = 0; i < 32; i++) Physics2D.IgnoreLayerCollision(branch, i, i != small);
            EditorUtility.SetDirty(AssetDatabase.LoadAllAssetsAtPath(path)[0]); AssetDatabase.SaveAssets();
            const string pattern = @"(?m)^  m_LayerCollisionMatrix: [0-9a-fA-F]+";
            string matrix = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(path), pattern).Value;
            if (matrix.Length == 0) throw new InvalidOperationException("Missing serialized 2D collision matrix.");
            // Saving old Unity settings can remove unrelated serialized fields; retain everything except our matrix.
            System.IO.File.WriteAllText(path, System.Text.RegularExpressions.Regex.Replace(original, pattern, matrix));
        }
    }
}
