using System.Collections;
using System.Linq;
using System.Reflection;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Reproduces puffer body/head contact, facing changes, sweeps and capacity restrictions in the saved scene.</summary>
    public sealed class PufferCaptureIntegrationTests : AbilityTestScene
    {
        private FishDefinition Puffer => Resources.FindObjectsOfTypeAll<FishDefinition>().Single(f => f.speciesId == "puffer");
        private Vector3 NextHead => Game.Hook.transform.position + Vector3.up * .13f;

        private FishActor SpawnPuffer(Vector3 position, float direction, float speed = 0, Vector2? range = null)
        { return Game.Spawner.SpawnOne(Puffer, position, direction, 4.5f, speed, range); }

        /// <summary>The authored circle decides body-centre overlap; its exterior rejects and its centre catches for both facings.</summary>
        [UnityTest]
        public IEnumerator AuthoredCircleDecidesBodyOverlapAndHeadContactForBothFacings()
        {
            Until(RunState.Ascending);
            int count = 0;
            foreach (float direction in new[] { 1f, -1f })
            {
                var fish = SpawnPuffer(NextHead, direction);
                float radius = Game.Hook.Radius + HeadRadius(fish);
                bool bodyPointInsideCircle = Vector3.Distance(HeadCentre(fish), NextHead) < radius;
                Steps(1);
                Assert.That(fish.IsCaptured, Is.EqualTo(bodyPointInsideCircle));
                if (fish.IsCaptured) count++;
                Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(count));
                if (!fish.IsCaptured) { Align(fish, NextHead); Steps(1); count++; }
                Assert.That(fish.IsCaptured, Is.True);
                Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(count));
                Assert.That(Game.Run.Captures.Last().SpeciesId, Is.EqualTo("puffer"));
                var exterior = SpawnPuffer(NextHead,direction);
                Align(exterior,NextHead+Vector3.right*direction*(radius+.08f)); Steps(1);
                Assert.That(exterior.IsCaptured,Is.False,"A point outside the authored circle must still reject.");
                Align(exterior,NextHead); Steps(1); count++;
                Assert.That(exterior.IsCaptured,Is.True); Assert.That(Game.Run.MainCaptureCount,Is.EqualTo(count));
            }
            yield return null;
        }

        /// <summary>Sweeping the main hook past a moving puffer catches it even with no overlap at either endpoint.</summary>
        [UnityTest]
        public IEnumerator SweptHeadContactCatchesMovingPuffersBetweenEndpoints()
        {
            Until(RunState.Ascending);
            // This case checks the full-speed swept path; launch continuity is checked separately.
            Steps(20);
            int count = 0;
            foreach (float direction in new[] { 1f, -1f })
            {
                Vector3 previous = Game.Hook.transform.position;
                var fish = SpawnPuffer(previous, direction, .3f);
                Align(fish, previous + Vector3.up * .65f);
                float radius = Game.Hook.Radius + HeadRadius(fish);
                Assert.That(Vector3.Distance(HeadCentre(fish), previous), Is.GreaterThan(radius));
                Game.AdvanceGameplay(.2f); count++;
                Assert.That(fish.IsCaptured, Is.True);
                Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(count));
                Assert.That(Game.Hook.transform.position.y - previous.y, Is.EqualTo(1.3f).Within(.001));
                Assert.That(.65f, Is.GreaterThan(radius), "The target also lies beyond the destination overlap.");
            }
            yield return null;
        }

        /// <summary>A puffer reversing at its swim boundary keeps its head on the new visible facing.</summary>
        [UnityTest]
        public IEnumerator BoundaryFlipMovesThePufferHeadToTheNewFacingBeforeDetection()
        {
            Until(RunState.Ascending);
            Vector3 centre = NextHead;
            var authored = HeadCentre(Puffer.prefab) - Puffer.prefab.transform.position;
            var position = centre + new Vector3(authored.x, -authored.y);
            var fish = SpawnPuffer(position, 1, .3f, new Vector2(position.x, position.x));
            Assert.That(HeadCentre(fish).x, Is.GreaterThan(centre.x + authored.x));
            Steps(1);
            Assert.That(fish.IsCaptured, Is.True);
            Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(1));
            yield return null;
        }

        /// <summary>A detected puffer is rejected by a full main hook even while the branches still have six free slots.</summary>
        [UnityTest]
        public IEnumerator FullMainRejectsDetectedPufferWhileBranchesCanStillCatchSmallFish()
        {
            Until(RunState.Ascending);
            for (int i = 0; i < 10; i++) AtHead(Blue, NextHead);
            Steps(1); Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(10));
            Game.DeployBranches(); Steps(30);
            var fish = AtHead(Puffer, Game.Hook.transform.position + Vector3.up * .325f);
            Steps(1);
            var detector = (ContactDetector)typeof(GameBootstrap).GetField("_detector", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Game);
            Assert.That(detector.LastReportCount, Is.GreaterThan(0), "The main query reported contact; capacity rejects it in Core.");
            Assert.That(fish.IsCaptured, Is.False);
            Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(10));
            Assert.That(Game.Run.BranchCaptureCount, Is.Zero);
            var small = AtHead(Small, Game.AbilityView.Branches[0].transform.position + Vector3.up * .325f);
            Steps(1);
            Assert.That(small.IsCaptured, Is.True);
            Assert.That(Game.Run.BranchCaptureCount, Is.EqualTo(1));
            yield return null;
        }

        /// <summary>The one-star puffer can be caught by either hook, retaining the real source on each record.</summary>
        [UnityTest]
        public IEnumerator OneStarPufferCanBeCaughtByABranchAndByTheMainHook()
        {
            Until(RunState.Ascending); Game.DeployBranches(); Steps(30);
            var branch = Game.AbilityView.Branches[0];
            var fish = AtHead(Puffer, branch.transform.position + Vector3.up * .13f);
            // Check overlap before capture attaches the fish to its hanging anchor.
            Assert.That(Vector3.Distance(HeadCentre(fish), branch.transform.position),
                Is.LessThan(branch.Radius + HeadRadius(fish)));
            Steps(1);
            Assert.That(fish.IsCaptured, Is.True);
            Assert.That(Game.Run.Captures.Single().Source, Is.EqualTo(CaptureSource.Branch));
            Assert.That(Game.Run.MainCaptureCount, Is.Zero);
            var mainFish = AtHead(Puffer, NextHead); Steps(1);
            Assert.That(mainFish.IsCaptured, Is.True); Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(1));
            Assert.That(Game.Run.BranchCaptureCount, Is.EqualTo(1));
            Assert.That(Game.Run.Captures.Single(r=>r.FishInstanceId==mainFish.InstanceId).Source, Is.EqualTo(CaptureSource.Main));
            yield return null;
        }
    }
}
