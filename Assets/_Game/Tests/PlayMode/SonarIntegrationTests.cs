using System.Collections;
using System.Linq;
using Fishing.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Fishing.Tests
{
    /// <summary>Actual layer queries, disabled heads, continuous escape and paused sonar presentation.</summary>
    public sealed class SonarIntegrationTests : AbilityTestScene
    {
        [UnityTest]
        public IEnumerator SonarScaresOnlyInRangeSmallHeadsWhileLargeFishStillSpendsProtection()
        {
            Until(RunState.Diving); var origin = Game.Hook.transform.position;
            var small = AtHead(Small, origin + Vector3.down * .12f, 1);
            var silver = AtHead(Silver, origin + Vector3.right * 3, 1);
            var large = AtHead(Blue, origin + Vector3.left * 2);
            var distant = AtHead(Small, origin + Vector3.right * 4.3f);
            Click("Sonar button");
            Assert.That(Game.Abilities.Sonar.RemainingUses, Is.EqualTo(1)); Assert.That(small.IsScared && silver.IsScared, Is.True);
            Assert.That(small.Head.enabled || silver.Head.enabled, Is.False); Assert.That(large.IsScared || distant.IsScared, Is.False);
            Assert.That(Game.AbilityView.Wave.enabled, Is.True); Steps(1);
            Assert.That(Game.Run.Protection, Is.EqualTo(2)); Assert.That(Game.Run.MainCaptureCount, Is.Zero);
            Assert.That(Game.Run.State, Is.EqualTo(RunState.Diving));
            Align(large, Game.Hook.transform.position + Vector3.down * .12f); Steps(1);
            Assert.That(Game.Run.Protection, Is.EqualTo(1)); Assert.That(large.IsCaptured, Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator PausingFreezesCooldownEscapeAndWaveAndRejectsActivation()
        {
            Until(RunState.Diving); var fish = AtHead(Small, Game.Hook.transform.position + Vector3.right, 1);
            Click("Sonar button"); Steps(10);
            double time = Game.Run.GameTime; float cooldown = Game.Abilities.Sonar.CooldownRemaining, scared = fish.ScaredRemaining;
            var position = fish.transform.position; var ring = Game.AbilityView.Wave.GetPosition(16);
            Click("Pause button"); Click("Sonar button"); Steps(200);
            Assert.That(Game.Run.GameTime, Is.EqualTo(time)); Assert.That(Game.Abilities.Sonar.RemainingUses, Is.EqualTo(1));
            Assert.That(Game.Abilities.Sonar.CooldownRemaining, Is.EqualTo(cooldown)); Assert.That(fish.ScaredRemaining, Is.EqualTo(scared));
            Assert.That(fish.transform.position, Is.EqualTo(position)); Assert.That(Game.AbilityView.Wave.GetPosition(16), Is.EqualTo(ring));
            Click("Resume"); Steps(1); Assert.That(Game.Abilities.Sonar.CooldownRemaining, Is.LessThan(cooldown));
            Assert.That(fish.transform.position.x, Is.GreaterThan(position.x)); yield return null;
        }
        [UnityTest]
        public IEnumerator RecoveryKeepsDisplacedPositionRestoresHeadAndAllowsAscentCapture()
        {
            Until(RunState.Diving); var fish = AtHead(Small, Game.Hook.transform.position + Vector3.right, 1, new Vector2(.5f, 1.1f));
            Click("Sonar button"); var previous = fish.transform.position;
            for (int i = 0; i < 127; i++)
            {
                Steps(1); Assert.That(Vector3.Distance(previous, fish.transform.position), Is.LessThanOrEqualTo(.061f));
                previous = fish.transform.position;
            }
            Assert.That(fish.IsScared, Is.False); Assert.That(fish.Head.enabled, Is.True); Assert.That(fish.transform.position.x, Is.GreaterThan(1.1f));
            Until(RunState.Ascending); Align(fish, Game.Hook.transform.position + Vector3.up * .13f); Steps(1);
            Assert.That(fish.IsCaptured, Is.True); Assert.That(Game.Run.Captures.Single().Source, Is.EqualTo(CaptureSource.Main));
            yield return null;
        }
        [UnityTest]
        public IEnumerator SonarUsesResetWithPooledFishAndButtonLocksAtTheBottomAndOnAscent()
        {
            Assert.That(GameObject.Find("Sonar button").GetComponent<Button>().interactable, Is.False);
            Until(RunState.Diving); var fish = AtHead(Small, Game.Hook.transform.position + Vector3.right, 1);
            Click("Sonar button"); Click("Sonar button"); Assert.That(Game.Abilities.Sonar.RemainingUses, Is.EqualTo(1));
            Steps(201); Click("Sonar button"); Assert.That(Game.Abilities.Sonar.RemainingUses, Is.Zero);
            var old = Game.Run.RunId; Game.BeginRun();
            Assert.That(Game.Run.RunId, Is.Not.EqualTo(old)); Assert.That(Game.Abilities.Sonar.RemainingUses, Is.EqualTo(2));
            Assert.That(Game.Spawner.Spawned.All(f => !f.IsScared && f.Head.enabled), Is.True); Assert.That(Game.AbilityView.Wave.enabled, Is.False);
            Game.Spawner.Clear(); Until(RunState.Diving);
            while (Game.Run.Depth < 96) Steps(1);
            Assert.That(GameObject.Find("Sonar button").GetComponent<Button>().interactable, Is.False);
            Click("Sonar button"); Assert.That(Game.Abilities.Sonar.RemainingUses, Is.EqualTo(2));
            Until(RunState.BottomHold); Assert.That(Game.Abilities.Sonar.CanActivate(Game.Run), Is.False);
            Until(RunState.Ascending); Assert.That(Game.Abilities.Sonar.CanActivate(Game.Run), Is.False);
            Assert.That(GameObject.Find("Branch button").GetComponent<Button>().interactable, Is.True); yield return null;
        }
    }
}
