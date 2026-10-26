using System.Collections;
using System.Linq;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Fishing.Tests
{
    /// <summary>Actual visual input, paused ambience and camera-boundary checks for imported art details.</summary>
    public sealed class ArtPolishIntegrationTests : AbilityTestScene
    {
        /// <summary>The visual face depresses, cancels and reopens without moving the button's hit rectangle.</summary>
        [UnityTest]
        public IEnumerator ButtonSurfaceMovesWithoutMovingItsHitRectangleAndRecoversFromDisable()
        {
            Game.Ui.ShowMenu(); yield return null;
            var button = GameObject.Find("Start dive").GetComponent<Button>();
            var rect = (RectTransform)button.transform; Vector2 position = rect.anchoredPosition, size = rect.sizeDelta;
            var surface = button.transform.Find("Button surface").GetComponent<RectTransform>();
            Assert.That(button.targetGraphic, Is.TypeOf<IceButtonGraphic>());
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(surface.localScale.x, Is.LessThan(.99f));
            Assert.That(rect.anchoredPosition, Is.EqualTo(position)); Assert.That(rect.sizeDelta, Is.EqualTo(size));
            button.interactable = false; yield return new WaitForSecondsRealtime(.3f);
            Assert.That(surface.localScale.x, Is.GreaterThan(.999f));
            button.gameObject.SetActive(false); button.gameObject.SetActive(true); button.interactable = true;
            Assert.That(surface.localScale, Is.EqualTo(Vector3.one));
            pointer.button = PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(surface.localScale, Is.EqualTo(Vector3.one));
            Game.BeginRun(); Until(RunState.Diving);
            var pause = GameObject.Find("Pause button").GetComponent<Button>();
            var caption = pause.transform.Find("Button surface/Button face/Pause bar 0");
            float captionY = caption.position.y;
            pointer.button = PointerEventData.InputButton.Left;
            ExecuteEvents.Execute(pause.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(caption.position.y, Is.LessThan(captionY), "The pause icon must depress with its button face.");
        }

        /// <summary>Ambient sprites move on the presentation clock, freeze on pause and remain a fixed pool after restart.</summary>
        [UnityTest]
        public IEnumerator BubblesAndDistantFishFreezeAndResetWithoutChangingFishRandomness()
        {
            var details = Game.SceneView.GetComponentInChildren<AmbientWaterDetails>();
            var atmosphere = Game.SceneView.GetComponent<UnderwaterAtmosphere>();
            Assert.That(details.HasBindings, Is.True); Assert.That(details.ParticleCount, Is.EqualTo(30));
            Assert.That(details.GetComponentsInChildren<Collider2D>(), Is.Empty);
            var renderers = details.GetComponentsInChildren<SpriteRenderer>();
            var positions = renderers.Select(r => r.transform.position).ToArray();
            var random = Random.state; atmosphere.Tick(.5f);
            Assert.That(Random.state, Is.EqualTo(random));
            Assert.That(renderers.Where((r, i) => r.transform.position != positions[i]).Count(), Is.GreaterThan(10));
            Until(RunState.Diving); Game.enabled = true; Game.PauseRun();
            positions = renderers.Select(r => r.transform.position).ToArray(); float time = atmosphere.AnimationTime;
            var distant = Game.SceneView.transform.Find("Underwater/Distant Fish").GetChild(0);
            Vector3 fishPosition = distant.position;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(atmosphere.AnimationTime, Is.EqualTo(time)); Assert.That(distant.position, Is.EqualTo(fishPosition));
            for (int i = 0; i < renderers.Length; i++) Assert.That(renderers[i].transform.position, Is.EqualTo(positions[i]));
            Game.enabled = false; Game.ResumeRun();
            for (int i = 0; i < 4; i++) { Game.BeginRun(); atmosphere.Tick(.1f); }
            Assert.That(details.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(30));
            SceneManager.LoadScene("Menu"); yield return null; yield return null;
            Assert.That(details == null && renderers.All(r => r == null), Is.True);
        }

        /// <summary>Three portrait viewports keep every ambient centre inside the water and pool count bounded during travel.</summary>
        [UnityTest]
        public IEnumerator AmbientPoolStaysUnderIceAboveSeabedAndInsideSideWallsAcrossPortraitRatios()
        {
            var details = Game.SceneView.GetComponentInChildren<AmbientWaterDetails>();
            var atmosphere = Game.SceneView.GetComponent<UnderwaterAtmosphere>();
            var sprites = details.GetComponentsInChildren<SpriteRenderer>();
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null;
                foreach (float depth in new[] { 0f, 45f, 90f })
                {
                    Camera.main.transform.position = new Vector3(0, -depth, -10);
                    for (int step = 0; step < 180; step++) atmosphere.Tick(.1f);
                    foreach (var sprite in sprites)
                    {
                        Assert.That(sprite.transform.position.y, Is.LessThan(.2f));
                        Assert.That(sprite.transform.position.y, Is.GreaterThan(Game.SceneView.SeabedY));
                        Assert.That(Mathf.Abs(sprite.transform.position.x), Is.LessThan(4.6f));
                        Assert.That(sprite.sharedMaterial.shader.isSupported, Is.True);
                    }
                }
            }
            Assert.That(details.ParticleCount, Is.EqualTo(30));
        }
    }
}
