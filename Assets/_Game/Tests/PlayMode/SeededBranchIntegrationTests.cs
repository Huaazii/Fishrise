using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Full-speed branch capture against an unchanged school from the saved fixed-seed population.</summary>
    public sealed class SeededBranchIntegrationTests : AbilityTestScene
    {
        [UnityTest]
        public IEnumerator FullMainBranchSweepsTheSavedSeedPopulationWithoutMovingItsSmallFish()
        {
            Game.BeginRun();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var config = (FishingConfig)typeof(GameBootstrap).GetField("config", flags).GetValue(Game);
            var input = (DragInput)typeof(GameBootstrap).GetField("_input", flags).GetValue(Game);
            Assert.That(config.randomSeed, Is.Not.Zero); Assert.That(Game.Spawner.ActiveCount, Is.EqualTo(54));
            var route = Route(config);
            for (int step = 0; step < 6000 && Game.Run.State != RunState.Ascending; step++)
            { input.Reset(RouteX(route, Game.Run.Depth + .12f)); Game.AdvanceGameplay(.02f); }
            Assert.That(Game.Run.State, Is.EqualTo(RunState.Ascending)); Assert.That(Game.Run.TotalCaptureCount, Is.Zero);
            var target = Game.Spawner.Spawned.Where(f => f.Definition.isSmall && -f.Head.transform.position.y < 65 && -f.Head.transform.position.y > 35)
                .OrderBy(f => f.Head.transform.position.y).First();
            var originalRange = target.SwimRange; float originalSpeed = target.SwimSpeed;
            // Establish a full main hook using existing large fish. The target school stays at its seeded position and speed.
            foreach (var large in Game.Spawner.Spawned.Where(f => !f.Definition.isSmall).Take(10))
            {
                typeof(FishActor).GetProperty("SwimRange").SetValue(large, new Vector2(-4.3f, 4.3f));
                typeof(FishActor).GetProperty("SwimSpeed").SetValue(large, 0f);
                Align(large, Game.Hook.transform.position + Vector3.up * .13f);
            }
            Steps(1); Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(10)); Click("Branch button"); Steps(20);
            int hookId = target.Head.transform.position.x < 0 ? 1 : 2; float offset = hookId == 1 ? -2.16f : 2.16f;
            for (int step = 0; step < 200 && Game.Run.State == RunState.Ascending && !target.IsCaptured; step++)
            {
                input.Reset(target.Head.transform.position.x - offset);
                float before = Game.Run.Depth; Steps(1);
                if (Game.Run.State == RunState.Ascending) Assert.That(before - Game.Run.Depth, Is.EqualTo(.65f).Within(.001));
            }
            Assert.That(target.IsCaptured, Is.True); Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(10));
            Assert.That(Game.Run.Captures.Single(r => r.FishInstanceId == target.InstanceId).Source, Is.EqualTo(CaptureSource.Branch));
            Assert.That(target.SwimRange, Is.EqualTo(originalRange)); Assert.That(target.SwimSpeed, Is.EqualTo(originalSpeed));
            yield return null;
        }
        private List<Vector2> Route(FishingConfig config)
        {
            var route = new List<Vector2> { Vector2.zero }; float gap = Game.Hook.Radius * 5;
            foreach (var row in Game.Spawner.Spawned.GroupBy(f => f.transform.position.y).OrderByDescending(g => g.Key))
            {
                var intervals = row.Select(f =>
                {
                    float half = Mathf.Max(f.Definition.sprite.bounds.extents.x, Mathf.Abs(f.Definition.headPosition.x) + f.Definition.headRadius) * f.Definition.size;
                    return new Vector2(f.SwimRange.x - half, f.SwimRange.y + half);
                }).OrderBy(v => v.x);
                float end = -config.halfWidth, width = 0; var best = Vector2.zero;
                foreach (var interval in intervals)
                {
                    if (interval.x - end > width) { best = new Vector2(end, interval.x); width = interval.x - end; }
                    end = Mathf.Max(end, interval.y);
                }
                if (config.halfWidth - end > width) best = new Vector2(end, config.halfWidth);
                float x = Mathf.Clamp(0, best.x + gap * .5f - .0001f, best.y - gap * .5f + .0001f);
                route.Add(new Vector2(-row.Key / config.unitsPerMetre, x));
            }
            return route;
        }
        private static float RouteX(List<Vector2> route, float depth)
        {
            for (int i = 1; i < route.Count; i++)
                if (depth <= route[i].x) return Mathf.Lerp(route[i - 1].y, route[i].y, Mathf.InverseLerp(route[i - 1].x, route[i].x, depth));
            return route[route.Count - 1].y;
        }
    }
}
