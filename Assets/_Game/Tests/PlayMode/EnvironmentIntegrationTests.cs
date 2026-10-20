using System.Collections;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Saved-scene environment, rope/pose alignment and camera transition acceptance.</summary>
    public sealed class EnvironmentIntegrationTests
    {
        private GameBootstrap _game;
        /// <summary>Loads the saved environment and pauses automatic gameplay stepping for acceptance.</summary>
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main"); yield return null; yield return null;
            _game = Object.FindObjectOfType<GameBootstrap>(); _game.enabled = false; _game.Spawner.Clear();
            Assert.That(_game.SceneView, Is.Not.Null); Assert.That(_game.SceneView.HasBindings, Is.True);
        }
        /// <summary>Checks scene teardown for unexpected errors.</summary>
        [UnityTearDown]
        public IEnumerator LeaveScene()
        { SceneManager.LoadScene("Menu"); yield return null; yield return null; LogAssert.NoUnexpectedReceived(); }

        private void Until(RunState state)
        {
            for (int i = 0; i < 4000 && _game.Run.State != state; i++) _game.AdvanceGameplay(.02f);
            Assert.That(_game.Run.State, Is.EqualTo(state));
        }

        /// <summary>Checks wall coverage, one environment instance, pose anchors and all rope endpoints.</summary>
        [UnityTest]
        public IEnumerator SavedEnvironmentHasContinuousWallsAlignedPosesAndRopeThroughTheHole()
        {
            var view = _game.SceneView; var actor = view.Fisherman;
            Vector3 feet = actor.transform.position; var idle = actor.sprite;
            Assert.That(view.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
            foreach (string side in new[] { "Left ", "Right " })
            {
                int tiles = 0; SpriteRenderer previous = null;
                while (view.transform.Find("Underwater/Walls/" + side + tiles.ToString("00")) != null)
                {
                    var wall = view.transform.Find("Underwater/Walls/" + side + tiles.ToString("00")).GetComponent<SpriteRenderer>();
                    if (previous != null) Assert.That(wall.bounds.max.y - previous.bounds.min.y, Is.GreaterThan(.09f));
                    previous = wall; tiles++;
                }
                Assert.That(tiles, Is.GreaterThan(24)); Assert.That(previous.bounds.min.y, Is.LessThan(-120));
            }
            Until(RunState.Diving); for (int i = 0; i < 25; i++) _game.AdvanceGameplay(.02f);
            var rope = _game.Hook.transform.Find("Rope").GetComponent<LineRenderer>();
            float previewPixels = rope.endWidth * 450 / (2 * Camera.main.orthographicSize * Camera.main.aspect);
            Assert.That(previewPixels, Is.GreaterThanOrEqualTo(1.4f), "A subpixel rope can vanish between pixel centres.");
            Assert.That(rope.positionCount, Is.EqualTo(3));
            Assert.That(Vector3.Distance(rope.GetPosition(0), view.RodTip.position), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(rope.GetPosition(1), view.LineGuide.position), Is.LessThan(.001f));
            Until(RunState.Ascending);
            Assert.That(actor.sprite, Is.Not.EqualTo(idle));
            Assert.That(actor.transform.position, Is.EqualTo(feet));
            Assert.That(actor.sprite.pivot, Is.EqualTo(idle.pivot));
            Assert.That(actor.sprite.pixelsPerUnit, Is.EqualTo(idle.pixelsPerUnit));
            Assert.That(Vector3.Distance(rope.GetPosition(0), view.RodTip.position), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(rope.GetPosition(rope.positionCount - 1),
                _game.Hook.transform.Find("Rope Anchor").position), Is.LessThan(.001f));
            _game.BeginRun();
            Assert.That(actor.sprite, Is.EqualTo(idle)); Assert.That(actor.transform.position, Is.EqualTo(feet));
            Assert.That(Object.FindObjectsOfType<FishingSceneView>().Length, Is.EqualTo(1));
            yield return null;
        }

        /// <summary>Checks stable portrait width, half-second direction blending, pause and surface continuity.</summary>
        [UnityTest]
        public IEnumerator CameraMatchesThreePortraitWidthsBlendsDirectionAndReturnsWithoutASurfaceJump()
        {
            var camera = Camera.main; var rig = _game.CameraRig;
            Until(RunState.Diving);
            for (int i = 0; i < 200; i++) _game.AdvanceGameplay(.02f);
            foreach (int height in new[] { 800, 975, 1050 })
            {
                camera.aspect = 450f / height; rig.Follow(_game.Run, .5f);
                Assert.That(camera.orthographicSize * camera.aspect, Is.EqualTo(6).Within(.001f));
                Assert.That(camera.WorldToViewportPoint(_game.Hook.transform.position).y, Is.EqualTo(.8f).Within(.001f));
            }
            Until(RunState.BottomHold); rig.Follow(_game.Run, .5f);
            Vector3 before = camera.transform.position;
            Until(RunState.Ascending); rig.Follow(_game.Run, 0);
            Assert.That(Vector3.Distance(camera.transform.position, before), Is.LessThan(.001f));
            rig.Follow(_game.Run, .25f);
            Assert.That(camera.WorldToViewportPoint(_game.Hook.transform.position).y, Is.EqualTo(.5f).Within(.001f));
            rig.Follow(_game.Run, .25f);
            Assert.That(camera.WorldToViewportPoint(_game.Hook.transform.position).y, Is.EqualTo(.2f).Within(.001f));
            before = camera.transform.position; _game.PauseRun(); rig.Follow(_game.Run, 1);
            Assert.That(camera.transform.position, Is.EqualTo(before)); _game.ResumeRun();
            for (int i = 0; i < 4000 && _game.Run.State != RunState.Surfacing; i++)
            {
                before = camera.transform.position; _game.AdvanceGameplay(.02f); rig.Follow(_game.Run, .02f);
                if (_game.Run.State == RunState.Surfacing)
                    Assert.That(Vector3.Distance(camera.transform.position, before), Is.LessThan(.001f));
            }
            Assert.That(_game.Run.State, Is.EqualTo(RunState.Surfacing));
            Assert.That(camera.transform.position.y, Is.EqualTo(-camera.orthographicSize * .4f).Within(.001f));
            camera.ResetAspect(); yield return null;
        }
    }
}
