using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Connects shared scenery materials in the existing environment without replacing scene objects or their identities.</summary>
    public static class FishingSurfaceSceneryBuilder
    {
        private const string Root = "Assets/_Game/";
        /// <summary>Prepares serialized shader references for Android inclusion and runtime-only decorative geometry.</summary>
        [MenuItem("Fishing/Prepare coherent surface scenery")]
        public static string Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing scenery.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before preparing scenery.");
            string path = Root + "Prefabs/Environment.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); AssetDatabase.SaveAssets(); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "Connected one overcast sky, shared submerged ice and shallow refraction materials; existing environment identities retained.";
        }

        /// <summary>Keeps scenery bindings when the original environment preparation command runs again.</summary>
        public static void Configure(GameObject root)
        {
            var sky = Material("OvercastSky", "Fishing/Overcast Sky");
            var ice = Material("SubmergedIce", "Fishing/Submerged Ice");
            var water = Material("ShallowWater", "Fishing/Shallow Water Refraction");
            var ground = Material("SnowGround", "Fishing/Snow Ground");
            var mountains = Material("DistantMountains", "Fishing/Distant Mountains");
            ground.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Environment/B07_Ice_Surface_Left.png"));
            var waterDepth = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/Materials/Underwater/WaterDepth.mat");
            var caustics = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/Materials/Underwater/IceCaustics.mat");
            waterDepth.SetFloat("_SurfaceY",.86f); caustics.SetFloat("_SurfaceY",.86f);
            EditorUtility.SetDirty(waterDepth); EditorUtility.SetDirty(caustics);
            var flow = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Textures/Water/WaterSmooth.png");
            ice.SetTexture("_FlowTex", flow); water.SetTexture("_FlowTex", flow);
            ice.SetTexture("_CausticA", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Textures/caustics/caust00.png"));
            ice.SetTexture("_CausticB", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Textures/caustics/caust01.png"));
            var view = new SerializedObject(root.GetComponent<Fishing.Game.FishingSceneView>());
            view.FindProperty("skyAtmosphereMaterial").objectReferenceValue = sky;
            view.FindProperty("submergedIceMaterial").objectReferenceValue = ice;
            view.FindProperty("waterSurfaceMaterial").objectReferenceValue = water;
            view.FindProperty("snowGroundMaterial").objectReferenceValue = ground;
            view.FindProperty("distantMountainMaterial").objectReferenceValue = mountains;
            view.FindProperty("submergedIceTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Environment/IceWalls/IceWall_C_Shoulder_Left.png");
            view.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(ice); EditorUtility.SetDirty(water); EditorUtility.SetDirty(ground);
        }

        private static Material Material(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName); if (shader == null) throw new InvalidOperationException("Missing scenery shader " + shaderName);
            string path = Root + "Art/Materials/Scenery/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else if (material.shader != shader) { material.shader = shader; EditorUtility.SetDirty(material); }
            return material;
        }
    }
}
