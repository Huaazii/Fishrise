using System;
using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Seeded treasure placement and main-hook swept pickup adapter; Core owns activation and deduplication.</summary>
    public sealed class SkillTreasureRig
    {
        /// <summary>Pulses one sonar activation emits; placement coverage and release spacing use the same train.</summary>
        private const int SonarPulseCount = 3;
        /// <summary>Score for scattering at least one fish with the pickup pulse itself.</summary>
        private const float FirstPulseValue = 120f;
        /// <summary>Score for scattering fish on a later pulse, which still clears the descending route.</summary>
        private const float LaterPulseValue = 40f;
        /// <summary>Score per newly scattered fish, weighted by how likely it is still inside the wave.</summary>
        private const float TargetValue = 10f;
        /// <summary>Penalty per fish already assigned to the other sonar, so two pickups do not clear one school twice.</summary>
        private const float SharedTargetPenalty = 30f;
        /// <summary>Penalty per metre away from the paced sonar depth.</summary>
        private const float DepthMissPenalty = .6f;
        /// <summary>Quiet time after the previous sonar's last wave, before the next pickup can fire.</summary>
        private const float SonarRecoverySeconds = .6f;
        /// <summary>Schools considered per treasure, nearest to the paced depth first.</summary>
        private const int SchoolsPerTreasure = 12;
        /// <summary>Shallow-side offsets in gameplay metres between the school's top fish and the pickup.</summary>
        private static readonly float[] ShallowOffsets = { .35f, .7f, 1f, 1.35f, 1.7f, 2.05f, 2.4f, 2.75f };
        private readonly Transform _root;
        private readonly GameUiAssets _art;
        private readonly Material _spriteMaterial, _lineMaterial;
        private readonly List<SkillTreasureView> _views = new List<SkillTreasureView>();
        private readonly List<SchoolSnapshot> _schools = new List<SchoolSnapshot>();
        private readonly List<Vector2> _sonarBands = new List<Vector2>();
        private readonly HashSet<int> _correctionAttempted = new HashSet<int>();
        private AbilityController _abilities;
        private FishingConfig _config;
        private FishSpawner _fish;
        private Guid _runId;
        private int _count, _seed;
        private float _hookRadius = .35f;
        private float _diveSpeed, _countdownSeconds, _snapshotDepth, _minSonarDepthGap, _sonarDeepest;
        private double _snapshotTime;
        private Vector2 _hookOffset;
        private bool _returnPlaced;
        /// <summary>All allocated, reusable views; active entries keep run-local positive IDs.</summary>
        public IReadOnlyList<SkillTreasureView> Views => _views;
        /// <summary>Successful pickups only, after Core has triggered the skill.</summary>
        public event Action<SkillTreasureKind, Vector3> Collected;
        /// <summary>Sonar pickups anchored beside a live small-fish school this run.</summary>
        public int AnchoredCount { get; private set; }
        /// <summary>Sonar pickups that fell back to far-from-fish sampling because no school could be covered safely.</summary>
        public int FallbackCount { get; private set; }
        /// <summary>One-shot entry checks, including checks without a valid replacement.</summary>
        public int CorrectionAttemptCount => _correctionAttempted.Count;
        /// <summary>Sonar pickups repositioned once from live, unstartled fish geometry.</summary>
        public int CorrectionCount { get; private set; }
        /// <summary>Creates one scene-owned pool with the existing sprite and line materials.</summary>
        public SkillTreasureRig(Transform parent, GameUiAssets art, Material spriteMaterial, Material lineMaterial)
        {
            _root = new GameObject("Skill treasures").transform; _root.SetParent(parent, false);
            _art = art; _spriteMaterial = spriteMaterial; _lineMaterial = lineMaterial;
        }

        /// <summary>Resamples this run from the fish seed without changing fish placement, rules or global random state.</summary>
        public void Bind(RunModel run, AbilityController abilities, FishingConfig config, FishSpawner fish, float hookRadius, Vector2 hookOffset = default)
        {
            _runId = run.RunId; _abilities = abilities; _config = config; _fish = fish; _count = 0;
            _hookRadius = Mathf.Max(.01f, hookRadius); _hookOffset = hookOffset;
            _diveSpeed = Mathf.Max(.01f, run.Rules.DiveSpeed); _countdownSeconds = run.Rules.CountdownSeconds;
            var sonar = abilities.Sonar.Effect;
            float units = Mathf.Max(.01f, config.unitsPerMetre);
            float releaseDepth = _diveSpeed * ((SonarPulseCount - 1) * sonar.PulseIntervalSeconds + sonar.WaveSeconds);
            _minSonarDepthGap = releaseDepth + _diveSpeed * SonarRecoverySeconds + 2 * PickupReach / units;
            // Even a late pickup must finish its final wave before the bottom zone cancels echoes.
            _sonarDeepest = run.Rules.BottomZoneDepth - releaseDepth - (PickupReach + Mathf.Abs(_hookOffset.y)) / units;
            _sonarBands.Clear();
            _snapshotDepth = 0; _snapshotTime = run.GameTime; _correctionAttempted.Clear(); CorrectionCount = 0;
            _seed = fish.RunSeed; _returnPlaced = false; AnchoredCount = FallbackCount = 0;
            foreach (var view in _views) view.gameObject.SetActive(false);
            if (!config.enableSkillTreasures) { _schools.Clear(); return; }
            CaptureSchools(fish);
            // Independent streams: tuning one pickup family must not shift the other's layout.
            AddSonar(run, new System.Random(unchecked(_seed ^ 0x2D734A19)));
            AddBranches(run, new System.Random(unchecked(_seed ^ 0x1B9E57A3)));
        }

        /// <summary>Groups live, unstartled small fish by visual school without touching the spawner or its random stream.</summary>
        private void CaptureSchools(FishSpawner fish)
        {
            _schools.Clear();
            if (fish == null) return;
            foreach (var actor in fish.Spawned)
            {
                if (actor == null || !actor.gameObject.activeSelf || actor.SchoolId <= 0) continue;
                var definition = actor.Definition;
                if (definition == null || !definition.isSmall || !definition.isCatchable) continue;
                if (actor.IsCaptured || actor.IsEscaping || actor.IsScared) continue;
                var head = actor.Head;
                if (head == null || !head.enabled) continue;
                float radius = head.radius * Mathf.Max(Mathf.Abs(head.transform.lossyScale.x), Mathf.Abs(head.transform.lossyScale.y));
                var centre = (Vector2)head.transform.TransformPoint(head.offset);
                float offsetX = Mathf.Abs(centre.x - actor.transform.position.x);
                float direction = Mathf.Sign(actor.transform.localScale.x);
                var school = FindSchool(actor.SchoolId);
                school.Fish.Add(new SmallFish
                {
                    Id = actor.InstanceId, Centre = centre, Radius = radius,
                    SwimMin = actor.SwimRange.x - offsetX, SwimMax = actor.SwimRange.y + offsetX,
                    RootX = actor.transform.position.x, RootMin = actor.SwimRange.x, RootMax = actor.SwimRange.y,
                    Speed = actor.SwimSpeed, Direction = direction, HeadOffsetX = (centre.x - actor.transform.position.x) * direction
                });
                school.MaxRadius = Mathf.Max(school.MaxRadius, radius);
                school.MinX = Mathf.Min(school.MinX, centre.x);
                school.MaxX = Mathf.Max(school.MaxX, centre.x);
                school.MinY = Mathf.Min(school.MinY, centre.y);
                school.MaxY = Mathf.Max(school.MaxY, centre.y);
            }
        }
        private SchoolSnapshot FindSchool(int schoolId)
        {
            foreach (var school in _schools) if (school.SchoolId == schoolId) return school;
            var created = new SchoolSnapshot
            {
                SchoolId = schoolId, MinX = float.MaxValue, MaxX = float.MinValue,
                MinY = float.MaxValue, MaxY = float.MinValue
            };
            _schools.Add(created); return created;
        }

        /// <summary>Places one sonar in each equal route segment, with separated pulse trains and a safe bottom deadline.</summary>
        private void AddSonar(RunModel run, System.Random random)
        {
            int count = Mathf.Max(0, _abilities.Sonar.Rules.Uses);
            if (count == 0) return;
            float deepest = Mathf.Min(_config.sonarTreasureDepths.y, _sonarDeepest);
            for (int i = 0; i < count; i++)
                _sonarBands.Add(new Vector2(
                    Mathf.Max(.5f, Mathf.Max(_config.sonarTreasureDepths.x, run.Rules.MaxDepth * i / count)),
                    Mathf.Min(deepest, run.Rules.MaxDepth * (i + 1) / count)));
            // Reserve backwards within the real route segments, not halves of the bottom-clipped range.
            float latest = deepest;
            for (int i = count - 1; i >= 0; i--)
            {
                var band = _sonarBands[i]; band.y = Mathf.Min(band.y, latest);
                if (band.x > band.y)
                    throw new InvalidOperationException("The sonar range cannot fit every route segment and its separated pulse train.");
                _sonarBands[i] = band; latest = band.y - _minSonarDepthGap;
            }
            float units = Mathf.Max(.01f, _config.unitsPerMetre), previousDepth = 0;
            var claimed = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                var band = _sonarBands[i];
                float bandMin = i == 0 ? band.x : Mathf.Max(band.x, previousDepth + _minSonarDepthGap);
                float bandMax = band.y;
                float target = Mathf.Lerp(bandMin, bandMax, .2f + .6f * (float)random.NextDouble());
                Vector2 position;
                if (TryPlaceNearSchool(run, target, bandMin, bandMax, claimed, out position))
                {
                    AnchoredCount++;
                }
                else
                {
                    position = SamplePosition(0, 1, _count, new Vector2(bandMin, bandMax), deepest, random, _fish);
                    FallbackCount++;
                    var hits = new List<int>();
                    ScorePulses(position, _abilities.Sonar.Effect.Radius * units,
                        run.Rules.DiveSpeed * _abilities.Sonar.Effect.PulseIntervalSeconds * units,
                        units, target, claimed, hits, out _);
                    foreach (int id in hits) claimed.Add(id);
                }
                previousDepth = -position.y / units;
                Append(SkillTreasureKind.Sonar, position);
            }
        }
        private void AddBranches(RunModel run, System.Random random)
        {
            int count = Mathf.Max(0, _abilities.Branches.Rules.Uses);
            float deepest = Mathf.Max(.5f, run.Rules.BottomZoneDepth - 6);
            for (int i = 0; i < count; i++)
                Append(SkillTreasureKind.Branches,
                    SamplePosition(i, count, _count, _config.branchTreasureDepths, deepest, random, _fish));
        }
        private void Append(SkillTreasureKind kind, Vector2 position)
        {
            if (_count == _views.Count)
            {
                var go = new GameObject("Skill treasure " + (_count + 1)); go.transform.SetParent(_root, false);
                var view = go.AddComponent<SkillTreasureView>(); view.Initialize(_art, _spriteMaterial, _lineMaterial); _views.Add(view);
            }
            _views[_count].ResetView(_count + 1, kind, position, _config.treasureVisualSize); _count++;
        }

        /// <summary>Scores school-adjacent positions inside the reserved depth band and keeps the best safe one.</summary>
        private bool TryPlaceNearSchool(RunModel run, float targetDepth, float minDepth, float maxDepth,
            HashSet<int> claimed, out Vector2 position)
        {
            position = default;
            if (_schools.Count == 0) return false;
            float units = Mathf.Max(.01f, _config.unitsPerMetre);
            float radius = _abilities.Sonar.Effect.Radius * units;
            float pulseStep = run.Rules.DiveSpeed * _abilities.Sonar.Effect.PulseIntervalSeconds * units;
            float limit = Mathf.Max(0, _config.halfWidth - _config.treasureVisualSize * .75f);
            // Keep the straight centre descent clear so the pickup stays a deliberate choice.
            float laneMin = _config.treasurePickupRadius + _hookRadius + .15f;
            if (laneMin > limit) return false;
            var order = new List<SchoolSnapshot>(_schools);
            order.Sort((a, b) =>
            {
                int depth = Mathf.Abs(ShallowDepth(a, units) - targetDepth).CompareTo(Mathf.Abs(ShallowDepth(b, units) - targetDepth));
                return depth != 0 ? depth : a.SchoolId.CompareTo(b.SchoolId);
            });
            float best = 0; bool bestCoversFirst = false; var bestPoint = Vector2.zero; List<int> bestHits = null;
            int examined = Mathf.Min(SchoolsPerTreasure, order.Count);
            for (int s = 0; s < examined; s++)
            {
                var school = order[s];
                float gap = _config.treasurePickupRadius + school.MaxRadius + .1f;
                bool left = (school.MinX + school.MaxX) * .5f < 0;
                for (int side = 0; side < 10; side++)
                {
                    // Side zero faces the passage: reachable without hugging the ice wall.
                    float x = left
                        ? (side == 0 ? school.MaxX + gap : school.MinX - gap)
                        : (side == 0 ? school.MinX - gap : school.MaxX + gap);
                    // The upper edge can cover a whole patrol lane without overlapping the school.
                    if (side == 1) x = (school.Fish[0].SwimMin + school.Fish[0].SwimMax) * .5f;
                    if (side >= 3) x = Mathf.Lerp(school.Fish[0].SwimMin, school.Fish[0].SwimMax, (side - 3) / 6f);
                    if (Mathf.Abs(x - _hookOffset.x) < laneMin || Mathf.Abs(x) > limit) continue;
                    float previousCandidateDepth = float.NaN;
                    for (int o = 0; o < ShallowOffsets.Length; o++)
                    {
                        // A spacing boundary can still cover this school on the first or a later pulse.
                        // Keep it inside the allowed band, then let real clearance and pulse coverage decide.
                        float depth = Mathf.Clamp(-(school.MaxY + ShallowOffsets[o] * units) / units, minDepth, maxDepth);
                        if (depth == previousCandidateDepth) continue;
                        previousCandidateDepth = depth;
                        var point = new Vector2(x, -depth * units);
                        if (Clearance(point, _fish) <= RequiredClearance) continue;
                        var hits = new List<int>();
                        float score = ScorePulses(point, radius, pulseStep, units, targetDepth, claimed, hits, out bool coveredFirst);
                        if (bestCoversFirst && !coveredFirst) continue;
                        if (coveredFirst == bestCoversFirst && score <= best) continue;
                        best = score; bestCoversFirst = coveredFirst; bestPoint = point; bestHits = hits;
                    }
                }
            }
            // No covered fish means a silent pickup: keep the old sampling and record the fallback instead.
            if (bestHits == null || bestHits.Count == 0) return false;
            position = bestPoint;
            foreach (var id in bestHits) claimed.Add(id);
            return true;
        }

        /// <summary>Rewards pulses that would scatter new fish, then prefers the paced depth within the safe band.</summary>
        private float ScorePulses(Vector2 point, float radius, float pulseStep, float units,
            float targetDepth, HashSet<int> claimed, List<int> hits, out bool coveredFirst)
        {
            coveredFirst = false;
            float pickupReach = PickupReach;
            float score = 0; var counted = new HashSet<int>();
            for (int pulse = 0; pulse < SonarPulseCount; pulse++)
            {
                var centre = point - _hookOffset - Vector2.up * (pulse * pulseStep);
                float arrival = _countdownSeconds + Mathf.Max(0, (-centre.y / units - _snapshotDepth) / _diveSpeed);
                float fresh = 0;
                foreach (var school in _schools)
                    foreach (var fish in school.Fish)
                    {
                        if (counted.Contains(fish.Id)) continue;
                        float weight = PulseWeight(centre, fish, radius, pickupReach, arrival, units * _diveSpeed);
                        if (weight <= 0) continue;
                        if (ThreatenedBeforePickup(fish, arrival, radius, pickupReach, units)) continue;
                        counted.Add(fish.Id); hits.Add(fish.Id);
                        if (claimed.Contains(fish.Id)) continue;
                        fresh += weight;
                        if (pulse == 0) coveredFirst = true;
                    }
                if (fresh > 0) score += (pulse == 0 ? FirstPulseValue : LaterPulseValue) + fresh * TargetValue;
            }
            foreach (var id in hits) if (claimed.Contains(id)) score -= SharedTargetPenalty;
            float depth = -point.y / units;
            score -= DepthMissPenalty * Mathf.Abs(depth - targetDepth);
            return score;
        }

        private bool ThreatenedBeforePickup(SmallFish fish, float arrival, float radius, float pickupReach, float units)
        {
            foreach (var echo in _abilities.Sonar.PendingPulses)
            {
                float delay = (float)(echo.GameTime - _snapshotTime);
                if (delay < 0 || delay > arrival + pickupReach / (units * _diveSpeed)) continue;
                // A pending earlier activation follows the same descending hook. Reject the whole
                // vertical band, independent of steering, rather than count fish it may startle first.
                float echoY = -(_snapshotDepth + delay * _diveSpeed) * units;
                if (Mathf.Abs(echoY - fish.Centre.y) <= radius + .06f) return true;
            }
            return false;
        }

        /// <summary>Prefers full-lane coverage, otherwise bounds the predicted patrol at the real descent time.</summary>
        private static float PulseWeight(Vector2 centre, SmallFish fish, float radius, float pickupReach, float arrival, float diveSpeed)
        {
            float dy = Mathf.Abs(centre.y - fish.Centre.y);
            float farX = Mathf.Max(Mathf.Abs(centre.x - fish.SwimMin), Mathf.Abs(centre.x - fish.SwimMax));
            if (Mathf.Sqrt(farX * farX + dy * dy) + pickupReach <= radius) return 2;
            float length = fish.RootMax - fish.RootMin;
            if (length <= .001f) return 0;
            float phase = fish.RootX - fish.RootMin;
            if (fish.Direction < 0) phase = 2 * length - phase;
            phase = Mathf.Repeat(phase + fish.Speed * arrival, 2 * length);
            float rootX = fish.RootMin + (phase <= length ? phase : 2 * length - phase);
            // Tick clamps at turns instead of preserving overshoot. Bound that lost distance
            // per possible turn, and the arrival-time shift across the whole pickup disk.
            float error = fish.Speed * (Time.fixedDeltaTime * (Mathf.Floor(fish.Speed * arrival / length) + 2) + pickupReach / diveSpeed);
            float headOffset = fish.HeadOffsetX * (phase < length ? 1 : -1);
            float low = Mathf.Max(fish.RootMin, rootX - error) + headOffset;
            float high = Mathf.Min(fish.RootMax, rootX + error) + headOffset;
            if (Mathf.Min(rootX - fish.RootMin, fish.RootMax - rootX) <= error)
            { low = Mathf.Max(fish.RootMin, rootX - error) - Mathf.Abs(fish.HeadOffsetX); high = Mathf.Min(fish.RootMax, rootX + error) + Mathf.Abs(fish.HeadOffsetX); }
            farX = Mathf.Max(Mathf.Abs(centre.x - low), Mathf.Abs(centre.x - high));
            return Mathf.Sqrt(farX * farX + dy * dy) + pickupReach <= radius ? 1 : 0;
        }
        private static float ShallowDepth(SchoolSnapshot school, float units) => -school.MaxY / units;
        // A pickup can occur at either vertical edge of the hook/pickup disk and at either bobbing extreme.
        // Reserve both envelopes so centre spacing guarantees quiet time even for late/early contacts.
        private float PickupReach => _config.treasurePickupRadius + _hookRadius + SkillTreasureView.BobAmplitude + .06f;


        // The pickup disk has radius pickup + hook. Reserve the hook's own radius again,
        // plus bobbing, so every hook circle inside that disk is initially clear of fish heads.
        private float RequiredClearance => _config.treasurePickupRadius + 2 * _hookRadius + SkillTreasureView.BobAmplitude + .06f;

        private Vector2 SamplePosition(int index, int count, int slot, Vector2 depths, float deepest, System.Random random, FishSpawner fish)
        {
            float min = Mathf.Clamp(depths.x, Mathf.Min(.5f, deepest), deepest), max = Mathf.Clamp(depths.y, min, deepest);
            float depth = Mathf.Lerp(min, max, (index + .2f + .6f * (float)random.NextDouble()) / count);
            float limit = Mathf.Max(0, _config.halfWidth - _config.treasureVisualSize * .75f);
            Vector2 best = new Vector2(0, -depth * _config.unitsPerMetre); float bestClearance = float.NegativeInfinity;
            for (int attempt = 0; attempt < 48; attempt++)
            {
                float side = (slot + attempt) % 2 == 0 ? -1 : 1;
                float laneMin = _config.treasurePickupRadius + _hookRadius + Mathf.Abs(_hookOffset.x) + .15f;
                float x = side * Mathf.Lerp(Mathf.Min(laneMin, limit), limit, (float)random.NextDouble());
                var point = new Vector2(x, -depth * _config.unitsPerMetre);
                float clearance = Clearance(point, fish);
                if (clearance > bestClearance) { best = point; bestClearance = clearance; }
                if (clearance > RequiredClearance) break;
            }
            if (bestClearance > RequiredClearance) return best;
            // Exhaust the depth band deterministically instead of returning an unsafe "best" sample.
            for (int row = 0; row <= 16; row++)
                for (int column = 0; column <= 32; column++)
                {
                    float x = Mathf.Lerp(-limit, limit, column / 32f);
                    if (Mathf.Abs(x - _hookOffset.x) < _config.treasurePickupRadius + _hookRadius + .15f) continue;
                    var point = new Vector2(x, -Mathf.Lerp(min, max, row / 16f) * _config.unitsPerMetre);
                    if (Clearance(point, fish) > RequiredClearance) return point;
                }
            throw new InvalidOperationException("No safe skill treasure placement inside its configured depth band.");
        }
        private void PlaceOnReturn(RunModel run)
        {
            if (_returnPlaced || run.State != RunState.Ascending) return;
            _returnPlaced = true;
            // Early returns still get a reachable choice ahead, away from the turn point.
            float deepest = run.Depth > 4.5f ? run.Depth - 4 : run.Depth * .5f;
            var depths = new Vector2(Mathf.Min(_config.branchTreasureDepths.x, deepest * .7f),
                Mathf.Min(_config.branchTreasureDepths.y, deepest));
            var random = new System.Random(unchecked(_seed ^ 0x573A2C11));
            int branchIndex = 0;
            for (int i = 0; i < _count; i++)
            {
                var view = _views[i]; if (view.Kind != SkillTreasureKind.Branches) continue;
                var position = SamplePosition(branchIndex++, _abilities.Branches.Rules.Uses, i, depths, deepest, random, _fish);
                view.ResetView(view.Id, view.Kind, position, _config.treasureVisualSize);
            }
        }
        private static bool InPhase(SkillTreasureKind kind, RunState state) =>
            kind == SkillTreasureKind.Sonar ? state == RunState.Diving : state == RunState.Ascending;

        private void CorrectOnEntry(RunModel run, Camera camera)
        {
            if (run.State != RunState.Diving || !_abilities.CanCollectTreasure(SkillTreasureKind.Sonar)) return;
            float units = Mathf.Max(.01f, _config.unitsPerMetre);
            float lead = (_config.halfWidth * 2 / Mathf.Max(.1f, _config.horizontalSpeed)) * _diveSpeed +
                (_config.treasurePickupRadius + _hookRadius + .15f) / units;
            for (int i = 0; i < _count; i++)
            {
                var view = _views[i];
                if (view.Kind != SkillTreasureKind.Sonar || view.Collected || _correctionAttempted.Contains(view.Id)) continue;
                float depth = -view.SpawnPosition.y / units;
                var band = _sonarBands[i];
                if (depth < band.x || depth > band.y) continue;
                var screen = camera.WorldToViewportPoint(view.Position);
                if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                bool waiting = false;
                float min = Mathf.Max(band.x, run.Depth + lead);
                float max = band.y;
                for (int otherIndex = 0; otherIndex < _count; otherIndex++)
                {
                    var other = _views[otherIndex]; if (other.Kind != SkillTreasureKind.Sonar || other.Id == view.Id) continue;
                    float otherDepth = -other.SpawnPosition.y / units;
                    if (other.Id < view.Id)
                    {
                        if (!other.Collected && run.Depth < otherDepth + (_config.treasurePickupRadius + _hookRadius) / units) waiting = true;
                        min = Mathf.Max(min, otherDepth + _minSonarDepthGap);
                    }
                    else max = Mathf.Min(max, otherDepth - _minSonarDepthGap);
                }
                if (waiting) continue;
                _correctionAttempted.Add(view.Id);
                if (depth < run.Depth + lead || min > max) continue;
                CaptureSchools(_fish); _snapshotDepth = run.Depth; _snapshotTime = run.GameTime; _countdownSeconds = 0;
                if (!TryPlaceNearSchool(run, Mathf.Clamp(depth, min, max), min, max, new HashSet<int>(), out var point)) continue;
                // Reset both swept endpoints at the new position; the relocation itself cannot collect the pickup.
                view.ResetView(view.Id, view.Kind, point, _config.treasureVisualSize); CorrectionCount++;
            }
        }
        private static float Clearance(Vector2 point, FishSpawner fish)
        {
            float nearest = float.PositiveInfinity;
            foreach (var actor in fish.Spawned)
            {
                if (actor.IsCaptured || !actor.Head.enabled) continue;
                var head = actor.Head;
                float radius = head.radius * Mathf.Max(Mathf.Abs(head.transform.lossyScale.x), Mathf.Abs(head.transform.lossyScale.y));
                nearest = Mathf.Min(nearest, Vector2.Distance(point, head.transform.TransformPoint(head.offset)) - radius);
            }
            return nearest;
        }

        /// <summary>Draws and resolves only main-hook pickup candidates after this step's fish contacts have committed.</summary>
        public void Tick(RunModel run, Vector2 previousHook, Vector2 hook, float hookRadius, float dt, Camera camera = null)
        {
            if (run == null || run.RunId != _runId || run.IsPaused || dt <= 0) return;
            PlaceOnReturn(run);
            if (camera != null) CorrectOnEntry(run, camera);
            for (int i = 0; i < _count; i++)
            {
                var view = _views[i]; bool eligible = _abilities.CanCollectTreasure(view.Kind);
                float depth = -view.SpawnPosition.y / Mathf.Max(.01f, _config.unitsPerMetre);
                // Present the capsule only after its one-shot entry decision, avoiding a visible teleport.
                bool ready = camera == null || view.Kind != SkillTreasureKind.Sonar || view.Collected ||
                    _correctionAttempted.Contains(view.Id) || depth < _sonarBands[i].x || depth > _sonarBands[i].y;
                view.Tick(dt, InPhase(view.Kind, run.State) && (view.Collected || eligible), ready);
                if (view.Collected || !eligible) continue;
                Vector2 from = previousHook - (Vector2)view.PreviousPosition, to = hook - (Vector2)view.Position;
                Vector2 delta = to - from;
                float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector2.Dot(from, delta) / delta.sqrMagnitude) : 0;
                float radius = _config.treasurePickupRadius + hookRadius;
                if ((from + delta * t).sqrMagnitude > radius * radius ||
                    !_abilities.TryCollectTreasure(_runId, view.Id, view.Kind)) continue;
                Vector3 position = view.Position; view.Collect(); Collected?.Invoke(view.Kind, position);
            }
        }
        /// <summary>Immediately hides unusable capsules on surfacing/result without advancing their animation.</summary>
        public void RefreshVisibility(RunModel run)
        {
            for (int i = 0; i < _count; i++)
                if (!InPhase(_views[i].Kind, run.State) ||
                    (!_views[i].Collected && !_abilities.CanCollectTreasure(_views[i].Kind)))
                    _views[i].gameObject.SetActive(false);
        }

        /// <summary>One unstartled catchable small fish, as seen by sonar placement scoring.</summary>
        private struct SmallFish
        {
            public int Id;
            public Vector2 Centre;
            public float Radius;
            public float SwimMin, SwimMax;
            public float RootX, RootMin, RootMax, Speed, Direction, HeadOffsetX;
        }

        /// <summary>Live visual school bounds rebuilt once per run from spawn identity.</summary>
        private sealed class SchoolSnapshot
        {
            public int SchoolId;
            public float MinX, MaxX, MinY, MaxY, MaxRadius;
            public readonly List<SmallFish> Fish = new List<SmallFish>();
        }
    }
}
