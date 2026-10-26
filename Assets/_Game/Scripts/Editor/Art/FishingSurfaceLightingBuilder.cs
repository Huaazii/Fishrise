using System;
using Fishing.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Prepares surface-only 2D lights and materials without rebuilding scenes, artwork or gameplay assets.</summary>
    public static class FishingSurfaceLightingBuilder
    {
        private const string Root = "Assets/_Game/";
        private const string Layer = "Surface Lit";
        /// <summary>Updates the common environment in place while retaining all existing object identities.</summary>
        [MenuItem("Fishing/Prepare surface lighting")]
        public static string Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing surface lighting.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before preparing lighting.");
            string path = Root + "Prefabs/Environment.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root,path); AssetDatabase.SaveAssets(); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "Prepared cold skylight, local lantern light, fixed snow/ice normals and four soft ground contacts.";
        }

        /// <summary>Keeps surface lighting when the original environment preparation command is used again.</summary>
        public static void Configure(GameObject root)
        {
            EnsureSortingLayer();
            var iceMaterial = Material("SurfaceIce","Fishing/Surface Ice Lit");
            var propMaterial = Material("SurfaceProps","Universal Render Pipeline/2D/Sprite-Lit-Default");
            var surface = root.transform.Find("Surface");
            var ice = new[] { surface.Find("Ice Left").GetComponent<SpriteRenderer>(), surface.Find("Ice Right").GetComponent<SpriteRenderer>() };
            foreach (var receiver in ice) { receiver.sharedMaterial = iceMaterial; receiver.sortingLayerName = Layer; }
            foreach (string name in new[] { "Fisherman", "Fisherman/Rod", "Weather Pole", "Anemometer", "Windsock", "Barrel", "Stand", "Lantern" })
            {
                var renderer = surface.Find(name).GetComponent<SpriteRenderer>(); renderer.sortingLayerName = Layer;
                // The lantern artwork contains the emissive flame; its brass and flame retain the supplied painted appearance.
                renderer.sharedMaterial = name == "Lantern" ? AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/SpriteUnlit.mat") : propMaterial;
            }
            var lights = Child(surface,"Lighting");
            // A broad fill avoids Global Light registration conflicts while this prefab and its live scene are both loaded.
            var ambient = Light(lights,"Ambient Sky",Light2D.LightType.Point,new Vector2(0,4),new Color(.89f,.94f,1),.80f,40,0);
            ambient.pointLightInnerRadius = 38;
            // Enabling any 2D light makes default Sprite Lit objects require a matching light too.
            // Keep dynamically-created branch art and existing default-layer sprites at their original brightness.
            var neutral = Light(lights,"Default Sprite Fill",Light2D.LightType.Point,new Vector2(0,-55),Color.white,1,160,0,"Default");
            neutral.pointLightInnerRadius = 150;
            var sky = Light(lights,"Sky Key",Light2D.LightType.Point,new Vector2(-3.8f,5.7f),new Color(.80f,.91f,1),.42f,18,5);
            var lamp = Light(lights,"Lantern Warmth",Light2D.LightType.Point,new Vector2(1.65f,1.82f),new Color(1,.64f,.30f),.70f,2.8f,1.1f);
            var presentation = root.GetComponent<SurfaceLighting>(); if (presentation == null) presentation = root.AddComponent<SurfaceLighting>();
            presentation.Bind(ice,sky,lamp,
                new[] { surface.Find("Fisherman"), surface.Find("Stand"), surface.Find("Barrel"), surface.Find("Weather Pole") },
                new[] { new Vector2(1.05f,.15f), new Vector2(.62f,.10f), new Vector2(.53f,.12f), new Vector2(.20f,.075f) });
        }

        private static Light2D Light(Transform parent, string name, Light2D.LightType type, Vector2 position, Color color, float intensity, float radius, float normalDistance, string layerName = Layer)
        {
            var transform = Child(parent,name); transform.localPosition = position;
            var light = transform.GetComponent<Light2D>(); if (light == null) light = transform.gameObject.AddComponent<Light2D>();
            light.lightType = type; light.color = color; light.intensity = intensity;
            light.blendStyleIndex = 0; light.pointLightInnerRadius = .12f; light.pointLightOuterRadius = radius; light.falloffIntensity = .5f;
            var serialized = new SerializedObject(light);
            var layers = serialized.FindProperty("m_ApplyToSortingLayers"); layers.arraySize = 1; layers.GetArrayElementAtIndex(0).intValue = SortingLayer.NameToID(layerName);
            serialized.FindProperty("m_NormalMapQuality").intValue = (int)(normalDistance == 0 ? Light2D.NormalMapQuality.Disabled : Light2D.NormalMapQuality.Accurate);
            serialized.FindProperty("m_NormalMapDistance").floatValue = normalDistance;
            serialized.FindProperty("m_LightVolumeIntensityEnabled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return light;
        }

        private static Material Material(string name, string shaderName)
        {
            string path = Root+"Art/Materials/"+name+".mat";
            var shader = Shader.Find(shaderName); if (shader == null) throw new InvalidOperationException("Missing shader: "+shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
            else if (material.shader != shader) { material.shader = shader; EditorUtility.SetDirty(material); }
            return material;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name); if (child != null) return child;
            child = new GameObject(name).transform; child.SetParent(parent,false); return child;
        }

        private static void EnsureSortingLayer()
        {
            if (SortingLayer.NameToID(Layer) != 0) return;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("m_SortingLayers"); int index = layers.arraySize;
            layers.InsertArrayElementAtIndex(index); var layer = layers.GetArrayElementAtIndex(index);
            layer.FindPropertyRelative("name").stringValue = Layer;
            layer.FindPropertyRelative("uniqueID").intValue = 1735264502;
            layer.FindPropertyRelative("locked").boolValue = false;
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
