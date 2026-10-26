using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Checks physical-looking prop scale, reusable shallow geometry and actual refraction pixels in saved scenes.</summary>
    public sealed class SceneryIntegrationTests : AbilityTestScene
    {
        private Transform Surface => Game.SceneView.transform.Find("Surface");
        private Transform Scenery => Game.SceneView.transform.Find("Shallow ice scenery");
        private SpriteRenderer Art(string name) => Surface.Find(name).GetComponent<SpriteRenderer>();

        /// <summary>Checks the connected prop group against the actor, its floor contact, lamp and viewport at four portrait ratios.</summary>
        [UnityTest]
        public IEnumerator PropsHaveCoherentScaleGroundingAndRoomAcrossPortraits()
        {
            var camera = Camera.main; var samples = new List<string>();
            var originals = new[] { "Barrel", "Stand", "Lantern", "Weather Pole", "Anemometer", "Windsock" }
                .Select(n => new { name = n, position = Art(n).transform.localPosition, scale = Art(n).transform.localScale }).ToArray();
            var lamp = Surface.Find("Lighting/Lantern Warmth").GetComponent<UnityEngine.Rendering.Universal.Light2D>();
            var lampPosition = lamp.transform.localPosition;
            var mountain = Art("Mountains").transform; var mountainPosition = mountain.localPosition; var mountainScale = mountain.localScale;
            var shelf = Surface.Find("Rear snow shelf").GetComponent<MeshFilter>().sharedMesh;
            foreach (float aspect in new[] { 450f / 800, 450f / 975, 450f / 1050, 1200f / 2670 })
            {
                camera.aspect = aspect; Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Surfacing); Steps(40);
                Game.CameraRig.Follow(Game.Run, 0, true);
                float actorHeight = Game.SceneView.Fisherman.sprite.bounds.size.y * Game.SceneView.Fisherman.transform.lossyScale.y;
                var weather = Art("Weather Pole").bounds; weather.Encapsulate(Art("Anemometer").bounds); weather.Encapsulate(Art("Windsock").bounds);
                float poleRatio = weather.size.y / actorHeight, barrelRatio = Art("Barrel").bounds.size.y / actorHeight;
                float lampRatio = Art("Lantern").bounds.size.y / actorHeight;
                Assert.That(poleRatio, Is.InRange(1.6f, 2.1f)); Assert.That(barrelRatio, Is.InRange(.45f, .7f)); Assert.That(lampRatio, Is.InRange(.28f, .45f));
                foreach (var original in originals)
                {
                    var bounds = Art(original.name).bounds;
                    Assert.That(camera.WorldToViewportPoint(bounds.min).x, Is.GreaterThan(.015f), original.name);
                    Assert.That(camera.WorldToViewportPoint(bounds.max).x, Is.LessThan(.985f), original.name);
                    Assert.That(camera.WorldToViewportPoint(bounds.max).y, Is.LessThan(.88f), original.name);
                }
                float supportY = Art("Stand").bounds.min.y + Art("Stand").bounds.size.y * .62f;
                Assert.That(Mathf.Abs(Art("Lantern").bounds.min.y - supportY), Is.LessThan(.08f), "The lamp must sit on the painted tabletop, below its rear corner.");
                Assert.That(Mathf.Abs(lamp.transform.position.x - Art("Lantern").bounds.center.x), Is.LessThan(.1f));
                Assert.That(lamp.transform.position.y, Is.InRange(Art("Lantern").bounds.min.y, Art("Lantern").bounds.max.y));
                Assert.That(Vector2.Distance(Game.SceneView.SurfaceCatchPoint, Art("Barrel").bounds.center), Is.LessThan(Art("Barrel").bounds.extents.y));
                samples.Add("{\"aspect\":" + aspect.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"weatherToActor\":" + poleRatio.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"barrelToActor\":" + barrelRatio.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"lanternToActor\":" + lampRatio.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
                Until(RunState.Result); Game.BeginRun();
                foreach (var original in originals) { Assert.That(Art(original.name).transform.localPosition, Is.EqualTo(original.position)); Assert.That(Art(original.name).transform.localScale, Is.EqualTo(original.scale)); }
                Assert.That(lamp.transform.localPosition, Is.EqualTo(lampPosition));
                Assert.That(mountain.localPosition, Is.EqualTo(mountainPosition)); Assert.That(mountain.localScale, Is.EqualTo(mountainScale));
                Assert.That(Surface.Find("Rear snow shelf").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(shelf));
            }
            File.WriteAllText("Docs/Verification/scene-coherence-proportions-20261025.json", "{\"samples\":[" + string.Join(",", samples) + "]}");
            camera.ResetAspect(); yield return null;
        }

        /// <summary>Ensures the added volume remains decorative, leaves the central route open and survives pause and repeated restarts without duplication.</summary>
        [UnityTest]
        public IEnumerator ShallowIceIsReusableColliderFreeAndUsesThePauseAwareClock()
        {
            var meshes = Scenery.GetComponentsInChildren<MeshFilter>(); var originals = meshes.Select(m => m.sharedMesh).ToArray();
            var renderers = Scenery.GetComponentsInChildren<MeshRenderer>(); var materials = renderers.Select(r => r.sharedMaterial).ToArray();
            Assert.That(Scenery.GetComponentsInChildren<Collider2D>(), Is.Empty);
            foreach (var receiver in renderers)
            {
                Assert.That(receiver.sharedMaterial.shader.isSupported, Is.True);
                if (receiver.name == "Shallow water refraction") continue;
                Assert.That(receiver.sortingLayerName, Is.EqualTo("Default")); Assert.That(receiver.sortingOrder, Is.LessThan(0));
                Assert.That(receiver.bounds.min.y, Is.LessThan(-3));
                Assert.That(receiver.bounds.center.x < 0 ? receiver.bounds.max.x : -receiver.bounds.min.x, Is.LessThan(-.5f), "The clear central opening must remain open.");
            }
            var atmosphere = Game.SceneView.GetComponent<UnderwaterAtmosphere>(); var block = new MaterialPropertyBlock();
            atmosphere.Tick(.35f); Game.SceneView.FollowCamera(Camera.main); renderers[0].GetPropertyBlock(block); float time = block.GetFloat("_WaterTime");
            Assert.That(time, Is.GreaterThan(0)); Game.PauseRun(); Steps(20); Game.SceneView.FollowCamera(Camera.main);
            renderers[0].GetPropertyBlock(block); Assert.That(block.GetFloat("_WaterTime"), Is.EqualTo(time)); Game.ResumeRun();
            for (int i = 0; i < 12; i++)
            {
                Game.BeginRun(); Assert.That(Scenery.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(meshes.Length));
                for (int j = 0; j < meshes.Length; j++) { Assert.That(meshes[j].sharedMesh, Is.SameAs(originals[j])); Assert.That(renderers[j].sharedMaterial, Is.SameAs(materials[j])); }
            }
            renderers[0].GetPropertyBlock(block); Assert.That(block.GetFloat("_WaterTime"), Is.Zero); yield return null;
        }

        /// <summary>Measures the added volume and true captured-background distortion in rendered pixels, with a fixed clock and fixed transforms.</summary>
        [UnityTest]
        public IEnumerator IceVolumeAndShallowRefractionChangeActualPixels()
        {
            Game.Spawner.Clear(); Until(RunState.Surfacing); Steps(40); Game.CameraRig.Follow(Game.Run, 0, true); Game.Ui.Canvas.enabled = false;
            var water = Scenery.Find("Shallow water refraction").GetComponent<MeshRenderer>();
            var ice = Scenery.GetComponentsInChildren<MeshRenderer>().Where(r => r != water).ToArray(); var block = new MaterialPropertyBlock();
            Texture2D empty = null, solid = null, refracted = null;
            try
            {
                water.GetPropertyBlock(block); block.SetFloat("_RefractionStrength", 0); water.SetPropertyBlock(block);
                foreach (var receiver in ice) receiver.enabled = false;
                yield return null; yield return new WaitForEndOfFrame(); empty = ScreenCapture.CaptureScreenshotAsTexture();
                foreach (var receiver in ice) receiver.enabled = true;
                yield return null; yield return new WaitForEndOfFrame(); solid = ScreenCapture.CaptureScreenshotAsTexture();
                block.SetFloat("_RefractionStrength", .008f); water.SetPropertyBlock(block);
                yield return null; yield return new WaitForEndOfFrame(); refracted = ScreenCapture.CaptureScreenshotAsTexture();
                int volume = Changed(empty, solid), optics = Changed(solid, refracted);
                File.WriteAllBytes("Docs/Verification/scene-coherence-optics-solid-20261025.png", solid.EncodeToPNG());
                File.WriteAllBytes("Docs/Verification/scene-coherence-optics-refracted-20261025.png", refracted.EncodeToPNG());
                File.WriteAllText("Docs/Verification/scene-coherence-optics-20261025.json", "{\"volumeChangedPixels\":" + volume + ",\"refractionChangedPixels\":" + optics + "}");
                Assert.That(volume, Is.GreaterThan(500), "The underside must occupy real visible pixels, rather than exist outside the viewport.");
                Assert.That(optics, Is.GreaterThan(40), "Changing refraction must bend actual captured ice pixels.");
            }
            finally
            {
                water.SetPropertyBlock(null); foreach (var receiver in ice) receiver.enabled = true; Game.Ui.Canvas.enabled = true;
                if (empty != null) Object.Destroy(empty); if (solid != null) Object.Destroy(solid); if (refracted != null) Object.Destroy(refracted);
            }
        }
        private static int Changed(Texture2D a, Texture2D b)
        {
            var lo = Camera.main.WorldToScreenPoint(new Vector3(-4.4f, -5.1f, 0)); var hi = Camera.main.WorldToScreenPoint(new Vector3(4.4f, .1f, 0));
            var first = a.GetPixels32(); var second = b.GetPixels32(); int count = 0;
            for (int y = Mathf.Clamp((int)lo.y, 0, a.height - 1); y < Mathf.Clamp((int)hi.y, 0, a.height); y++)
            for (int x = Mathf.Clamp((int)lo.x, 0, a.width - 1); x < Mathf.Clamp((int)hi.x, 0, a.width); x++)
            {
                int i = y * a.width + x;
                if (Mathf.Abs(first[i].r - second[i].r) + Mathf.Abs(first[i].g - second[i].g) + Mathf.Abs(first[i].b - second[i].b) > 8) count++;
            }
            return count;
        }
    }
}
