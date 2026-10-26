using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Fishing.Tests
{
    /// <summary>SO-driven catalog, menu navigation and scalable mobile UI including the native Xiaomi 15 profile.</summary>
    public sealed class FishCatalogIntegrationTests : AbilityTestScene
    {
        private FishDefinition[] Definitions => (FishDefinition[])typeof(GameBootstrap).GetField("species", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Game);
        private static void Size(int width, int height)
        {
            var preview = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Fishing.Editor.FishingPreview")).First(t => t != null);
            preview.GetMethod("Resolution").Invoke(null, new object[] { width, height });
        }
        private static void Xiaomi()
        {
            var preview = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Fishing.Editor.FishingPreview")).First(t => t != null);
            preview.GetMethod("Xiaomi15Portrait").Invoke(null, null);
        }
        private void Fits(params string[] names)
        {
            Canvas.ForceUpdateCanvases(); var safe = Game.Ui.SafeArea.rect; var corners = new Vector3[4];
            foreach (string name in names)
            {
                var rect = GameObject.Find(name).GetComponent<RectTransform>();
                // Scroll content intentionally extends outside its masked viewport; measure the control itself.
                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = Game.Ui.SafeArea.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(safe.xMin - 1, safe.xMax + 1), name);
                    Assert.That(local.y, Is.InRange(safe.yMin - 1, safe.yMax + 1), name);
                }
            }
        }
        private void CatalogTextFits()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in Game.Ui.CatalogScroll.transform.parent.GetComponentsInChildren<Text>())
            {
                // Offscreen masked rows have no rendered characters yet; check their actual UGUI layout metrics.
                Assert.That(text.rectTransform.rect.width, Is.GreaterThan(0), text.name);
                Assert.That(text.preferredWidth, Is.InRange(1, text.rectTransform.rect.width + 1), text.name + ": " + text.text);
                Assert.That(text.preferredHeight, Is.InRange(1, text.rectTransform.rect.height + 1), text.name + ": " + text.text);
                foreach (char glyph in text.text.Where(c => !char.IsWhiteSpace(c)))
                    Assert.That(text.font.HasCharacter(glyph), Is.True, "Missing glyph: " + glyph);
            }
        }
        private static Rect ScreenBounds(RectTransform rect)
        { var corners = new Vector3[4]; rect.GetWorldCorners(corners); return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y); }

        /// <summary>The actual Menu scene opens a complete, correctly sorted catalog without creating a run or consuming tutorial state.</summary>
        [UnityTest]
        public IEnumerator MenuCatalogShowsConfiguredValuesAndBackDoesNotStartARun()
        {
            int tutorial = PlayerPrefs.GetInt(GameUi.TutorialPreferenceKey);
            bool existed = PlayerPrefs.HasKey(GameUi.TutorialPreferenceKey);
            SceneManager.LoadScene("Menu"); yield return null; yield return null;
            Game = Object.FindObjectOfType<GameBootstrap>(); Game.enabled = false;
            Assert.That(Game.Run, Is.Null); Click("Open catalog"); Canvas.ForceUpdateCanvases();
            var expected = Definitions.Where(f => f.isCatchable).OrderByDescending(f => f.gold).ThenByDescending(f => f.shells).ThenBy(f => f.speciesId, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, Game.Ui.CatalogEntries);
            Assert.That(expected.Length, Is.EqualTo(7)); Assert.That(Game.Ui.IsCatalogOpen, Is.True);
            var assets=(GameUiAssets)typeof(GameBootstrap).GetField("uiAssets",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(Game);
            foreach (var fish in expected)
            {
                var row = GameObject.Find("Catalog row " + fish.speciesId);
                Assert.That(row.transform.Find("Catalog fish").GetComponent<Image>().sprite, Is.EqualTo(fish.sprite));
                Assert.That(row.GetComponent<ResultCardGraphic>(),Is.Not.Null);
                Assert.That(row.GetComponent<LayoutElement>().preferredHeight,Is.EqualTo(176));
                Assert.That(row.transform.Find("Catalog info/Catalog gold").GetComponent<Text>().text, Is.EqualTo(fish.gold.ToString()));
                Assert.That(row.transform.Find("Catalog info/Catalog shells").GetComponent<Text>().text, Is.EqualTo(fish.shells.ToString()));
                Assert.That(row.transform.Find("Catalog info/Catalog depth").GetComponent<Text>().text, Is.EqualTo($"{fish.minDepth:0.#}–{fish.maxDepth:0.#} 米"));
                Assert.That(row.transform.Find("Catalog info/Catalog gold icon").GetComponent<Image>().sprite,Is.SameAs(assets.coin));
                Assert.That(row.transform.Find("Catalog info/Catalog shells icon").GetComponent<Image>().sprite,Is.SameAs(assets.shell));
                Assert.That(row.transform.Find("Catalog info/Catalog depth icon").GetComponent<Image>().sprite,Is.SameAs(assets.depth));
                var stars=row.transform.Find("Catalog info/Catalog rarity").GetComponentsInChildren<Image>();
                Assert.That(stars.Length,Is.EqualTo(fish.rarity)); Assert.That(stars.All(image=>image.sprite==assets.star),Is.True);
            }
            Assert.That(GameObject.Find("Catalog order"), Is.Null);
            Assert.That(GameObject.Find("Catalog leaders"), Is.Null);
            CatalogTextFits(); Click("Close catalog"); Assert.That(Game.Ui.IsCatalogOpen, Is.False);
            Assert.That(GameObject.Find("Menu"), Is.Not.Null); Click("Open catalog"); Assert.That(Game.Ui.HandleBack(), Is.True);
            Assert.That(Game.Run, Is.Null); Assert.That(Game.Ui.IsTutorialOpen, Is.False);
            Assert.That(PlayerPrefs.HasKey(GameUi.TutorialPreferenceKey), Is.EqualTo(existed));
            Assert.That(PlayerPrefs.GetInt(GameUi.TutorialPreferenceKey), Is.EqualTo(tutorial));
        }

        /// <summary>Reopening rereads live SO values, preserves value sorting and reuses the seven cards.</summary>
        [UnityTest]
        public IEnumerator ReopenReflectsAssetChangesAndDoesNotDuplicateCardsOrModifyRun()
        {
            Game.Ui.ShowMenu(); var fish = Definitions.Single(f => f.speciesId == "blue");
            int gold = fish.gold, shells = fish.shells; float min = fish.minDepth, max = fish.maxDepth;
            var id = Game.Run.RunId; int runGold = Game.Run.Gold;
            try
            {
                Click("Open catalog"); Click("Close catalog"); fish.gold = 999; fish.shells = 1; fish.minDepth = 12; fish.maxDepth = 24;
                for (int i = 0; i < 12; i++)
                {
                    Click("Open catalog"); Assert.That(Game.Ui.CatalogEntries[0], Is.SameAs(fish));
                    Assert.That(Game.Ui.CatalogScroll.content.childCount, Is.EqualTo(7));
                    Assert.That(GameObject.Find("Catalog row blue").transform.Find("Catalog info/Catalog gold").GetComponent<Text>().text, Is.EqualTo("999"));
                    Assert.That(GameObject.Find("Catalog row blue").transform.Find("Catalog info/Catalog shells").GetComponent<Text>().text, Is.EqualTo("1"));
                    Assert.That(GameObject.Find("Catalog row blue").transform.Find("Catalog info/Catalog depth").GetComponent<Text>().text, Is.EqualTo("12–24 米"));
                    Click("Close catalog");
                }
            }
            finally { fish.gold = gold; fish.shells = shells; fish.minDepth = min; fish.maxDepth = max; }
            Assert.That(Game.Run.RunId, Is.EqualTo(id)); Assert.That(Game.Run.Gold, Is.EqualTo(runGold));
            Game.BeginRun(); Assert.That(Game.Ui.IsCatalogOpen, Is.False); yield return null;
        }

        /// <summary>Real portrait resolutions and synthetic cutout insets keep the catalog readable and its pointer drag scrollable.</summary>
        [UnityTest]
        public IEnumerator FourPortraitProfilesKeepCatalogInsideSafeAreaAndPointerDragReachesLastFish()
        {
            foreach (int height in new[] { 800, 975, 1050, 2670 })
            {
                if (height == 2670) Xiaomi(); else Size(450, height);
                yield return null; yield return null;
                int width = height == 2670 ? 1200 : 450;
                Assert.That(Screen.width, Is.EqualTo(width)); Assert.That(Screen.height, Is.EqualTo(height));
                foreach (bool inset in new[] { false, true })
                {
                    var safe = inset ? new Rect(width * .025f, height * .03f, width * .95f, height * .93f) : new Rect(0, 0, width, height);
                    Game.Ui.ApplySafeArea(safe); Game.Ui.ShowMenu(); Fits("Start dive", "How to play", "Open catalog", "Quit", "Menu settings");
                    Click("Open catalog"); Fits("Catalog card", "Catalog title", "Catalog viewport", "Close catalog"); CatalogTextFits();
                    var scroll = Game.Ui.CatalogScroll; Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                    var origin = ScreenBounds(scroll.viewport).center;
                    var pointer = new PointerEventData(EventSystem.current) { position = origin, pressPosition = origin, button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
                    ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.beginDragHandler);
                    pointer.position += Vector2.up * 180 * Game.Ui.Canvas.scaleFactor; pointer.delta = pointer.position - origin;
                    ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.dragHandler);
                    ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.endDragHandler);
                    Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.99f));
                    scroll.StopMovement(); scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                    var last = GameObject.Find("Catalog row " + Game.Ui.CatalogEntries.Last().speciesId).GetComponent<RectTransform>();
                    Assert.That(ScreenBounds(last).Overlaps(ScreenBounds(scroll.viewport)), Is.True);
                    Click("Close catalog");
                }
            }
        }

        /// <summary>Low and high pixel counts retain the same logical width and fit the menu, teaching, settings and catalog.</summary>
        [UnityTest]
        public IEnumerator CanvasScalesAcrossAdditionalMobileResolutions()
        {
            var profiles = new[] { new Vector2Int(320, 568), new Vector2Int(720, 1602), new Vector2Int(1080, 2400), new Vector2Int(1440, 3200) };
            foreach (var size in profiles)
            {
                Size(size.x, size.y); yield return null; yield return null; Game.Ui.ApplySafeArea(); Game.Ui.ShowMenu();
                var scaler = Game.Ui.Canvas.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(720, 1602)));
                Assert.That(Game.Ui.Canvas.scaleFactor, Is.EqualTo(size.x / 720f).Within(.001f));
                Fits("Start dive", "How to play", "Open catalog", "Menu settings");
                Click("Open catalog"); Fits("Catalog card", "Close catalog"); CatalogTextFits(); Click("Close catalog");
                Click("How to play"); Fits("Tutorial card", "Tutorial next", "Tutorial close"); Click("Tutorial close");
                Click("Menu settings"); Fits("Settings card", "Toggle sound", "Toggle haptics", "Close settings"); Click("Close settings");
            }
        }

        /// <summary>The Xiaomi baseline also supports the existing complete gameplay, pause, result and restart UI.</summary>
        [UnityTest]
        public IEnumerator XiaomiBaselineKeepsGameplayPauseResultAndRestartScalable()
        {
            Xiaomi(); yield return null; yield return null;
            Game.Ui.ApplySafeArea(new Rect(30, 80, 1140, 2480)); Game.BeginRun(); Game.Spawner.Clear();
            Until(RunState.Diving); Fits("Stats", "Pause button");
            Game.PauseRun(); Fits("Pause card", "Resume", "Restart", "Back to menu"); Click("Resume");
            Until(RunState.Result); Steps(30); Canvas.ForceUpdateCanvases(); Fits("Result card", "Dive again", "Reward totals");
            Click("Dive again"); Assert.That(Game.Run.State, Is.EqualTo(RunState.Countdown));
            Assert.That(Game.Ui.IsCatalogOpen, Is.False); Assert.That(Game.Run.TotalCaptureCount, Is.Zero);
            Assert.That(Camera.main.orthographicSize * Camera.main.aspect, Is.EqualTo(6).Within(.001f));
        }
    }
}
