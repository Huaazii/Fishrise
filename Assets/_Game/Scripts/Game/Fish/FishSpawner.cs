using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Seeded, clustered depth-table population with clear passages and species-specific view pools.</summary>
    public sealed class FishSpawner
    {
        private readonly Transform _poolRoot;
        private readonly Dictionary<FishDefinition, Queue<FishActor>> _pools = new Dictionary<FishDefinition, Queue<FishActor>>();
        private readonly List<FishActor> _spawned = new List<FishActor>();
        private readonly Dictionary<int, FishActor> _active = new Dictionary<int, FishActor>();
        private int _nextSpawnId;
        private float _passageOffset, _passagePhase;
        /// <summary>Actual population seed, retained so a randomly generated run can be reproduced.</summary>
        public int RunSeed { get; private set; }
        /// <summary>All views allocated by this spawner, including cached ones.</summary>
        public int AllocatedCount { get; private set; }
        /// <summary>Fish currently eligible for capture.</summary>
        public int ActiveCount => _active.Count;
        /// <summary>Views owned by the current run, including hanging fish.</summary>
        public IReadOnlyList<FishActor> Spawned => _spawned;
        /// <summary>Creates species-specific pools under the scene owner.</summary>
        public FishSpawner(Transform poolRoot) { _poolRoot = poolRoot; }
        private sealed class School
        {
            public FishDefinition Fish;
            public int Count, Order;
            public float MinDepth, MaxDepth, HalfWidth, HalfHeight;
            public Vector2[] Offsets;
        }
        private sealed class Placement
        {
            public School School;
            public Vector2 Centre, Range;
            public int Side;
        }
        /// <summary>Preserves species budgets while varying group sizes, continuous depths and staggered members.</summary>
        public void SpawnRun(FishingConfig config, FishDefinition[] species, float hookWidth)
        {
            Clear();
            int seed = config.useFixedPopulationSeed ? config.randomSeed : Guid.NewGuid().GetHashCode();
            if (!config.useFixedPopulationSeed && seed == RunSeed) seed = unchecked(seed + 1);
            RunSeed = seed;
            var random = new System.Random(seed);
            _passageOffset = Sample(random, -.25f, .25f); _passagePhase = Sample(random, 0, Mathf.PI * 2);
            float gap = hookWidth * Mathf.Max(2.5f, config.passageWidthMultiplier);
            var placed = new List<Placement>();
            foreach (var band in config.depthBands)
            {
                if (band.rows < 1 || band.maxDepth - band.minDepth <= 5 || band.maxDepth > config.maxDepth)
                    throw new InvalidOperationException("Invalid fish depth band.");
                var schools = new List<School>();
                foreach (var entry in band.population)
                {
                    if (entry.fish == null || Array.IndexOf(species, entry.fish) < 0 || entry.count < 1 || entry.schoolSize < 1)
                        throw new InvalidOperationException("Invalid fish population entry or missing species binding.");
                    for (int remaining = entry.count; remaining > 0;)
                    {
                        int count = random.Next(1, Mathf.Min(remaining, entry.schoolSize) + 1);
                        schools.Add(CreateSchool(entry.fish, count, schools.Count, band, config.unitsPerMetre, random));
                        remaining -= count;
                    }
                }
                // Place restricted-depth species first; sampling never moves a rare fish outside its habitat.
                schools.Sort((a, b) =>
                {
                    int comparison = (a.MaxDepth - a.MinDepth).CompareTo(b.MaxDepth - b.MinDepth);
                    return comparison != 0 ? comparison : a.Order.CompareTo(b.Order);
                });
                foreach (var school in schools)
                {
                    float separation = Mathf.Max(.15f, (band.maxDepth - band.minDepth) * config.unitsPerMetre / band.rows * .06f);
                    var placement = PlaceSchool(config, school, placed, gap, hookWidth, separation, random);
                    placed.Add(placement); SpawnSchool(config, placement, placed.Count, random);
                }
            }
        }
        private static School CreateSchool(FishDefinition fish, int count, int order, FishDepthBand band, float units, System.Random random)
        {
            float halfWidth = Mathf.Max((fish.sprite != null ? fish.sprite.bounds.extents.x : .625f) * fish.size, fish.prefab.HeadHorizontalExtent);
            var head = fish.prefab.Head;
            float headHeight = Mathf.Abs(head.transform.TransformPoint(head.offset).y - fish.prefab.transform.position.y) +
                head.radius * Mathf.Max(Mathf.Abs(head.transform.lossyScale.x), Mathf.Abs(head.transform.lossyScale.y));
            float halfHeight = Mathf.Max((fish.sprite != null ? fish.sprite.bounds.extents.y : .4f) * fish.size, headHeight);
            var school = new School { Fish = fish, Count = count, Order = order, Offsets = new Vector2[count] };
            float spread = 0;
            for (int i = 0; i < count; i++)
            {
                // Stagger actual fish positions, keeping the head collider attached to the art.
                var offset = new Vector2((i - (count - 1) * .5f) * (halfWidth * 2 + .12f), count > 1 ? Sample(random, -.65f, .65f) : 0);
                school.Offsets[i] = offset; spread = Mathf.Max(spread, Mathf.Abs(offset.y));
                school.HalfWidth = Mathf.Max(school.HalfWidth, Mathf.Abs(offset.x) + halfWidth);
                school.HalfHeight = Mathf.Max(school.HalfHeight, Mathf.Abs(offset.y) + halfHeight);
            }
            school.MinDepth = Mathf.Max(band.minDepth + 2, fish.minDepth) + spread / units;
            school.MaxDepth = Mathf.Min(band.maxDepth - 2, fish.maxDepth) - spread / units;
            if (school.MinDepth > school.MaxDepth) throw new InvalidOperationException("No habitat for " + fish.speciesId);
            return school;
        }
        private Placement PlaceSchool(FishingConfig config, School school, List<Placement> placed, float gap, float hookWidth, float separation, System.Random random)
        {
            Placement best = null; float bestClearance = float.NegativeInfinity;
            for (int attempt = 0; attempt < 96; attempt++)
            {
                float depth = Sample(random, school.MinDepth, school.MaxDepth);
                float passage = _passageOffset + .45f * Mathf.Sin(depth * .06f + _passagePhase);
                // Reserve the curved passage over the whole group's vertical reach, not just its centre line.
                float margin = .08f + .027f * (school.HalfHeight + hookWidth) / config.unitsPerMetre;
                int side = random.Next(2) == 0 ? -1 : 1;
                float left = side < 0 ? -config.halfWidth + .1f : passage + gap * .5f + margin;
                float right = side < 0 ? passage - gap * .5f - margin : config.halfWidth - .1f;
                var range = new Vector2(left + school.HalfWidth, right - school.HalfWidth);
                if (range.x > range.y) continue;
                var candidate = new Placement { School = school, Side = side, Range = range,
                    Centre = new Vector2(Sample(random, range.x, range.y), -depth * config.unitsPerMetre) };
                float clearance = float.PositiveInfinity;
                foreach (var other in placed)
                    if (side == other.Side) clearance = Mathf.Min(clearance,
                        Mathf.Abs(candidate.Centre.y - other.Centre.y) - school.HalfHeight - other.School.HalfHeight);
                if (best == null || clearance > bestClearance) { best = candidate; bestClearance = clearance; }
                if (clearance >= separation) return candidate;
            }
            if (best == null || bestClearance < .05f) throw new InvalidOperationException("Population does not fit its swim lanes: " + school.Fish.speciesId);
            return best;
        }
        private void SpawnSchool(FishingConfig config, Placement placement, int schoolId, System.Random random)
        {
            var school = placement.School;
            float speed = Mathf.Lerp(school.Fish.swimSpeed, Mathf.Max(school.Fish.swimSpeed, school.Fish.swimSpeedMax), (float)random.NextDouble());
            float direction = random.Next(2) == 0 ? -1 : 1;
            for (int i = 0; i < school.Count; i++)
            {
                Vector2 offset = school.Offsets[i];
                var fish = SpawnOne(school.Fish, placement.Centre + offset, direction,
                    config.halfWidth, speed, placement.Range + Vector2.one * offset.x);
                fish.SchoolId = schoolId;
            }
        }
        private static float Sample(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        /// <summary>Creates a fresh spawn ID even when the underlying view is reused.</summary>
        public FishActor SpawnOne(FishDefinition species, Vector2 position, float direction, float halfWidth,
            float swimSpeed = -1, Vector2? swimRange = null)
        {
            if (species == null || species.prefab == null || species.prefab.Definition != species)
                throw new InvalidOperationException("Missing or mismatched species prefab binding.");
            if (!_pools.TryGetValue(species, out var pool))
            { pool = new Queue<FishActor>(); _pools.Add(species, pool); }
            FishActor fish;
            if (pool.Count > 0) fish = pool.Dequeue();
            else { fish = UnityEngine.Object.Instantiate(species.prefab, _poolRoot); AllocatedCount++; }
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
        /// <summary>Looks up an owned view, including hanging and escaping catches.</summary>
        public bool TryGetOwned(int id, out FishActor fish)
        {
            foreach (var owned in _spawned) if (owned.InstanceId == id) { fish = owned; return true; }
            fish = null; return false;
        }
        /// <summary>Removes a committed lost catch from the hook without returning it to active capture lookup.</summary>
        public bool Escape(int id, out FishActor fish)
        {
            if (!TryGetOwned(id, out fish)) return false;
            fish.Escape(_poolRoot); return true;
        }
        /// <summary>Removes a capture from the active lookup before visual attachment.</summary>
        public bool Take(int id, out FishActor fish)
        {
            if (!_active.TryGetValue(id, out fish)) return false;
            _active.Remove(id); return true;
        }
        /// <summary>Recycles all fish including previously captured views.</summary>
        public void Clear()
        {
            foreach (var fish in _spawned) { fish.Despawn(_poolRoot); _pools[fish.Definition].Enqueue(fish); }
            _spawned.Clear(); _active.Clear();
        }
    }
}
