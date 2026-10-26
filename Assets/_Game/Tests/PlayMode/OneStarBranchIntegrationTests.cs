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
    /// <summary>Exercises the saved species' real head shapes on both original fish layers.</summary>
    public sealed class OneStarBranchIntegrationTests : AbilityTestScene
    {
        [UnityTest]
        public IEnumerator EverySavedOneStarSpeciesIncludingBlueAndPufferIsCapturedByABranch()
        {
            var species=(FishDefinition[])typeof(GameBootstrap).GetField("species",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Game);
            var oneStar=species.Where(f=>f.rarity==1).ToArray();
            CollectionAssert.Contains(oneStar,Blue); Assert.That(oneStar.Any(f=>f.speciesId=="puffer"),Is.True);
            foreach(var definition in oneStar)
            {
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending); Game.DeployBranches(); Steps(30);
                var branch=Game.AbilityView.Branches[0];
                var fish=AtHead(definition,branch.transform.position+Vector3.up*.1f); Steps(1);
                Assert.That(fish.IsCaptured,Is.True,definition.speciesId);
                var record=Game.Run.Captures.Single(); Assert.That(record.Source,Is.EqualTo(CaptureSource.Branch));
                Assert.That(record.HookId,Is.EqualTo(branch.HookId)); Assert.That(record.SpeciesId,Is.EqualTo(definition.speciesId));
                Assert.That(Game.Run.MainCaptureCount,Is.Zero); Assert.That(Game.Run.Gold,Is.EqualTo(definition.gold));
                Assert.That(Game.Run.Shells,Is.EqualTo(definition.shells));
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator EverySavedHigherStarSpeciesPassesBranchesAndIsStillCaughtByTheMainHook()
        {
            var species=(FishDefinition[])typeof(GameBootstrap).GetField("species",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Game);
            foreach(var definition in species.Where(f=>f.rarity>1))
            {
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending); Game.DeployBranches(); Steps(30);
                var branch=Game.AbilityView.Branches[0]; var fish=AtHead(definition,branch.transform.position+Vector3.up*.1f); Steps(1);
                Assert.That(fish.IsCaptured,Is.False,definition.speciesId); Assert.That(Game.Run.BranchCaptureCount,Is.Zero);
                Align(fish,Game.Hook.transform.position); Steps(1); Assert.That(fish.IsCaptured,Is.True);
                Assert.That(Game.Run.Captures.Single().Source,Is.EqualTo(CaptureSource.Main));
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator OneStarBlueIsSweptBetweenTwoNonOverlappingEndpointsAtFullMainSpeed()
        {
            Until(RunState.Ascending);
            AtHead(Small,Game.Hook.transform.position); for(int i=0;i<9;i++) AtHead(Blue,Game.Hook.transform.position);
            Steps(1); Assert.That(Game.Run.IsFull,Is.True); Game.DeployBranches(); Steps(20);
            var branch=Game.AbilityView.Branches[0]; var start=branch.transform.position;
            var fish=AtHead(Blue,start+Vector3.up);
            float radius=branch.Radius+HeadRadius(fish),travel=Mathf.Max(1.3f,2*radius+.4f);
            Align(fish,start+Vector3.up*(travel*.5f)); var centre=HeadCentre(fish);
            Assert.That(Vector3.Distance(start,centre),Is.GreaterThan(radius));
            var config=(FishingConfig)typeof(GameBootstrap).GetField("config",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Game);
            Game.AdvanceGameplay(travel/(Game.Run.Rules.AscentSpeed*Game.Run.Rules.FullMultiplier*config.unitsPerMetre));
            Assert.That(Vector3.Distance(branch.transform.position,centre),Is.GreaterThan(radius));
            Assert.That(fish.IsCaptured,Is.True); Assert.That(Game.Run.MainCaptureCount,Is.EqualTo(10));
            Assert.That(Game.Run.Captures.Single(r=>r.FishInstanceId==fish.InstanceId).Source,Is.EqualTo(CaptureSource.Branch));
            yield return null;
        }
        [UnityTest]
        public IEnumerator StarSnapshotDoesNotChangeWhenTheSourceAssetChangesDuringARun()
        {
            int original=Blue.rarity;
            try
            {
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending); Game.DeployBranches(); Steps(30);
                Blue.rarity=2;
                var fish=AtHead(Blue,Game.AbilityView.Branches[0].transform.position); Steps(1);
                Assert.That(fish.IsCaptured,Is.True,"The already-started run keeps its one-star snapshot.");
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending); Game.DeployBranches(); Steps(30);
                fish=AtHead(Blue,Game.AbilityView.Branches[0].transform.position); Steps(1);
                Assert.That(fish.IsCaptured,Is.False,"The next run must use the new star snapshot.");
            }
            finally { Blue.rarity=original; }
            yield return null;
        }
    }
}
