using System;
using System.Linq;
using Fishing.Core;
using NUnit.Framework;

namespace Fishing.Tests
{
    /// <summary>Star-based branch allocation remains independent of sonar size classification.</summary>
    public sealed class OneStarBranchRulesTests
    {
        private static void Step(FishingFlowController flow,float dt,params ContactCandidate[] hits)
        {
            long step=flow.BeginStep(dt);
            flow.ResolveContacts(new ContactBatch(flow.Run.RunId,step,hits,hits.Select(h=>new ContactKey(h.HookId,h.FishId))));
        }
        private static FishingFlowController Ascent(FishRules fish,bool risk=false)
        {
            var flow=new FishingFlowController(new FishingRules(slowdownDistance:0,countdownSeconds:0),new[] {fish},branches:new BranchHookRules(breakOnLarge:risk));
            flow.Start(); Step(flow,.02f); Step(flow,100f/6); Step(flow,1.2f);
            Assert.That(flow.Run.State,Is.EqualTo(RunState.Ascending)); Assert.That(flow.Abilities.Branches.Activate(),Is.True);
            return flow;
        }

        [TestCase(1,true,true)] [TestCase(1,false,true)]
        [TestCase(2,true,false)] [TestCase(2,false,false)]
        [TestCase(5,true,false)] [TestCase(5,false,false)]
        public void BranchEligibilityUsesStarsRegardlessOfSmallFlag(int stars,bool small,bool expected)
        {
            var fish=new FishRules("target","Target",10,7,small,rarity:stars); var flow=Ascent(fish);
            Assert.That(flow.Abilities.Branches.CanReceive(1,fish),Is.EqualTo(expected));
            Step(flow,.02f,new ContactCandidate(1,10,"target",0));
            Assert.That(flow.Run.BranchCaptureCount,Is.EqualTo(expected ? 1 : 0));
        }
        [Test]
        public void OneStarLargeFishSurvivesLegacyRiskAndCommitsOnlyOnceAcrossAllHooks()
        {
            var flow=Ascent(new FishRules("large-one","Large one star",10,10,rarity:1),true);
            var hits=new[] {new ContactCandidate(0,5,"large-one",0),new ContactCandidate(2,5,"large-one",0),new ContactCandidate(1,5,"large-one",0)};
            Step(flow,.02f,hits); Step(flow,.02f,hits);
            Assert.That(flow.Run.Captures.Count,Is.EqualTo(1)); Assert.That(flow.Run.Captures[0].HookId,Is.EqualTo(1));
            Assert.That(flow.Run.Gold,Is.EqualTo(10)); Assert.That(flow.Run.MainCaptureCount,Is.Zero);
            Assert.That(flow.Abilities.Branches.Model.IsActive(1),Is.True);
        }
        [Test]
        public void OneStarStillHonoursTheUncatchableFlag()
        {
            var fish=new FishRules("decor","Decorative",0,0,false,false,1); var flow=Ascent(fish);
            Assert.That(flow.Abilities.Branches.CanReceive(1,fish),Is.False);
            Step(flow,.02f,new ContactCandidate(1,7,"decor",0)); Assert.That(flow.Run.TotalCaptureCount,Is.Zero);
        }
        [Test]
        public void SonarStillUsesSmallFlagRatherThanStars()
        {
            var flow=new FishingFlowController(new FishingRules(countdownSeconds:0),new[] {
                new FishRules("large-one","One",1,1,rarity:1),new FishRules("small-two","Two",1,1,true,rarity:2)});
            flow.Start(); Step(flow,.02f); Assert.That(flow.Abilities.Sonar.Activate(),Is.True);
            var commands=flow.Abilities.Sonar.ResolveTargets(new[] {new SonarTarget(1,"large-one",0,1),new SonarTarget(2,"small-two",0,1)});
            CollectionAssert.AreEqual(new[] {2},commands.Select(c=>c.FishId));
        }
        [TestCase(0)] [TestCase(6)]
        public void InvalidStarSnapshotsAreRejected(int stars)
        { Assert.Throws<ArgumentException>(()=>new FishRules("invalid","Invalid",1,1,rarity:stars)); }
    }
}
