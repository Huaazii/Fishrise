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
    /// <summary>Full-speed branch capture from untouched one-star fish in the saved fixed-seed population.</summary>
    public sealed class SeededBranchIntegrationTests : AbilityTestScene
    {
        [UnityTest]
        public IEnumerator FullMainBranchSweepsTheSavedSeedPopulationWithoutMovingItsOneStarFish()
        {
            Game.BeginRun();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var config = (FishingConfig)typeof(GameBootstrap).GetField("config", flags).GetValue(Game);
            var input = (DragInput)typeof(GameBootstrap).GetField("_input", flags).GetValue(Game);
            var species = Game.Spawner.Spawned.Select(f => f.Definition).Distinct().ToArray();
            var fixedConfig = Object.Instantiate(config); fixedConfig.useFixedPopulationSeed = true;
            Game.Spawner.SpawnRun(fixedConfig, species, Game.Hook.Radius * 2); Object.Destroy(fixedConfig);
            Assert.That(config.randomSeed, Is.Not.Zero); Assert.That(Game.Spawner.ActiveCount, Is.EqualTo(54));
            Assert.That(Game.Spawner.RunSeed, Is.EqualTo(config.randomSeed));
            var route = Route(config);
            for (int step = 0; step < 6000 && Game.Run.State != RunState.Ascending; step++)
            { input.Reset(RouteX(route, Game.Run.Depth + .12f)); Game.AdvanceGameplay(.02f); }
            Assert.That(Game.Run.State, Is.EqualTo(RunState.Ascending)); Assert.That(Game.Run.TotalCaptureCount, Is.Zero);
            var oneStar = Game.Spawner.Spawned.Where(f=>f.Definition.rarity==1)
                .ToDictionary(f=>f.InstanceId,f=>new { Fish=f, Range=f.SwimRange, Speed=f.SwimSpeed });
            // Fill only with higher-star fish, leaving every newly eligible one-star fish on its seeded motion.
            var mainFill = Game.Spawner.Spawned.Where(f=>f.Definition.rarity>1).Take(10).ToArray();
            Assert.That(mainFill.Length,Is.GreaterThan(0));
            foreach (var large in mainFill)
            {
                typeof(FishActor).GetProperty("SwimRange").SetValue(large, new Vector2(-4.3f, 4.3f));
                typeof(FishActor).GetProperty("SwimSpeed").SetValue(large, 0f);
                Align(large, Game.Hook.transform.position + Vector3.up * .13f);
            }
            // This population has fewer than ten higher-star fish; extra main-only controls leave seeded one-stars untouched.
            for(int i=mainFill.Length;i<10;i++) AtHead(mainFill[0].Definition,Game.Hook.transform.position+Vector3.up*.13f);
            Steps(1); Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(10)); Game.DeployBranches(); Steps(20);
            for (int step = 0; step < 200 && Game.Run.State == RunState.Ascending && Game.Run.BranchCaptureCount<6; step++)
            {
                var target=Game.Spawner.Spawned.Where(f=>f.Definition.rarity==1 && !f.IsCaptured && HeadCentre(f).y>Game.Hook.transform.position.y)
                    .OrderBy(f=>HeadCentre(f).y).FirstOrDefault();
                if(target!=null)
                {
                    int hookId=HeadCentre(target).x<0 ? 1 : 2;
                    if(Game.Abilities.Branches.RemainingCapacity(hookId)==0) hookId=3-hookId;
                    input.Reset(HeadCentre(target).x-(hookId==1 ? -2.16f : 2.16f));
                }
                float before = Game.Run.Depth; Steps(1);
                if (Game.Run.State == RunState.Ascending) Assert.That(before - Game.Run.Depth, Is.EqualTo(.65f).Within(.001));
            }
            Assert.That(Game.Run.BranchCaptureCount,Is.GreaterThan(0)); Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(10));
            foreach(var record in Game.Run.Captures.Where(r=>r.Source==CaptureSource.Branch))
            {
                Assert.That(oneStar.ContainsKey(record.FishInstanceId),Is.True);
                var original=oneStar[record.FishInstanceId];
                Assert.That(original.Fish.SwimRange,Is.EqualTo(original.Range)); Assert.That(original.Fish.SwimSpeed,Is.EqualTo(original.Speed));
            }
            yield return null;
        }
        private List<Vector2> Route(FishingConfig config)
        {
            var route = new List<Vector2> { Vector2.zero }; float gap = Game.Hook.Radius * 5;
            foreach (var row in Game.Spawner.Spawned.GroupBy(f => f.transform.position.y).OrderByDescending(g => g.Key))
            {
                var intervals = row.Select(f =>
                {
                    float half = Mathf.Max(f.Definition.sprite.bounds.extents.x * f.Definition.size, f.HeadHorizontalExtent);
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
