using System.Collections;
using System.Collections.Generic;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Fishing.Tests
{
    /// <summary>Saved-scene acceptance using actual prefabs, transforms and Physics2D simulation.</summary>
    public sealed class GameIntegrationTests
    {
        private GameBootstrap _game;
        private FishDefinition _species;
        private readonly List<FishDefinition> _definitions = new List<FishDefinition>();
        private bool _hadTutorialPreference;
        private int _tutorialPreference;
        private ReelingComparisonScope _comparison;
        [UnitySetUp]
        public IEnumerator LoadSavedMainScene()
        {
            _hadTutorialPreference = PlayerPrefs.HasKey(GameUi.TutorialPreferenceKey);
            _tutorialPreference = PlayerPrefs.GetInt(GameUi.TutorialPreferenceKey);
            SceneManager.LoadScene("Main"); yield return null; yield return null;
            _game = Object.FindObjectOfType<GameBootstrap>();
            Assert.That(_game, Is.Not.Null); Assert.That(_game.HasBindings, Is.True);
            _game.enabled = false; _comparison = new ReelingComparisonScope(false); _game.BeginRun();
            _species = _game.Spawner.Spawned[0].Definition;
            _definitions.Clear();
            foreach (var fish in _game.Spawner.Spawned)
                if (!_definitions.Contains(fish.Definition)) _definitions.Add(fish.Definition);
            _game.Spawner.Clear();
        }
        [UnityTearDown]
        public IEnumerator LeaveScene()
        {
            _comparison?.Dispose();
            SceneManager.LoadScene("Menu"); yield return null; yield return null;
            if (_hadTutorialPreference) PlayerPrefs.SetInt(GameUi.TutorialPreferenceKey, _tutorialPreference);
            else PlayerPrefs.DeleteKey(GameUi.TutorialPreferenceKey);
            PlayerPrefs.Save();
            LogAssert.NoUnexpectedReceived();
        }
        private void Until(RunState state)
        {
            for (int i = 0; i < 4000 && _game.Run.State != state; i++) _game.AdvanceGameplay(.02f);
            Assert.That(_game.Run.State, Is.EqualTo(state));
        }
        private FishActor SpawnAtHead(Vector3 centre)
        {
            var fish = _game.Spawner.SpawnOne(_species, centre, 1, 4.5f);
            AlignHead(fish, centre); return fish;
        }
        private static void AlignHead(FishActor fish, Vector3 centre)
        { fish.transform.position += centre - fish.Head.transform.TransformPoint(fish.Head.offset); }
        private Vector3 NextDiveHead => _game.Hook.transform.position + Vector3.down * .12f;

        [UnityTest]
        public IEnumerator NormalBottomPathReachesResultAndRestartCreatesFreshState()
        {
            var oldId = _game.Run.RunId;
            Until(RunState.BottomHold);
            Assert.That(_game.Run.Depth, Is.EqualTo(100));
            Assert.That(_game.Hook.transform.position.y, Is.EqualTo(-100).Within(.001));
            Until(RunState.Ascending); Until(RunState.Result);
            Assert.That(_game.Run.ReachedDepth, Is.EqualTo(100));
            Assert.That(GameObject.Find("Result"), Is.Not.Null);
            _game.BeginRun();
            Assert.That(_game.Run.RunId, Is.Not.EqualTo(oldId));
            Assert.That(_game.Run.State, Is.EqualTo(RunState.Countdown));
            Assert.That(_game.Run.Protection, Is.EqualTo(2)); Assert.That(_game.Run.TotalCaptureCount, Is.Zero);
            Assert.That(_game.Spawner.ActiveCount, Is.EqualTo(54));
            yield return null;
        }
        [UnityTest]
        public IEnumerator RealContinuousOverlapDoesNotRepeatProtectionThenReentryDoes()
        {
            Until(RunState.Diving); var fish = SpawnAtHead(NextDiveHead);
            for (int i = 0; i < 80; i++)
            { AlignHead(fish, NextDiveHead); _game.AdvanceGameplay(.02f); }
            Assert.That(_game.Run.Protection, Is.EqualTo(1)); Assert.That(fish.IsCaptured, Is.False);
            AlignHead(fish, _game.Hook.transform.position + Vector3.right * 3);
            _game.AdvanceGameplay(.02f);
            AlignHead(fish, NextDiveHead); _game.AdvanceGameplay(.02f);
            Assert.That(_game.Run.Protection, Is.Zero); yield return null;
        }
        [UnityTest]
        public IEnumerator SweptFishBetweenEndpointsTriggersUnprotectedEarlyAscent()
        {
            Until(RunState.Diving);
            for (int i = 0; i < 2; i++)
            {
                SpawnAtHead(NextDiveHead); _game.AdvanceGameplay(.02f); _game.Spawner.Clear();
                for (int tick = 0; tick < 60; tick++) _game.AdvanceGameplay(.02f);
            }
            Assert.That(_game.Run.Protection, Is.Zero);
            var before = _game.Hook.transform.position;
            var fish = SpawnAtHead(before + Vector3.down * .6f);
            Assert.That(Vector3.Distance(fish.Head.transform.position, before), Is.GreaterThan(.42f));
            _game.AdvanceGameplay(.2f);
            Assert.That(_game.Run.State, Is.EqualTo(RunState.Ascending));
            Assert.That(_game.Run.TotalCaptureCount, Is.EqualTo(1)); Assert.That(fish.IsCaptured, Is.True);
            Assert.That(_game.AppliedCaptureEvents, Is.EqualTo(1));
            Assert.That(fish.Head.enabled, Is.False); Assert.That(fish.transform.parent, Is.EqualTo(_game.Hook.transform));
            Until(RunState.Result); Assert.That(_game.Run.ReachedDepth, Is.LessThan(100)); yield return null;
        }
        [UnityTest]
        public IEnumerator ActualMainHookFullStopsCaptureAndAcceleratesOnlyOnAscent()
        {
            Until(RunState.Ascending);
            for (int i = 0; i < 12; i++) SpawnAtHead(_game.Hook.transform.position + Vector3.up * .13f);
            _game.AdvanceGameplay(.02f);
            Assert.That(_game.Run.MainCaptureCount, Is.EqualTo(10));
            Assert.That(_game.AppliedCaptureEvents, Is.EqualTo(10)); Assert.That(_game.Spawner.ActiveCount, Is.EqualTo(2));
            float depth = _game.Run.Depth; _game.AdvanceGameplay(.02f);
            Assert.That(depth - _game.Run.Depth, Is.InRange(.00001f,.1f), "Full load must still ease away from the bottom.");
            for (int i = 0; i < 20; i++) _game.AdvanceGameplay(.02f);
            depth = _game.Run.Depth; _game.AdvanceGameplay(.02f);
            Assert.That(depth - _game.Run.Depth, Is.EqualTo(.65f).Within(.001));
            Assert.That(_game.Run.MainCaptureCount, Is.EqualTo(10)); yield return null;
        }
        [UnityTest]
        public IEnumerator PauseFreezesActualFishAndHookInAllThreePlayableStates()
        {
            foreach (var state in new[] { RunState.Diving, RunState.BottomHold, RunState.Ascending })
            {
                _game.BeginRun(); _game.Spawner.Clear(); Until(state);
                var fish = SpawnAtHead(_game.Hook.transform.position + Vector3.right * 3);
                var hookPosition = _game.Hook.transform.position; var fishPosition = fish.transform.position;
                double time = _game.Run.GameTime;
                _game.PauseRun(); Assert.That(GameObject.Find("Pause"), Is.Not.Null);
                for (int i = 0; i < 100; i++) _game.AdvanceGameplay(.02f);
                Assert.That(_game.Run.GameTime, Is.EqualTo(time));
                Assert.That(_game.Hook.transform.position, Is.EqualTo(hookPosition)); Assert.That(fish.transform.position, Is.EqualTo(fishPosition));
                GameObject.Find("Resume").GetComponent<Button>().onClick.Invoke();
                _game.AdvanceGameplay(.02f); Assert.That(_game.Run.GameTime, Is.GreaterThan(time));
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator TwentySceneRestartsReuseViewsAndDoNotDuplicateCaptureSubscriptions()
        {
            int allocated = _game.Spawner.AllocatedCount; int previousSpawn = 0;
            for (int run = 0; run < 20; run++)
            {
                _game.BeginRun(); Assert.That(_game.AppliedCaptureEvents, Is.Zero);
                Assert.That(_game.Hook.GetComponentsInChildren<FishActor>(true).Length, Is.Zero);
                _game.Spawner.Clear(); Until(RunState.Ascending);
                var fish = SpawnAtHead(_game.Hook.transform.position + Vector3.up * .13f);
                Assert.That(fish.InstanceId, Is.GreaterThan(previousSpawn)); previousSpawn = fish.InstanceId;
                _game.AdvanceGameplay(.02f);
                Assert.That(_game.AppliedCaptureEvents, Is.EqualTo(1));
                Assert.That(_game.Run.TotalCaptureCount, Is.EqualTo(1));
                Assert.That(_game.Spawner.AllocatedCount, Is.EqualTo(allocated));
                Assert.That(Object.FindObjectsOfType<FishActor>(true).Length, Is.EqualTo(allocated));
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator MenuStartAndResultExitButtonsLoadTheSavedScenes()
        {
            SceneManager.LoadScene("Menu"); yield return null; yield return null;
            Assert.That(Object.FindObjectOfType<GameBootstrap>().Run, Is.Null);
            GameObject.Find("Start dive").GetComponent<Button>().onClick.Invoke(); yield return null; yield return null;
            var menu = Object.FindObjectOfType<GameBootstrap>();
            if (menu.Ui.IsTutorialOpen)
            { GameObject.Find("Tutorial close").GetComponent<Button>().onClick.Invoke(); yield return null; yield return null; }
            var main = Object.FindObjectOfType<GameBootstrap>(); Assert.That(main.Run.State, Is.EqualTo(RunState.Countdown));
            main.enabled = false; main.Spawner.Clear(); _game = main; Until(RunState.Result);
            GameObject.Find("Back to menu").GetComponent<Button>().onClick.Invoke(); yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
        }

        [UnityTest]
        public IEnumerator ImportedFishOnlyContactAtTheirHeadsAndMirrorWithFacing()
        {
            Assert.That(_definitions.Count, Is.EqualTo(7));
            foreach (var definition in _definitions) foreach (float direction in new[] { -1f, 1f })
            {
                _game.BeginRun(); _game.Spawner.Clear(); Until(RunState.Diving);
                var fish = _game.Spawner.SpawnOne(definition, Vector2.zero, direction, 4.5f);
                var body = fish.transform.Find("Body").GetComponent<SpriteRenderer>();
                Assert.That(body.sprite, Is.SameAs(definition.sprite));
                Assert.That(body.sprite.bounds.size.x, Is.EqualTo(1.25f).Within(.001));
                Assert.That(fish.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(1));
                float authoredX = definition.prefab.Head.transform.TransformPoint(definition.prefab.Head.offset).x - definition.prefab.transform.position.x;
                Assert.That(fish.Head.transform.TransformPoint(fish.Head.offset).x - fish.transform.position.x,
                    Is.EqualTo(authoredX * direction).Within(.001));
                fish.transform.position += NextDiveHead - fish.transform.TransformPoint(new Vector3(-.5f, 0, 0));
                _game.AdvanceGameplay(.02f);
                Assert.That(_game.Run.Protection, Is.EqualTo(2), definition.speciesId + " tail triggered contact");
                AlignHead(fish, NextDiveHead); _game.AdvanceGameplay(.02f);
                Assert.That(_game.Run.Protection, Is.EqualTo(1), definition.speciesId + " head missed contact");
                Assert.That(fish.IsCaptured, Is.False);
                fish.transform.position = new Vector3(direction * 4.5f, fish.transform.position.y, 0);
                fish.Tick(.02f);
                Assert.That((fish.Head.transform.position.x - fish.transform.position.x) * direction, Is.LessThan(0));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoreArtMixedCatchHangsAtMouthReachesResultAndResetsPooledRotation()
        {
            Until(RunState.BottomHold);
            foreach (var definition in _definitions)
            {
                var fish = _game.Spawner.SpawnOne(definition, Vector2.zero, -1, 4.5f);
                AlignHead(fish, _game.Hook.transform.position);
            }
            _game.AdvanceGameplay(.02f);
            Assert.That(_game.Run.MainCaptureCount, Is.EqualTo(7));
            var lamp = _game.Hook.transform.Find("Lamp").GetComponent<SpriteRenderer>();
            Assert.That(lamp.color.g, Is.GreaterThan(lamp.color.r));
            Assert.That(_game.Hook.transform.Find("Protection").GetComponent<SpriteRenderer>().enabled, Is.False);
            var caught = _game.Hook.GetComponentsInChildren<FishActor>();
            Assert.That(caught.Length, Is.EqualTo(7));
            foreach (var fish in caught)
            {
                Assert.That(fish.Head.enabled, Is.False);
                var mouth = fish.transform.TransformPoint(fish.Definition.mouthPosition);
                float nearest = float.MaxValue;
                for (int slot = 0; slot < 10; slot++) nearest = Mathf.Min(nearest,
                    Vector3.Distance(mouth, _game.Hook.transform.TransformPoint(_game.Hook.HangPosition(slot))));
                Assert.That(nearest, Is.LessThan(.001f));
            }
            Until(RunState.Ascending);
            Assert.That(lamp.color.r, Is.GreaterThan(.9f)); Assert.That(lamp.color.g, Is.GreaterThan(.7f));
            Until(RunState.Result);
            Assert.That(_game.Run.MainCaptureCount, Is.EqualTo(7));
            Assert.That(_game.Run.Gold, Is.EqualTo(163));
            Assert.That(_game.Run.Shells, Is.EqualTo(118));
            _game.BeginRun();
            Assert.That(_game.Hook.GetComponentsInChildren<FishActor>().Length, Is.Zero);
            foreach (var fish in _game.Spawner.Spawned)
                Assert.That(Quaternion.Angle(fish.transform.localRotation, Quaternion.identity), Is.LessThan(.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ImportedBubbleTrailMovesOnGameplayTimeAndFreezesDuringPause()
        {
            Until(RunState.Diving);
            var trail = _game.Hook.transform.Find("Bubbles");
            Assert.That(trail.childCount, Is.EqualTo(3));
            var bubble = trail.GetChild(0).GetComponent<SpriteRenderer>();
            Assert.That(bubble.sprite.texture.width, Is.LessThan(200));
            Assert.That(bubble.sprite.texture.height, Is.LessThan(200));
            Assert.That(bubble.enabled, Is.True);
            var before = bubble.transform.localPosition;
            _game.AdvanceGameplay(.1f);
            Assert.That(bubble.transform.localPosition, Is.Not.EqualTo(before));
            before = bubble.transform.localPosition; _game.PauseRun();
            for (int i = 0; i < 20; i++) _game.AdvanceGameplay(.02f);
            Assert.That(bubble.transform.localPosition, Is.EqualTo(before));
            _game.ResumeRun(); _game.AdvanceGameplay(.1f);
            Assert.That(bubble.transform.localPosition, Is.Not.EqualTo(before));
            Until(RunState.BottomHold);
            Assert.That(bubble.enabled, Is.False); yield return null;
        }
    }
}
