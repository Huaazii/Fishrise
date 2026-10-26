using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Saved-asset population, moving passage and real-physics navigability acceptance.</summary>
    public sealed class PopulationIntegrationTests
    {
        private GameBootstrap _game;
        private FishingConfig _config;
        private FishingConfig _savedConfig;
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main"); yield return null; yield return null;
            _game = Object.FindObjectOfType<GameBootstrap>(); _game.enabled = false;
            var field = typeof(GameBootstrap).GetField("config", BindingFlags.Instance | BindingFlags.NonPublic);
            _savedConfig = (FishingConfig)field.GetValue(_game);
            _config = Object.Instantiate(_savedConfig); _config.useFixedPopulationSeed = true;
            field.SetValue(_game, _config);
            _game.BeginRun();
        }
        [UnityTearDown]
        public IEnumerator LeaveScene()
        { SceneManager.LoadScene("Menu"); yield return null; yield return null; Object.Destroy(_config); LogAssert.NoUnexpectedReceived(); }

        [UnityTest]
        public IEnumerator SevenSpeciesMatchValuesDepthLimitsAndExactBandCounts()
        {
            Assert.That(_config.FishCount, Is.EqualTo(54)); Assert.That(_game.Spawner.ActiveCount, Is.EqualTo(54));
            var fish = _game.Spawner.Spawned;
            var expected = new[] { ("puffer", 14, 10, 10, 1, 0f), ("blue", 10, 10, 10, 1, 0f),
                ("red", 6, 15, 10, 2, 20f), ("small", 6, 5, 5, 1, 0f),
                ("yellow", 2, 40, 30, 3, 60f), ("eel", 1, 80, 50, 4, 85f), ("silver", 15, 3, 3, 1, 0f) };
            foreach (var item in expected)
            {
                var group = fish.Where(f => f.Definition.speciesId == item.Item1).ToArray();
                Assert.That(group.Length, Is.EqualTo(item.Item2), item.Item1);
                var definition = group[0].Definition;
                Assert.That(definition.gold, Is.EqualTo(item.Item3)); Assert.That(definition.shells, Is.EqualTo(item.Item4));
                Assert.That(definition.rarity, Is.EqualTo(item.Item5)); Assert.That(definition.minDepth, Is.EqualTo(item.Item6));
                Assert.That(definition.maxDepth, Is.EqualTo(100)); Assert.That(definition.isCatchable, Is.True);
                Assert.That(definition.isSmall, Is.EqualTo(item.Item1 == "small" || item.Item1 == "silver"));
                Assert.That(definition.sprite, Is.Not.Null);
                foreach (var actor in group)
                {
                    float depth = -actor.transform.position.y / _config.unitsPerMetre;
                    Assert.That(depth, Is.InRange(definition.minDepth, definition.maxDepth));
                    Assert.That(actor.SwimSpeed, Is.InRange(definition.swimSpeed, definition.swimSpeedMax));
                }
            }
            var counts = new[] { 12, 13, 14, 15 };
            for (int i = 0; i < 4; i++)
            {
                Assert.That(_config.depthBands[i].minDepth, Is.EqualTo(i * 25));
                Assert.That(_config.depthBands[i].maxDepth, Is.EqualTo((i + 1) * 25));
                Assert.That(fish.Count(f => -f.transform.position.y / _config.unitsPerMetre >= i * 25 &&
                    -f.transform.position.y / _config.unitsPerMetre < (i + 1) * 25), Is.EqualTo(counts[i]));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedSeedRestartsRepeatSchoolsAndSwimmingNeverClosesPassages()
        {
            var before = _game.Spawner.Spawned.Select(f => (f.Definition.speciesId, f.transform.position, f.SwimRange, f.SwimSpeed)).ToArray();
            var oldIds = _game.Spawner.Spawned.Select(f => f.InstanceId).ToArray();
            _game.BeginRun();
            var after = _game.Spawner.Spawned.Select(f => (f.Definition.speciesId, f.transform.position, f.SwimRange, f.SwimSpeed)).ToArray();
            CollectionAssert.AreEqual(before, after);
            foreach (var fish in _game.Spawner.Spawned) CollectionAssert.DoesNotContain(oldIds, fish.InstanceId);
            var route = PassageRoute(true);
            Assert.That(route.Count, Is.GreaterThan(16));
            for (int step = 0; step < 600; step++)
            {
                _game.Spawner.Tick(.1f);
                foreach (var fish in _game.Spawner.Spawned)
                {
                    Assert.That(fish.transform.position.x, Is.InRange(fish.SwimRange.x - .0001f, fish.SwimRange.y + .0001f));
                    Assert.That(Mathf.Abs(fish.transform.position.x) + fish.Definition.sprite.bounds.extents.x * fish.Definition.size,
                        Is.LessThanOrEqualTo(_config.halfWidth + .001f));
                }
                if (step % 20 == 0) PassageRoute(false);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalRestartsVarySeedsAndLayoutsWithoutGrowingPoolsOrChangingBudgets()
        {
            Assert.That(_savedConfig.useFixedPopulationSeed, Is.False, "Normal play must not repeat the debug seed.");
            _config.useFixedPopulationSeed = false;
            var seeds = new HashSet<int>(); var layouts = new HashSet<string>();
            int allocated = _game.Spawner.AllocatedCount;
            for (int run = 0; run < 20; run++)
            {
                _game.BeginRun(); seeds.Add(_game.Spawner.RunSeed);
                layouts.Add(string.Join(";", _game.Spawner.Spawned.Select(f => f.transform.position.ToString("F3"))));
                Assert.That(_game.Spawner.ActiveCount, Is.EqualTo(54));
                Assert.That(_game.Spawner.AllocatedCount, Is.EqualTo(allocated));
                Assert.That(_game.AppliedCaptureEvents, Is.Zero);
            }
            Assert.That(seeds.Count, Is.EqualTo(20)); Assert.That(layouts.Count, Is.EqualTo(20));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwentyReproducibleSeedsKeepHabitatsSpacingAndDifferentSmallSchoolCompositions()
        {
            var compositions = new HashSet<string>();
            for (int seed = 1; seed <= 20; seed++)
            {
                _config.randomSeed = seed; _game.BeginRun();
                Assert.That(_game.Spawner.RunSeed, Is.EqualTo(seed));
                var fish = _game.Spawner.Spawned;
                Assert.That(fish.Select(f => f.transform.position.y).Distinct().Count(), Is.GreaterThan(40), "Continuous depths, not shared grid rows.");
                foreach (var actor in fish)
                {
                    float depth = -actor.transform.position.y / _config.unitsPerMetre;
                    Assert.That(depth, Is.InRange(actor.Definition.minDepth, actor.Definition.maxDepth));
                    Assert.That(Mathf.Abs(actor.transform.position.x) + Mathf.Max(actor.Definition.sprite.bounds.extents.x * actor.Definition.size,
                        actor.HeadHorizontalExtent), Is.LessThan(_config.halfWidth));
                }
                for (int band = 0; band < 4; band++)
                    Assert.That(fish.Count(f => -f.transform.position.y / _config.unitsPerMetre >= band * 25 &&
                        -f.transform.position.y / _config.unitsPerMetre < (band + 1) * 25), Is.EqualTo(12 + band));
                for (int a = 0; a < fish.Count; a++)
                    for (int b = a + 1; b < fish.Count; b++)
                    {
                        var first = fish[a].Head; var second = fish[b].Head;
                        float radiusA = first.radius * Mathf.Max(Mathf.Abs(first.transform.lossyScale.x), Mathf.Abs(first.transform.lossyScale.y));
                        float radiusB = second.radius * Mathf.Max(Mathf.Abs(second.transform.lossyScale.x), Mathf.Abs(second.transform.lossyScale.y));
                        float distance = Vector2.Distance(first.transform.TransformPoint(first.offset), second.transform.TransformPoint(second.offset));
                        Assert.That(distance, Is.GreaterThan(radiusA + radiusB), "Overlapping initial head circles for seed " + seed);
                    }
                var bottomSilver = fish.Where(f => f.Definition.speciesId == "silver" && -f.transform.position.y / _config.unitsPerMetre >= 75);
                compositions.Add(string.Join("+", bottomSilver.GroupBy(f => f.SchoolId).Select(g => g.Count()).OrderBy(n => n)));
                PassageRoute(true);
                _game.Spawner.Tick(3); PassageRoute(false);
            }
            Assert.That(compositions.Count, Is.GreaterThanOrEqualTo(3), "Five small fish must not always split as 3+2.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedPopulationHasAPlayableRouteToFullDepthWithoutSpendingProtection()
        {
            var route = PassageRoute(true);
            var input = (DragInput)typeof(GameBootstrap).GetField("_input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_game);
            for (int tick = 0; tick < 4000 && _game.Run.State != RunState.BottomHold; tick++)
            {
                input.Reset(RouteX(route, _game.Run.Depth + .12f)); _game.AdvanceGameplay(.02f);
                if (_game.Run.State == RunState.Diving && _game.Run.Depth < _game.Run.Rules.BottomZoneDepth)
                    Assert.That(_game.Run.Protection, Is.EqualTo(2), "Passage required protection at depth " + _game.Run.Depth);
                Assert.That(_game.Run.TotalCaptureCount, Is.Zero);
            }
            Assert.That(_game.Run.State, Is.EqualTo(RunState.BottomHold)); Assert.That(_game.Run.Depth, Is.EqualTo(100));
            for (int tick = 0; tick < 4000 && _game.Run.State != RunState.Result; tick++)
            { input.Reset(RouteX(route, _game.Run.Depth)); _game.AdvanceGameplay(.02f); }
            Assert.That(_game.Run.State, Is.EqualTo(RunState.Result)); Assert.That(_game.Run.ReachedDepth, Is.EqualTo(100));
            yield return null;
        }

        private List<Vector2> PassageRoute(bool entireSwimRange)
        {
            var route = new List<Vector2> { Vector2.zero };
            float gap = _game.Hook.Radius * 2 * 2.5f;
            // Sample cross-sections including each fish's full vertical extent; groups no longer share exact Y.
            for (float depth = .25f; depth <= _config.maxDepth; depth += .25f)
            {
                float y = -depth * _config.unitsPerMetre;
                var intervals = _game.Spawner.Spawned.Where(f =>
                {
                    float radius = f.Head.radius * Mathf.Max(Mathf.Abs(f.Head.transform.lossyScale.x), Mathf.Abs(f.Head.transform.lossyScale.y));
                    float halfHeight = Mathf.Max(f.Definition.sprite.bounds.extents.y * f.Definition.size,
                        Mathf.Abs(f.Head.transform.TransformPoint(f.Head.offset).y - f.transform.position.y) + radius);
                    return Mathf.Abs(y - f.transform.position.y) <= halfHeight + _game.Hook.Radius + .05f;
                }).Select(f =>
                {
                    float half = Mathf.Max(f.Definition.sprite.bounds.extents.x * f.Definition.size, f.HeadHorizontalExtent);
                    return entireSwimRange ? new Vector2(f.SwimRange.x - half, f.SwimRange.y + half) :
                        new Vector2(f.transform.position.x - half, f.transform.position.x + half);
                }).OrderBy(v => v.x).ToArray();
                float end = -_config.halfWidth, bestWidth = 0; var best = Vector2.zero;
                foreach (var interval in intervals)
                {
                    if (interval.x - end > bestWidth) { best = new Vector2(end, interval.x); bestWidth = interval.x - end; }
                    end = Mathf.Max(end, interval.y);
                }
                if (_config.halfWidth - end > bestWidth) { best = new Vector2(end, _config.halfWidth); bestWidth = _config.halfWidth - end; }
                Assert.That(bestWidth, Is.GreaterThanOrEqualTo(gap - .001f), "Closed passage at depth=" + depth);
                float x = Mathf.Clamp(route[route.Count - 1].y, best.x + gap * .5f - .0001f, best.y - gap * .5f + .0001f);
                route.Add(new Vector2(depth, x));
            }
            return route;
        }
        private static float RouteX(List<Vector2> route, float depth)
        {
            for (int i = 1; i < route.Count; i++)
                if (depth <= route[i].x) return Mathf.Lerp(route[i - 1].y, route[i].y,
                    Mathf.InverseLerp(route[i - 1].x, route[i].x, depth));
            return route[route.Count - 1].y;
        }
    }
}
