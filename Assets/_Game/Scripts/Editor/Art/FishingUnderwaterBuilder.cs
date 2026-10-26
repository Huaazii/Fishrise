using System;
using Fishing.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Adds 2D water atmosphere to the shared environment without rebuilding scenes or gameplay assets.</summary>
    public static class FishingUnderwaterBuilder
    {
        private const string Root = "Assets/_Game/";
        private const string IceLayer = "Underwater Ice";
        /// <summary>Updates the existing environment prefab in place, preserving GUIDs and child references.</summary>
        [MenuItem("Fishing/Prepare underwater atmosphere")]
        public static string Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing water.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before preparing water.");
            string path = Root + "Prefabs/Environment.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); AssetDatabase.SaveAssets(); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "Shared Environment updated: three imported ice silhouettes, recessed walls, sparse outcrops and water optics.";
        }

        /// <summary>Preserves the water setup when the existing environment preparation command is used again.</summary>
        public static void Configure(GameObject root)
        {
            var water = root.transform.Find("Underwater/Water").GetComponent<SpriteRenderer>();
            // The continuous water already covers the full viewport; the old opaque strips create visible seams.
            foreach (string name in new[] { "Left Backing", "Right Backing" })
                root.transform.Find("Underwater/" + name).GetComponent<SpriteRenderer>().enabled = false;
            var walls = root.transform.Find("Underwater/Walls").GetComponentsInChildren<SpriteRenderer>();
            ConfigureRefractionCapture();
            var baseIce = FishingCoreArtImporter.Import("IceWalls/IceWall_A_Base_Left", "Environment", 14.4f, true, Vector2.one * .5f);
            var outcrop = FishingCoreArtImporter.Import("IceWalls/IceWall_B_Outcrop_Left", "Environment", 4.2f, true, Vector2.one * .5f);
            var shoulder = FishingCoreArtImporter.Import("IceWalls/IceWall_C_Shoulder_Left", "Environment", 7.8f, true, Vector2.one * .5f);
            ArrangeWalls(root.transform.Find("Underwater/Walls"), baseIce, outcrop, shoulder);
            var flow = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Textures/Water/WaterSmooth.png");
            var frames = new Texture2D[32];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Textures/caustics/caust" + i.ToString("00") + ".png");
                if (frames[i] == null) throw new InvalidOperationException("Missing imported caustic frame " + i);
            }
            if (flow == null) throw new InvalidOperationException("Missing imported broad-flow normal.");
            var background = Material("WaterDepth", "Fishing/Water Depth");
            var ice = Material("IceCaustics", "Fishing/Ice Caustics");
            background.SetTexture("_FlowTex", flow);
            background.SetFloat("_DepthRange", -root.transform.Find("Underwater/Seabed").GetComponent<SpriteRenderer>().bounds.max.y - 3);
            // The visible profile waterline sits below the snow cap; the rope guide remains a separate gameplay anchor.
            background.SetFloat("_SurfaceY", .86f);
            ice.SetFloat("_SurfaceY", background.GetFloat("_SurfaceY"));
            ice.SetFloat("_BottomY", root.transform.Find("Underwater/Seabed").GetComponent<SpriteRenderer>().bounds.max.y);
            ice.SetTexture("_FlowTex", flow);
            ice.SetTexture("_CausticA", frames[0]); ice.SetTexture("_CausticB", frames[1]);
            water.sharedMaterial = background;
            foreach (var wall in walls) wall.sharedMaterial = ice;
            var atmosphere = root.GetComponent<UnderwaterAtmosphere>();
            if (atmosphere == null) atmosphere = root.AddComponent<UnderwaterAtmosphere>();
            atmosphere.Bind(water, walls, frames);
            EditorUtility.SetDirty(background); EditorUtility.SetDirty(ice);
        }

        private static void ArrangeWalls(Transform parent, Sprite baseIce, Sprite outcrop, Sprite shoulder)
        {
            // Reuse each original group of three objects as two recessed faces and one sparse foreground accent.
            // Different side phases avoid mirrored landmarks; all silhouettes stay outside the playable route.
            for (int side = 0; side < 2; side++)
                for (int i = 0; ; i++)
                {
                    var child = parent.Find((side == 0 ? "Left " : "Right ") + i.ToString("00"));
                    if (child == null) break;
                    var wall = child.GetComponent<SpriteRenderer>();
                    int group = i / 3;
                    bool accent = i % 3 == 1;
                    bool broad = (group + side) % 2 == 0;
                    wall.sprite = accent ? (broad ? outcrop : shoulder) : baseIce;
                    float phase = i * 1.91f + side * 2.37f;
                    float variation = .5f + .5f * Mathf.Sin(phase);
                    float inner = accent ? 4.72f + .35f * variation :
                        5.7f + .5f * Mathf.Sin(group * .9f + side * 2.4f) + .15f * Mathf.Sin(phase);
                    float top = -.55f - group * 14.7f;
                    if (accent) top += -4.5f - side * 1.9f + Mathf.Sin(phase) * 1.5f;
                    else if (i % 3 == 2) top += -7.35f + Mathf.Sin(phase) * .6f;
                    else if (i > 0) top += Mathf.Sin(phase) * .4f;
                    if (i == 0) inner = 5.1f + side * .15f;
                    float scale = .94f + .14f * variation;
                    child.localScale = new Vector3(scale, scale, 1);
                    child.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase * .73f) * (accent ? 4 : 1.5f));
                    wall.flipX = side == 1;
                    wall.flipY = !accent && i > 0 && Mathf.Sin(phase * 1.13f) > .25f;
                    child.localPosition = Vector3.zero;
                    var extent = wall.bounds.extents;
                    child.localPosition = new Vector3((side == 0 ? -1 : 1) * (inner + extent.x), top - extent.y, 0);
                    wall.color = accent ? new Color(.7f + variation * .08f, .88f + variation * .04f, 1, .95f) :
                        new Color(.52f + variation * .07f, .75f + variation * .04f, .92f, .83f);
                    wall.sortingOrder = accent ? 7 : 6;
                    wall.sortingLayerName = IceLayer;
                }
        }

        private static void ConfigureRefractionCapture()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("m_SortingLayers");
            bool exists = false;
            for (int i = 0; i < layers.arraySize; i++)
                exists |= layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == IceLayer;
            if (!exists)
            {
                int index = layers.arraySize;
                layers.InsertArrayElementAtIndex(index);
                var layer = layers.GetArrayElementAtIndex(index);
                layer.FindPropertyRelative("name").stringValue = IceLayer;
                layer.FindPropertyRelative("uniqueID").intValue = 187410297;
                layer.FindPropertyRelative("locked").boolValue = false;
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
            // URP 2D captures Default once, before ice. No extra camera or recursive ice sampling.
            var renderer = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.Renderer2DData>("Assets/Settings/Renderer2D.asset"));
            renderer.FindProperty("m_UseCameraSortingLayersTexture").boolValue = true;
            renderer.FindProperty("m_CameraSortingLayersTextureBound").intValue = SortingLayer.NameToID("Default");
            renderer.FindProperty("m_CameraSortingLayerDownsamplingMethod").intValue =
                (int)UnityEngine.Rendering.Universal.Downsampling._2xBilinear;
            renderer.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Material(string name, string shaderName)
        {
            string path = Root + "Art/Materials/Underwater/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing water shader " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            return material;
        }
    }
}
