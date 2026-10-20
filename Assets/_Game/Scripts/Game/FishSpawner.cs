using System.Collections.Generic;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Fixed-seed placeholder population and bounded fish-view pool.</summary>
    public sealed class FishSpawner
    {
        private readonly FishActor _prefab;
        private readonly Transform _poolRoot;
        private readonly Queue<FishActor> _pool = new Queue<FishActor>();
        private readonly List<FishActor> _spawned = new List<FishActor>();
        private readonly Dictionary<int, FishActor> _active = new Dictionary<int, FishActor>();
        private int _nextSpawnId;
        /// <summary>All views allocated by this spawner, including cached ones.</summary>
        public int AllocatedCount { get; private set; }
        /// <summary>Fish currently eligible for capture.</summary>
        public int ActiveCount => _active.Count;
        /// <summary>Views owned by the current run, including hanging fish.</summary>
        public IReadOnlyList<FishActor> Spawned => _spawned;
        /// <summary>Creates the pool with an asset-backed prefab.</summary>
        public FishSpawner(FishActor prefab, Transform poolRoot) { _prefab = prefab; _poolRoot = poolRoot; }
        /// <summary>Generates reproducible placeholder lanes; navigability is verified with the playable scene.</summary>
        public void SpawnRun(FishingConfig config, FishDefinition[] species)
        {
            Clear();
            var random = new System.Random(config.randomSeed == 0 ? System.Environment.TickCount : config.randomSeed);
            for (int i = 0; i < config.fishCount; i++)
            {
                // Basic repeating lanes are deliberately replaced by depth-specific layouts in stage 02.
                float depth = 5 + (config.maxDepth - 7) * i / Mathf.Max(1, config.fishCount - 1);
                float x = (i % 3 - 1) * 2.55f + (float)(random.NextDouble() - .5) * .5f;
                SpawnOne(species[i % species.Length], new Vector2(x, -depth * config.unitsPerMetre),
                    random.Next(2) == 0 ? -1 : 1, config.halfWidth);
            }
        }
        /// <summary>Creates a fresh spawn ID even when the underlying view is reused.</summary>
        public FishActor SpawnOne(FishDefinition species, Vector2 position, float direction, float halfWidth)
        {
            FishActor fish;
            if (_pool.Count > 0) fish = _pool.Dequeue();
            else { fish = Object.Instantiate(_prefab, _poolRoot); AllocatedCount++; }
            fish.Spawn(++_nextSpawnId, species, position, direction, halfWidth);
            // Keep active and pooled views under the scene owner so destroying it cleans up both.
            fish.transform.SetParent(_poolRoot, true);
            _spawned.Add(fish); _active.Add(fish.InstanceId, fish); return fish;
        }
        /// <summary>Advances only active fish.</summary>
        public void Tick(float dt)
        { foreach (var fish in _active.Values) fish.Tick(dt); }
        /// <summary>Removes a capture from the active lookup before visual attachment.</summary>
        public bool Take(int id, out FishActor fish)
        {
            if (!_active.TryGetValue(id, out fish)) return false;
            _active.Remove(id); return true;
        }
        /// <summary>Recycles all fish including previously captured views.</summary>
        public void Clear()
        {
            foreach (var fish in _spawned) { fish.Despawn(_poolRoot); _pool.Enqueue(fish); }
            _spawned.Clear(); _active.Clear();
        }
    }
}
