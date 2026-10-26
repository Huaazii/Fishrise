using System;
using Fishing.Game;
using UnityEditor;
using UnityEngine;

namespace Fishing.Editor
{
    /// <summary>Assigns a separate nylon material to the original main-line renderer without changing detection.</summary>
    public static class FishingLineBuilder
    {
        /// <summary>Updates the existing Hook bindings and creates shared line/branch materials if absent.</summary>
        [MenuItem("Fishing/Prepare main fishing line")]
        public static string Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing the fishing line.");
            const string path="Assets/_Game/Prefabs/Hook.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root,path); AssetDatabase.SaveAssets(); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "Prepared separate nylon shading and retained the original effect material, hook geometry and prefab identities.";
        }
        /// <summary>Keeps the separate main-line material when existing hook art is prepared again.</summary>
        public static void Configure(GameObject root)
        {
            const string path="Assets/_Game/Art/Materials/FishingLine.mat";
            var shader=Shader.Find("Fishing/Fishing Line"); if(shader==null) throw new InvalidOperationException("Missing fishing-line shader.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
            var line=root.transform.Find("Rope").GetComponent<LineRenderer>();
            root.GetComponent<HookMotor>().BindEffectLineMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/SpriteUnlit.mat"));
            const string metalPath="Assets/_Game/Art/Materials/HookMetal.mat";
            var metal=AssetDatabase.LoadAssetAtPath<Material>(metalPath);
            if(metal==null)
            {
                var metalShader=Shader.Find("Fishing/Hook Metal");
                if(metalShader==null) throw new InvalidOperationException("Missing hook-metal shader.");
                metal=new Material(metalShader); AssetDatabase.CreateAsset(metal,metalPath);
            }
            root.GetComponent<HookMotor>().BindBranchMetalMaterial(metal);
            line.sharedMaterial=material; line.generateLightingData=true;
            line.startColor=line.endColor=Color.white; line.numCornerVertices=2; line.numCapVertices=2;
        }
    }
}
