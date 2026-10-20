using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Species configuration and Unity art references.</summary>
    [CreateAssetMenu(menuName = "Fishing/Fish Definition")]
    public sealed class FishDefinition : ScriptableObject
    {
        public string speciesId = "blue";
        public string displayName = "Bluefish";
        [Min(0)] public int gold = 10;
        [Min(0)] public int shells = 10;
        public bool isSmall;
        public bool isCatchable = true;
        [Min(.1f)] public float size = 1;
        [Min(0)] public float swimSpeed = .5f;
        public Color color = Color.cyan;
        public Sprite sprite;
        public Vector2 headPosition = new Vector2(.38f, 0);
        [Min(.01f)] public float headRadius = .2f;
        public Vector2 mouthPosition = new Vector2(.55f, 0);
        /// <summary>Creates a pure-data snapshot, retaining no reference to this asset.</summary>
        public FishRules Snapshot() => new FishRules(speciesId, displayName, gold, shells, isSmall, isCatchable);
    }
}
