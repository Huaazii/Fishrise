using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fishing.Tests
{
    /// <summary>Attributed sonar evidence on normal-speed routes, including missed pickups and cancelled echoes.</summary>
    public sealed class SonarPickupPlacementTests : AbilityTestScene
    {
        protected override bool InteractiveReeling => true;
        protected override bool SkillTreasures => true;
        private const int Seeds = 32;

        [UnityTest]
        public IEnumerator ImpossiblePickupGeometryIsRejectedInsteadOfReturningAnUnsafeFallback()
        {
            Until(RunState.Diving); Game.Spawner.Clear();
            var obstruction = AtHead(Small, new Vector3(0, -31, 0)); obstruction.Head.radius = 1000;
            var config = (FishingConfig)ConfigField.GetValue(Game);
            Assert.Throws<System.InvalidOperationException>(() =>
                Game.Treasures.Bind(Game.Run, Game.Abilities, config, Game.Spawner, Game.Hook.Radius));
            Assert.That(Game.Abilities.CollectedTreasureCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EntryCorrectionRunsOnceWithoutSweepingIntoTheHookAndResetsOnRestart()
        {
            var field = ConfigField; var saved = (FishingConfig)field.GetValue(Game); var copy = Object.Instantiate(saved);
            copy.useFixedPopulationSeed = true; field.SetValue(Game, copy);
            try
            {
                Game.BeginRun(); Game.Spawner.Clear();
                var view = Game.Treasures.Views.First(v => v.Kind == SkillTreasureKind.Sonar);
                var second = Game.Treasures.Views.Last(v => v.Kind == SkillTreasureKind.Sonar);
                view.ResetView(view.Id, view.Kind, new Vector3(3, -18, 0), copy.treasureVisualSize);
                second.ResetView(second.Id, second.Kind, new Vector3(-3, -65, 0), copy.treasureVisualSize);
                var school = AtHead(Small, new Vector3(2, -22, 0), 0, new Vector2(2, 2));
                typeof(FishActor).GetProperty(nameof(FishActor.SchoolId)).SetValue(school, 1);
                Until(RunState.Diving); Game.CameraRig.Follow(Game.Run, 0, true);
                for (int step = 0; step < 200 && Camera.main.WorldToViewportPoint(view.Position).y < 0; step++)
                { Game.AdvanceGameplay(.02f); Game.CameraRig.Follow(Game.Run, .02f); }
                Assert.That(Camera.main.WorldToViewportPoint(view.Position).y, Is.InRange(0f, 1f));
                Assert.That(view.GetComponentsInChildren<SpriteRenderer>(true).All(r => r.color.a == 0), Is.True,
                    "An entry-pending capsule must not show its old position before relocation.");
                int id = view.Id; var original = view.SpawnPosition;
                Game.PauseRun(); Game.AdvanceGameplay(.2f);
                Assert.That(Game.Treasures.CorrectionAttemptCount, Is.Zero);
                Game.ResumeRun(); Game.AdvanceGameplay(.02f);
                Assert.That(Game.Treasures.CorrectionAttemptCount, Is.EqualTo(1));
                Assert.That(Game.Treasures.CorrectionCount, Is.EqualTo(1));
                Assert.That(view.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.enabled && r.color.a > 0), Is.True);
                Assert.That(view.Id, Is.EqualTo(id)); Assert.That(view.Collected, Is.False);
                Assert.That(Game.Abilities.CollectedTreasureCount, Is.Zero, "Moving the capsule must not manufacture a swept pickup.");
                Assert.That(view.SpawnPosition, Is.Not.EqualTo(original)); var corrected = view.SpawnPosition;
                foreach (var fish in Game.Spawner.Spawned) fish.transform.position += Vector3.right * .15f;
                Steps(40);
                Assert.That(view.SpawnPosition, Is.EqualTo(corrected), "A corrected capsule must not follow subsequent fish movement.");
                Assert.That(Game.Treasures.CorrectionAttemptCount, Is.EqualTo(1));
                Game.BeginRun(); Assert.That(Game.Treasures.CorrectionAttemptCount, Is.Zero);
                Assert.That(Game.Treasures.CorrectionCount, Is.Zero); Assert.That(Game.Treasures.Views.Count, Is.EqualTo(3));
            }
            finally { field.SetValue(Game, saved); Object.Destroy(copy); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EntryCorrectionKeepsEachSonarInItsHalfWhenTheSchoolIsAcrossTheMidpoint()
        {
            var field = ConfigField; var saved = (FishingConfig)field.GetValue(Game); var copy = Object.Instantiate(saved);
            copy.useFixedPopulationSeed = true; field.SetValue(Game, copy);
            try
            {
                for (int variant = 0; variant < 2; variant++)
                {
                    Game.BeginRun(); Game.Spawner.Clear();
                    var sonars = Game.Treasures.Views.Where(v => v.Kind == SkillTreasureKind.Sonar).ToArray();
                    sonars[0].ResetView(sonars[0].Id, sonars[0].Kind, new Vector3(3, variant == 0 ? -44 : -20, 0), copy.treasureVisualSize);
                    sonars[1].ResetView(sonars[1].Id, sonars[1].Kind, new Vector3(-3, variant == 0 ? -75 : -55, 0), copy.treasureVisualSize);
                    var fish = AtHead(Small, new Vector3(2, variant == 0 ? -54.5f : -47.2f, 0), 0, new Vector2(2, 2));
                    typeof(FishActor).GetProperty(nameof(FishActor.SchoolId)).SetValue(fish, 1);
                    Until(RunState.Diving); Game.CameraRig.Follow(Game.Run, 0, true);
                    if (variant == 1)
                    {
                        // Commit the earlier pickup so the second entry decision can run independently.
                        Game.Treasures.Tick(Game.Run, sonars[0].Position, sonars[0].Position, Game.Hook.Radius, .02f);
                        Assert.That(sonars[0].Collected, Is.True);
                    }
                    for (int step = 0; step < 1000 && Game.Treasures.CorrectionAttemptCount == 0 && Game.Run.State == RunState.Diving; step++)
                    { Game.AdvanceGameplay(.02f); Game.CameraRig.Follow(Game.Run, .02f); }
                    Assert.That(Game.Treasures.CorrectionCount, Is.EqualTo(1), "The live school must yield a valid correction within the same half.");
                    AssertSeparatedPickupWindows(copy);
                    Assert.That(sonars[variant].Collected, Is.False, "The correction itself cannot collect the pickup.");
                }
            }
            finally { field.SetValue(Game, saved); Object.Destroy(copy); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BobbingPickupEnvelopeKeepsScaledOffsetHooksClearOfInitialFishAndCentreLane()
        {
            var field = ConfigField; var saved = (FishingConfig)field.GetValue(Game); var copy = Object.Instantiate(saved);
            copy.useFixedPopulationSeed = true; field.SetValue(Game, copy);
            var head = Game.Hook.Head; var scale = head.transform.localScale; var offset = head.offset;
            try
            {
                for (int variant = 0; variant < 2; variant++)
                {
                    head.transform.localScale = scale * (variant == 0 ? 1 : 1.25f);
                    head.offset = variant == 0 ? offset : new Vector2(.18f, -.12f);
                    for (int seed = 0; seed < 20; seed++)
                    {
                        copy.randomSeed = seed * 7919 + 17; Game.BeginRun();
                        float radius = head.radius * Mathf.Max(Mathf.Abs(head.transform.lossyScale.x), Mathf.Abs(head.transform.lossyScale.y));
                        float laneX = head.transform.TransformPoint(head.offset).x - Game.Hook.transform.position.x;
                        foreach (var view in Game.Treasures.Views)
                        {
                            Assert.That(Mathf.Abs(view.SpawnPosition.x - laneX), Is.GreaterThan(copy.treasurePickupRadius + radius), "Centre descent must miss pickups.");
                            for (int direction = 0; direction < 16; direction++)
                            {
                                float angle = direction * Mathf.PI / 8;
                                var hookCentre = (Vector2)view.SpawnPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                                    (copy.treasurePickupRadius + radius + SkillTreasureView.BobAmplitude);
                                foreach (var fish in Game.Spawner.Spawned)
                                    Assert.That(Vector2.Distance(hookCentre, HeadCentre(fish)), Is.GreaterThan(radius + HeadRadius(fish)),
                                        "A hook touching the bobbing pickup must not overlap an initial fish head. Seed " + copy.randomSeed);
                            }
                        }
                        AssertSeparatedPickupWindows(copy);
                    }
                }
            }
            finally { head.transform.localScale = scale; head.offset = offset; field.SetValue(Game, saved); Object.Destroy(copy); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FallbackPreservesReleaseWindowsAcrossWorldScalesAndSonarTimings()
        {
            var field = ConfigField; var saved = (FishingConfig)field.GetValue(Game); var copy = Object.Instantiate(saved);
            var sonarField = typeof(GameBootstrap).GetField("sonarConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            var savedSonar = (SonarConfig)sonarField.GetValue(Game); var sonar = Object.Instantiate(savedSonar);
            var head = Game.Hook.Head; var scale = head.transform.localScale; var offset = head.offset;
            copy.useFixedPopulationSeed = true; field.SetValue(Game, copy); sonarField.SetValue(Game, sonar);
            try
            {
                for (int variant = 0; variant < 3; variant++)
                {
                    copy.unitsPerMetre = variant == 0 ? 1 : variant == 1 ? .5f : 2;
                    copy.diveSpeed = variant == 0 ? 6 : variant == 1 ? 7 : 4;
                    sonar.pulseIntervalSeconds = variant == 2 ? 1.5f : variant == 1 ? .75f : 1;
                    sonar.waveSeconds = variant == 0 ? .4f : .6f;
                    head.transform.localScale = scale * (variant == 1 ? 1.25f : 1);
                    head.offset = variant == 1 ? new Vector2(.18f, -.12f) : offset;
                    Game.BeginRun(); AssertSeparatedPickupWindows(copy);
                    Game.Spawner.Clear();
                    float radius = HeadRadiusForPickup(head);
                    Vector2 pickupOffset = head.transform.TransformPoint(head.offset) - Game.Hook.transform.position;
                    Game.Treasures.Bind(Game.Run, Game.Abilities, copy, Game.Spawner, radius, pickupOffset);
                    Assert.That(Game.Treasures.FallbackCount, Is.EqualTo(2), "An empty sea must exercise both fallback placements.");
                    AssertSeparatedPickupWindows(copy);
                }
            }
            finally
            {
                head.transform.localScale = scale; head.offset = offset;
                field.SetValue(Game, saved); sonarField.SetValue(Game, savedSonar); Object.Destroy(copy); Object.Destroy(sonar);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InsufficientDepthRangeIsRejectedInsteadOfCompressingSonars()
        {
            var copy = Object.Instantiate((FishingConfig)ConfigField.GetValue(Game));
            try
            {
                copy.sonarTreasureDepths = new Vector2(14, 24);
                Assert.Throws<System.InvalidOperationException>(() =>
                    Game.Treasures.Bind(Game.Run, Game.Abilities, copy, Game.Spawner, Game.Hook.Radius));
            }
            finally { Object.Destroy(copy); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverlappingSonarsCannotBorrowEachOthersNewlyScaredFish()
        {
            Until(RunState.Diving);
            var fish = AtHead(Small, Game.Hook.transform.position + Vector3.right * 1.2f);
            var pulses = new List<SonarPulseResult>(); Game.AbilityView.SonarResolved += pulses.Add;
            try
            {
                Assert.That(Game.Abilities.TryCollectTreasure(Game.Run.RunId, 1, SkillTreasureKind.Sonar), Is.True);
                Assert.That(Game.Abilities.TryCollectTreasure(Game.Run.RunId, 2, SkillTreasureKind.Sonar), Is.True);
                Steps(105);
                CollectionAssert.AreEqual(new[] { "1:0", "2:0", "1:1", "2:1", "1:2", "2:2" },
                    pulses.Select(p => p.Pulse.TreasureId + ":" + p.Pulse.Index));
                CollectionAssert.AreEqual(new[] { fish.InstanceId }, pulses[0].FreshFishIds);
                Assert.That(pulses.Where(p => p.Pulse.TreasureId == 2).SelectMany(p => p.FreshFishIds), Is.Empty,
                    "A nearby activation must not inherit the first activation's fresh targets.");
                Assert.That(pulses.All(p => p.Pulse.RunId == Game.Run.RunId), Is.True);
            }
            finally { Game.AbilityView.SonarResolved -= pulses.Add; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameSeedUsesStableSchoolMemberKeysInsteadOfCrossRunSpawnIds()
        {
            var field = ConfigField; var saved = (FishingConfig)field.GetValue(Game); var copy = Object.Instantiate(saved);
            copy.useFixedPopulationSeed = true; copy.randomSeed = 31693; field.SetValue(Game, copy);
            try
            {
                Game.BeginRun(); var first = FishKeys();
                Game.BeginRun(); var second = FishKeys();
                Assert.That(first.Keys.Intersect(second.Keys), Is.Empty, "Spawn IDs deliberately change across runs.");
                Assert.That(first.Values.Intersect(second.Values).Count(), Is.EqualTo(first.Count));
                Assert.That(first.Count, Is.GreaterThan(0));
            }
            finally { field.SetValue(Game, saved); Object.Destroy(copy); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SonarPickupsScatterUnstartledSmallFishWithinThreePulses()
        {
            var field = ConfigField; var saved = (FishingConfig)field.GetValue(Game); var copy = Object.Instantiate(saved);
            copy.useFixedPopulationSeed = true; field.SetValue(Game, copy);
            var report = new StringBuilder();
            int anchored = 0, fallback = 0, shared = 0, compared = 0, corrections = 0, attempts = 0;
            var independent = new List<Measurement>(); var sequential = new List<Measurement>();
            try
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    copy.randomSeed = seed * 7919 + 17;
                    var pair = new Measurement[2];
                    for (int index = 0; index < 2; index++)
                    {
                        var trial = MeasureRoute(index);
                        attempts += Game.Treasures.CorrectionAttemptCount; corrections += Game.Treasures.CorrectionCount;
                        if (index == 0) { anchored += Game.Treasures.AnchoredCount; fallback += Game.Treasures.FallbackCount; }
                        pair[index] = trial[index]; independent.Add(pair[index]);
                        Describe(report, copy.randomSeed, "independent", pair[index]);
                    }
                    if (pair.All(p => p.Collected)) { compared++; shared += pair[0].Fresh.Intersect(pair[1].Fresh).Count(); }
                    var route = MeasureRoute(-1);
                    attempts += Game.Treasures.CorrectionAttemptCount; corrections += Game.Treasures.CorrectionCount;
                    foreach (var sample in route)
                    { sequential.Add(sample); Describe(report, copy.randomSeed, "sequential", sample); }
                }
            }
            finally { field.SetValue(Game, saved); Object.Destroy(copy); }
            TestContext.WriteLine("anchored " + anchored + " fallback " + fallback + " sharedTargets " + shared + "/" + compared +
                " correctionAttempts " + attempts + " corrections " + corrections + " correctionMisses " + (attempts - corrections));
            Summarize("independent", independent); Summarize("sequential", sequential);
            TestContext.WriteLine(report.ToString());
            Assert.That(anchored + fallback, Is.EqualTo(Seeds * 2));
            foreach (var samples in new[] { independent, sequential })
                for (int index = 0; index < 2; index++)
                {
                    var reached = samples.Where(p => p.Index == index && p.Collected).ToArray();
                    Assert.That(reached, Is.Not.Empty, "Each route and pickup must have measured arrivals; zero samples cannot pass.");
                    Assert.That(reached.Count(p => p.Fresh.Count == 0), Is.Zero, "Every measured pickup must affect an unstartled fish.");
                }
            // Independent routes may legitimately reach the same fish at different times. Actual sequential
            // fresh targets above are the benefit gate; cross-run overlap is an honestly reported comparison.
            yield return null;
        }

        private static FieldInfo ConfigField => typeof(GameBootstrap).GetField("config", BindingFlags.Instance | BindingFlags.NonPublic);
        private static float HeadRadiusForPickup(CircleCollider2D head) =>
            head.radius * Mathf.Max(Mathf.Abs(head.transform.lossyScale.x), Mathf.Abs(head.transform.lossyScale.y));

        private void AssertSeparatedPickupWindows(FishingConfig config)
        {
            var sonars = Game.Treasures.Views.Where(v => v.Kind == SkillTreasureKind.Sonar).OrderBy(v => v.Id).ToArray();
            float reach = config.treasurePickupRadius + HeadRadiusForPickup(Game.Hook.Head) + SkillTreasureView.BobAmplitude;
            var effect = Game.Abilities.Sonar.Effect;
            float releaseWindow = 2 * effect.PulseIntervalSeconds + effect.WaveSeconds;
            for (int i = 0; i < sonars.Length; i++)
            {
                float depth = -sonars[i].SpawnPosition.y / config.unitsPerMetre;
                Assert.That(depth, Is.InRange(config.sonarTreasureDepths.x, config.sonarTreasureDepths.y));
                Assert.That(depth, Is.InRange(Game.Run.Rules.MaxDepth * i / sonars.Length, Game.Run.Rules.MaxDepth * (i + 1) / sonars.Length),
                    "Initial and corrected pickups must stay in their own half of the complete route.");
                float hookOffsetY = Mathf.Abs(Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset).y - Game.Hook.transform.position.y);
                float lastWaveDepth = depth + (reach + hookOffsetY) / config.unitsPerMetre + Game.Run.Rules.DiveSpeed * releaseWindow;
                Assert.That(lastWaveDepth, Is.LessThan(Game.Run.Rules.BottomZoneDepth),
                    "Even the latest edge contact must leave enough depth for all three pulses and the final wave.");
                if (i == 0) continue;
                float earliestGap = (sonars[i - 1].SpawnPosition.y - sonars[i].SpawnPosition.y - 2 * reach) /
                    (config.unitsPerMetre * Game.Run.Rules.DiveSpeed);
                Assert.That(earliestGap, Is.GreaterThanOrEqualTo(releaseWindow + .5f),
                    "Even a late first contact followed by an early second contact must leave a quiet gap after the final wave.");
            }
        }


        // No transform writes: steering uses the same target and speed limit as normal input.
        private Measurement[] MeasureRoute(int target)
        {
            Game.BeginRun(); var keys = FishKeys(); Until(RunState.Diving);
            var views = Game.Treasures.Views.Where(v => v.Kind == SkillTreasureKind.Sonar)
                .OrderByDescending(v => v.SpawnPosition.y).ToArray();
            var samples = views.Select((v, i) => new Measurement { Index = i, Id = v.Id, Position = v.SpawnPosition }).ToArray();
            var config = (FishingConfig)ConfigField.GetValue(Game);
            AssertSeparatedPickupWindows(config);
            var input = (DragInput)typeof(GameBootstrap).GetField("_input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Game);
            void Resolved(SonarPulseResult result)
            {
                Assert.That(result.Pulse.RunId, Is.EqualTo(Game.Run.RunId));
                var sample = samples.Single(s => s.Id == result.Pulse.TreasureId);
                Assert.That(result.Pulse.Index, Is.EqualTo(sample.Pulses));
                if (sample.Pulses++ == 0) sample.CollectedAt = result.Pulse.GameTime;
                foreach (int id in result.FreshFishIds)
                { sample.Fresh.Add(keys[id]); if (result.Pulse.Index == 0) sample.First.Add(keys[id]); }
                sample.Evidence.Add("source " + result.Pulse.TreasureId + " pulse " + result.Pulse.Index + " t " + result.Pulse.GameTime.ToString("0.000") +
                    " x " + result.Origin.x.ToString("0.00") + " depth " + (-result.Origin.y).ToString("0.00") +
                    " fresh " + string.Join(",", result.FreshFishIds.Select(id => keys[id])));
            }
            Game.AbilityView.SonarResolved += Resolved;
            try
            {
                for (int step = 0; step < 1800 && Game.Run.State == RunState.Diving; step++)
                {
                    int index = target >= 0 ? target : (views[0].Collected ? 1 : 0);
                    input.Reset(views[index].Position.x); Game.AdvanceGameplay(.02f); Game.CameraRig.Follow(Game.Run, .02f);
                    var last = samples[target >= 0 ? target : 1];
                    if (last.Pulses > 0 && Game.Run.GameTime - last.CollectedAt >= 2.04) break;
                }
            }
            finally { Game.AbilityView.SonarResolved -= Resolved; }
            for (int i = 0; i < samples.Length; i++)
            {
                var sample = samples[i]; sample.Position = views[i].SpawnPosition;
                sample.Collected = views[i].Collected; sample.EndState = Game.Run.State;
                Assert.That(sample.Collected, Is.EqualTo(sample.Pulses > 0), "Every actual pickup needs an attributed first pulse.");
                if (sample.Collected && sample.EndState == RunState.Diving && Game.Run.GameTime >= sample.CollectedAt + 2)
                    Assert.That(sample.Pulses, Is.EqualTo(3), "A complete dive window must include all three pulses.");
            }
            Assert.That(Game.Treasures.CorrectionAttemptCount, Is.LessThanOrEqualTo(views.Length));
            AssertSeparatedPickupWindows(config);
            if (samples.All(s => s.Collected))
            {
                var effect = Game.Abilities.Sonar.Effect;
                Assert.That(samples[1].CollectedAt - samples[0].CollectedAt,
                    Is.GreaterThanOrEqualTo(2 * effect.PulseIntervalSeconds + effect.WaveSeconds + .5f),
                    "Actual sequential pickups must not overlap the previous sonar's pulse train or final wave.");
            }
            return samples;
        }

        private Dictionary<int, string> FishKeys()
        {
            var counts = new Dictionary<int, int>(); var keys = new Dictionary<int, string>();
            foreach (var fish in Game.Spawner.Spawned)
            {
                counts.TryGetValue(fish.SchoolId, out int member); counts[fish.SchoolId] = member + 1;
                keys.Add(fish.InstanceId, fish.Definition.speciesId + ":" + fish.SchoolId + ":" + member);
            }
            return keys;
        }
        private static void Describe(StringBuilder report, int seed, string route, Measurement sample)
        {
            report.AppendLine("seed " + seed + " " + route + " pickup " + sample.Index + " x " + sample.Position.x.ToString("0.00") +
                " depth " + (-sample.Position.y).ToString("0.0") + " reached " + sample.Collected + " pulses " + sample.Pulses +
                " fresh " + sample.Fresh.Count + " first " + sample.First.Count + " end " + sample.EndState);
            foreach (string pulse in sample.Evidence) report.AppendLine("  " + pulse);
        }
        private static void Summarize(string route, List<Measurement> samples)
        {
            for (int index = 0; index < 2; index++)
            {
                var group = samples.Where(p => p.Index == index).ToArray(); var reached = group.Where(p => p.Collected).ToArray();
                TestContext.WriteLine(route + " pickup " + index + " planned " + group.Length + " reached " + reached.Length +
                    " unreached " + (group.Length - reached.Length) + " empty " + reached.Count(p => p.Fresh.Count == 0) +
                    " firstHits " + reached.Count(p => p.First.Count > 0) + " incomplete " + reached.Count(p => p.Pulses < 3));
            }
        }
        private sealed class Measurement
        {
            public int Index, Id, Pulses;
            public Vector3 Position;
            public bool Collected;
            public double CollectedAt;
            public RunState EndState;
            public readonly HashSet<string> Fresh = new HashSet<string>(), First = new HashSet<string>();
            public readonly List<string> Evidence = new List<string>();
        }
    }
}
