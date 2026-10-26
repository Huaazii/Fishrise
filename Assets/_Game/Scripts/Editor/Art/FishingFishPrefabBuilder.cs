using System;
using System.IO;
using Fishing.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Editor
{
    /// <summary>Serializes SO-backed species into independent prefabs and migrates the saved scenes.</summary>
    public static class FishingFishPrefabBuilder
    {
        private const string Root = "Assets/_Game/";
        private const string Legacy = Root + "Prefabs/Fish.prefab";

        /// <summary>Rebuilds species presentation without changing existing SO or prefab GUIDs.</summary>
        [MenuItem("Fishing/Refresh species prefabs from SO")]
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before updating fish prefabs.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before updating fish prefabs.");
            var guids = AssetDatabase.FindAssets("t:FishDefinition", new[] { Root + "Configs" });
            if (guids.Length == 0) throw new InvalidOperationException("No fish definitions found.");
            foreach (string guid in guids)
                CreateOrUpdate(AssetDatabase.LoadAssetAtPath<FishDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            AssetDatabase.SaveAssets();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // Saving after deserialization removes the obsolete bootstrap fishPrefab binding.
                foreach (string name in new[] { "Main", "Menu" })
                {
                    var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
                    var bootstrap = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
                    if (bootstrap == null || !bootstrap.HasBindings) throw new InvalidOperationException("Missing bindings in " + name);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            if (AssetDatabase.LoadMainAssetAtPath(Legacy) != null)
            {
                foreach (string path in AssetDatabase.GetAllAssetPaths())
                {
                    string extension = Path.GetExtension(path);
                    if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path == Legacy ||
                        (extension != ".prefab" && extension != ".unity" && extension != ".asset")) continue;
                    if (Array.IndexOf(AssetDatabase.GetDependencies(path, false), Legacy) >= 0)
                        throw new InvalidOperationException("Legacy Fish is still referenced by " + path);
                }
                if (!AssetDatabase.DeleteAsset(Legacy)) throw new IOException("Could not delete legacy Fish prefab.");
            }
            return "Updated " + guids.Length + " independent species prefabs; Main/Menu use SO prefab bindings; legacy Fish removed.";
        }

        /// <summary>Refreshes SO art and bindings, using SO head geometry only when first creating the collider.</summary>
        public static FishActor CreateOrUpdate(FishDefinition definition)
        {
            if (definition == null || definition.sprite == null || string.IsNullOrWhiteSpace(definition.speciesId))
                throw new InvalidOperationException("A species ID and imported sprite are required.");
            string path = Root + "Prefabs/Fish_" + definition.speciesId + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root = existing != null ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Fish_" + definition.speciesId);
            try
            {
                var actor = root.GetComponent<FishActor>();
                if (actor == null) actor = root.AddComponent<FishActor>();
                var body = Child(root.transform, "Body").GetComponent<SpriteRenderer>();
                if (body == null) body = root.transform.Find("Body").gameObject.AddComponent<SpriteRenderer>();
                body.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/Materials/SpriteUnlit.mat");
                var head = Child(root.transform, "Head").GetComponent<CircleCollider2D>();
                if (head == null)
                {
                    head = root.transform.Find("Head").gameObject.AddComponent<CircleCollider2D>();
                    head.transform.localPosition = definition.headPosition;
                    head.offset = Vector2.zero; head.radius = definition.headRadius;
                }
                head.isTrigger = true;
                actor.Bind(definition, body, head, Child(root.transform, "Mouth"));
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                definition.prefab = prefab.GetComponent<FishActor>(); EditorUtility.SetDirty(definition);
                return definition.prefab;
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }
    }
}
