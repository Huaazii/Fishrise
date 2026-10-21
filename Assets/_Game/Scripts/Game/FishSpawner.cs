using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Depth-table population, moving schools with open passages, and a bounded fish-view pool.</summary>
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
        private sealed class School
        {
            public FishDefinition Fish;
            public int Count, Order;
            public readonly List<int> Rows = new List<int>();
        }
        /// <summary>Generates exact depth-table counts with seeded schools and persistent open passages.</summary>
        public void SpawnRun(FishingConfig config, FishDefinition[] species, float hookWidth)
        {
            Clear();
            var random = new System.Random(config.randomSeed == 0 ? System.Environment.TickCount : config.randomSeed);
            float gap = hookWidth * Mathf.Max(2.5f, config.passageWidthMultiplier);
            int rowOffset = 0;
            foreach (var band in config.depthBands)
            {
                if (band.rows < 1 || band.maxDepth - band.minDepth <= 5 || band.maxDepth > config.maxDepth)
                    throw new InvalidOperationException("Invalid fish depth band.");
                var depths = new float[band.rows];
                for (int row = 0; row < depths.Length; row++)
                    depths[row] = Mathf.Lerp(band.minDepth + 3, band.maxDepth - 2, (row + .5f) / band.rows);
                var schools = new List<School>();
                foreach (var entry in band.population)
                {
                    if (entry.fish == null || Array.IndexOf(species, entry.fish) < 0 || entry.count < 1 || entry.schoolSize < 1)
                        throw new InvalidOperationException("Invalid fish population entry or missing species binding.");
                    for (int remaining = entry.count; remaining > 0; remaining -= entry.schoolSize)
                    {
                        var school = new School { Fish = entry.fish, Count = Mathf.Min(remaining, entry.schoolSize), Order = schools.Count };
                        for (int row = 0; row < depths.Length; row++)
                            if (depths[row] >= entry.fish.minDepth && depths[row] <= entry.fish.maxDepth) school.Rows.Add(row);
                        if (school.Rows.Count == 0) throw new InvalidOperationException("No eligible row for " + entry.fish.speciesId);
                        schools.Add(school);
                    }
                }
                // Reserve restricted-depth species first, so common species cannot occupy all their eligible rows.
                schools.Sort((a, b) => a.Rows.Count != b.Rows.Count ? a.Rows.Count.CompareTo(b.Rows.Count) : a.Order.CompareTo(b.Order));
                var occupancy = new int[band.rows]; var firstSides = new int[band.rows];
                foreach (var school in schools)
                {
                    var candidates = new List<int>(); int least = 2;
                    foreach (int row in school.Rows)
                    {
                        if (occupancy[row] >= 2 || occupancy[row] > least) continue;
                        if (occupancy[row] < least) { candidates.Clear(); least = occupancy[row]; }
                        candidates.Add(row);
                    }
                    if (candidates.Count == 0) throw new InvalidOperationException("Too many schools for the configured depth rows.");
                    int selected = candidates[random.Next(candidates.Count)];
                    int side = occupancy[selected] == 0 ? (random.Next(2) == 0 ? -1 : 1) : -firstSides[selected];
                    firstSides[selected] = side; occupancy[selected]++;
                    float passageX = (rowOffset + selected) % 3 == 0 ? 0 : ((rowOffset + selected) % 3 == 1 ? -.7f : .7f);
                    SpawnSchool(config, school, depths[selected], side, passageX, gap, random);
                }
                rowOffset += band.rows;
            }
        }
        private void SpawnSchool(FishingConfig config, School school, float depth, int side, float passageX, float gap, System.Random random)
        {
            float bodyHalf = school.Fish.sprite != null ? school.Fish.sprite.bounds.extents.x : .625f;
            float width = 2 * Mathf.Max(bodyHalf, Mathf.Abs(school.Fish.headPosition.x) + school.Fish.headRadius) * school.Fish.size;
            float schoolWidth = width * school.Count + .18f * (school.Count - 1);
            float left = side < 0 ? -config.halfWidth + .1f : passageX + gap * .5f;
            float right = side < 0 ? passageX - gap * .5f : config.halfWidth - .1f;
            var leaderRange = new Vector2(left + schoolWidth * .5f, right - schoolWidth * .5f);
            if (leaderRange.x > leaderRange.y) throw new InvalidOperationException("School is wider than its swim lane: " + school.Fish.speciesId);
            float leader = Mathf.Lerp(leaderRange.x, leaderRange.y, (float)random.NextDouble());
            float speed = Mathf.Lerp(school.Fish.swimSpeed, Mathf.Max(school.Fish.swimSpeed, school.Fish.swimSpeedMax), (float)random.NextDouble());
            float direction = random.Next(2) == 0 ? -1 : 1;
            for (int i = 0; i < school.Count; i++)
            {
                float offset = (i - (school.Count - 1) * .5f) * (width + .18f);
                SpawnOne(school.Fish, new Vector2(leader + offset, -depth * config.unitsPerMetre), direction,
                    config.halfWidth, speed, leaderRange + Vector2.one * offset);
            }
        }
        /// <summary>Creates a fresh spawn ID even when the underlying view is reused.</summary>
        public FishActor SpawnOne(FishDefinition species, Vector2 position, float direction, float halfWidth,
            float swimSpeed = -1, Vector2? swimRange = null)
        {
            FishActor fish;
            if (_pool.Count > 0) fish = _pool.Dequeue();
            else { fish = UnityEngine.Object.Instantiate(_prefab, _poolRoot); AllocatedCount++; }
            fish.Spawn(++_nextSpawnId, species, position, direction, halfWidth, swimSpeed, swimRange);
            // Keep active and pooled views under the scene owner so destroying it cleans up both.
            fish.transform.SetParent(_poolRoot, true);
            _spawned.Add(fish); _active.Add(fish.InstanceId, fish); return fish;
        }
        /// <summary>Advances swimmers and captured hang animations with the same paused-aware delta.</summary>
        public void Tick(float dt)
        { foreach (var fish in _spawned) if (fish.gameObject.activeSelf) fish.Tick(dt); }
        /// <summary>Looks up a live spawn without removing it from active ownership.</summary>
        public bool TryGet(int id, out FishActor fish) => _active.TryGetValue(id, out fish);
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
