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
    /// <summary>Chinese UI, tutorial persistence, source-aware settlement and real portrait preview acceptance.</summary>
    public sealed class UiIntegrationTests
    {
        private GameBootstrap _game;
        private bool _hadPreference;
        private int _preference;
        private ReelingComparisonScope _comparison;
        [UnitySetUp]
        public IEnumerator Load()
        {
            _hadPreference = PlayerPrefs.HasKey(GameUi.TutorialPreferenceKey); _preference = PlayerPrefs.GetInt(GameUi.TutorialPreferenceKey);
            PlayerPrefs.DeleteKey(GameUi.TutorialPreferenceKey); PlayerPrefs.Save();
            Resolution(800); SceneManager.LoadScene("Main"); yield return null; yield return null; Current();
            _comparison = new ReelingComparisonScope(false); _game.BeginRun();
        }
        [UnityTearDown]
        public IEnumerator Leave()
        {
            _comparison?.Dispose();
            if (_hadPreference) PlayerPrefs.SetInt(GameUi.TutorialPreferenceKey, _preference); else PlayerPrefs.DeleteKey(GameUi.TutorialPreferenceKey);
            PlayerPrefs.Save(); Resolution(800); SceneManager.LoadScene("Menu"); yield return null; yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        private void Current() { _game = Object.FindObjectOfType<GameBootstrap>(); _game.enabled = false; }
        private static void Click(string name) { GameObject.Find(name).GetComponent<Button>().onClick.Invoke(); }
        private static Text Label(string name) => GameObject.Find(name).GetComponent<Text>();
        private void Until(RunState state)
        { for (int i = 0; i < 4000 && _game.Run.State != state; i++) _game.AdvanceGameplay(.02f); Assert.That(_game.Run.State, Is.EqualTo(state)); }

        [UnityTest]
        public IEnumerator FirstStartDefersRunUntilTutorialClosesAndManualTeachingReturnsToMenu()
        {
            SceneManager.LoadScene("Menu"); yield return null; yield return null; Current();
            Assert.That(_game.Run, Is.Null); Click("How to play");
            Assert.That(_game.Ui.IsTutorialOpen, Is.True); Assert.That(_game.Ui.TutorialPage, Is.Zero);
            Assert.That(GameObject.Find("Tutorial previous").GetComponent<Button>().interactable, Is.False);
            Click("Tutorial next"); Click("Tutorial next"); Assert.That(_game.Ui.TutorialPage, Is.EqualTo(2));
            Click("Tutorial next"); Assert.That(_game.Ui.IsTutorialOpen, Is.False); Assert.That(_game.Run, Is.Null);
            Assert.That(PlayerPrefs.GetInt(GameUi.TutorialPreferenceKey, 0), Is.Zero);
            Click("Start dive"); Assert.That(_game.Ui.IsTutorialOpen, Is.True); Assert.That(_game.Run, Is.Null);
            Click("Tutorial next"); Click("Tutorial previous"); Assert.That(_game.Ui.TutorialPage, Is.Zero);
            Click("Tutorial next"); Click("Tutorial next");
            Assert.That(GameObject.Find("Tutorial next").GetComponentInChildren<Text>().text, Is.EqualTo("开始下潜"));
            Click("Tutorial next"); yield return null; yield return null; Current();
            Assert.That(_game.Run.State, Is.EqualTo(RunState.Countdown)); Assert.That(_game.Ui.IsTutorialOpen, Is.False);
            Assert.That(PlayerPrefs.GetInt(GameUi.TutorialPreferenceKey), Is.EqualTo(1));
            var runId = _game.Run.RunId; _game.Spawner.Clear(); Until(RunState.Result); Click("Back to menu");
            yield return null; yield return null; Current(); Assert.That(_game.Run, Is.Null);
            Click("Start dive"); yield return null; yield return null; Current();
            Assert.That(_game.Run.State, Is.EqualTo(RunState.Countdown)); Assert.That(_game.Run.RunId, Is.Not.EqualTo(runId));
            Assert.That(_game.Run.TotalCaptureCount, Is.Zero); Assert.That(_game.Ui.IsTutorialOpen, Is.False);
        }
        [UnityTest]
        public IEnumerator ClosingFirstTutorialWithCloseButtonStartsTheCountdown()
        {
            SceneManager.LoadScene("Menu"); yield return null; yield return null; Current(); Click("Start dive");
            for (int i = 0; i < 12; i++) { yield return null; Assert.That(_game.Run, Is.Null); }
            Click("Tutorial close"); yield return null; yield return null; Current();
            Assert.That(_game.Run.State, Is.EqualTo(RunState.Countdown)); Assert.That(_game.Run.Depth, Is.Zero);
            Assert.That(_game.Hook.transform.position.x, Is.Zero); Assert.That(Label("Countdown").text, Is.EqualTo("3"));
            Assert.That(GameObject.Find("Pause button").GetComponent<Button>().interactable, Is.False);
        }
        [UnityTest]
        public IEnumerator SettlementMergesSourcesUsesSnapshotValuesScrollsSevenSpeciesAndClearsEmptyResults()
        {
            var run = SettlementRun(); var blue = _game.Spawner.Spawned.First(f => f.Definition.speciesId == "blue").Definition;
            int oldGold = blue.gold;
            try { blue.gold = 999; _game.Ui.StateChanged(run); _game.Ui.ShowResult(run); }
            finally { blue.gold = oldGold; }
            yield return null; yield return null;
            CollectionAssert.AreEqual(new[] { "eel", "yellow", "red", "blue", "puffer", "small", "silver" }, _game.Ui.ResultRows.Select(r => r.SpeciesId));
            Assert.That(_game.Ui.ResultRows.Sum(r => r.TotalCount), Is.EqualTo(8));
            Assert.That(Label("Gold total").text, Is.EqualTo("173")); Assert.That(Label("Shell total").text, Is.EqualTo("128"));
            var card = GameObject.Find("Catch card blue").transform;
            Assert.That(card.Find("Quantity").GetComponent<Text>().text, Is.EqualTo("×2"));
            Assert.That(card.Find("Fish name").GetComponent<Text>().cachedTextGenerator.characterCountVisible, Is.EqualTo(3));
            Assert.That(card.Find("Quantity").GetComponent<Text>().cachedTextGenerator.characterCountVisible, Is.EqualTo(2));
            Assert.That(card.Find("Capture sources").GetComponent<Text>().text, Is.EqualTo("主钩 ×1 / 分支 ×1"));
            Assert.That(card.Find("Unit gold").GetComponent<Text>().text, Is.EqualTo("10")); Assert.That(card.Find("Branch badge"), Is.Not.Null);
            Assert.That(GameObject.Find("Catch card puffer").transform.Find("Branch badge"), Is.Null);
            var scroll = _game.Ui.ResultScroll;
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = scroll.viewport.position };
            scroll.OnBeginDrag(pointer); pointer.position += Vector2.up * 250; scroll.OnDrag(pointer); scroll.OnEndDrag(pointer);
            yield return null; Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.95f));
            var empty = new RunModel(_game.Run.Rules); State(empty, RunState.Result); _game.Ui.ShowResult(empty);
            yield return null; yield return null;
            Assert.That(_game.Ui.ResultRows.Count, Is.Zero); Assert.That(GameObject.Find("Empty catch"), Is.Not.Null);
            Assert.That(Label("Gold total").text, Is.EqualTo("0")); Assert.That(Label("Shell total").text, Is.EqualTo("0"));
            Assert.That(scroll.content.GetComponentsInChildren<Text>().Length, Is.Zero);
        }
        /// <summary>Seven-species settlement and the green-zone tutorial remain readable in all supported portrait previews.</summary>
        [UnityTest]
        public IEnumerator SettlementCardsAndControlTeachingRemainReadableInFourPortraits()
        {
            var result = SettlementRun();
            foreach (var size in new[] { new Vector2Int(450, 800), new Vector2Int(450, 975), new Vector2Int(450, 1050), new Vector2Int(1200, 2670) })
            {
                Resolution(size.y, size.x); yield return null; yield return null;
                _game.Ui.ApplySafeArea(); _game.Ui.StateChanged(result); _game.Ui.ShowResult(result); _game.Ui.Tick(1);
                yield return null; yield return null;
                Bounds("Result card", "Catch viewport", "Dive again", "Back to menu"); TextFits();
                foreach (var row in _game.Ui.ResultRows)
                {
                    var card = GameObject.Find("Catch card " + row.SpeciesId).transform;
                    Assert.That(card.Find("Fish plate"), Is.Null);
                    Assert.That(card.Find("Quantity").GetComponent<Text>().text, Is.EqualTo("×" + row.TotalCount));
                    Assert.That(card.Find("Unit gold").GetComponent<Text>().text, Is.EqualTo((row.Gold / row.TotalCount).ToString()));
                }
                ScreenCapture.CaptureScreenshot($"Docs/Verification/result-polish-{size.x}x{size.y}-20261024.png");
                yield return null; yield return null;
                _game.Ui.ShowMenu(); Click("How to play"); Click("Tutorial next"); Click("Tutorial next");
                StringAssert.Contains("绿区上升", Label("Tutorial body").text);
                StringAssert.Contains("松开下落", Label("Tutorial body").text);
                StringAssert.Contains("稳固", Label("Tutorial body").text);
                StringAssert.DoesNotContain("电脑", Label("Tutorial body").text);
                StringAssert.DoesNotContain("空格", Label("Tutorial body").text);
                Assert.That(GameObject.Find("Tutorial green zone"), Is.Not.Null); TextFits();
                if (size.x == 1200)
                {
                    ScreenCapture.CaptureScreenshot("Docs/Verification/tutorial-android-20261025.png");
                    yield return null; yield return null;
                }
                Click("Tutorial close");
            }
        }
        [UnityTest]
        public IEnumerator ChineseHudTracksProtectionPauseFreezesItsPulseAndRestartResetsTheRun()
        {
            var definition = _game.Spawner.Spawned[0].Definition; _game.Spawner.Clear(); Until(RunState.Diving);
            var fish = _game.Spawner.SpawnOne(definition, Vector2.zero, 1, 4.5f);
            fish.transform.position += _game.Hook.transform.position + Vector3.down * .12f - fish.Head.transform.position;
            _game.AdvanceGameplay(.02f);
            Assert.That(Label("HUD value 0").text, Is.EqualTo("1 / 2"));
            Assert.That(Label("HUD value 2").text, Is.EqualTo($"{Mathf.FloorToInt(_game.Run.Depth)} / 100 米"));
            var scale = Label("HUD value 0").transform.localScale; double time = _game.Run.GameTime;
            _game.PauseRun(); var state = _game.Run.State;
            for (int i = 0; i < 100; i++) _game.AdvanceGameplay(.02f);
            Assert.That(_game.Run.GameTime, Is.EqualTo(time)); Assert.That(_game.Run.State, Is.EqualTo(state));
            Assert.That(Label("HUD value 0").transform.localScale, Is.EqualTo(scale)); Assert.That(GameObject.Find("Pause"), Is.Not.Null);
            Click("Resume"); _game.AdvanceGameplay(.02f); Assert.That(_game.Run.GameTime, Is.GreaterThan(time));
            var id = _game.Run.RunId; _game.PauseRun(); Click("Restart");
            Assert.That(_game.Run.RunId, Is.Not.EqualTo(id)); Assert.That(_game.Run.State, Is.EqualTo(RunState.Countdown));
            Assert.That(_game.Run.IsPaused, Is.False); Assert.That(Label("HUD value 0").text, Is.EqualTo("2 / 2"));
            Assert.That(Label("HUD value 1").text, Is.EqualTo("0 / 10")); Assert.That(Label("HUD value 2").text, Is.EqualTo("0 / 100 米"));
            Assert.That(GameObject.Find("Pause"), Is.Null); Assert.That(_game.Hook.GetComponentsInChildren<FishActor>().Length, Is.Zero);
            yield return null;
        }
        /// <summary>Longest HUD values remain readable across safe areas without restarting depth pulses or hiding retained branch fish.</summary>
        [UnityTest]
        public IEnumerator UnifiedHudKeepsLongestValuesSeparateAndRetainsBranchCatchesInFourPortraits()
        {
            var definition = _game.Spawner.Spawned[0].Definition;
            var add = typeof(RunModel).GetMethod("AddCapture", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var size in new[] { new Vector2Int(450, 800), new Vector2Int(450, 975), new Vector2Int(450, 1050), new Vector2Int(1200, 2670) })
            {
                Resolution(size.y, size.x); yield return null; yield return null;
                _game.Ui.ResetRun();
                var run = new RunModel(_game.Run.Rules); State(run, RunState.Diving);
                typeof(RunModel).GetMethod("SetDepth", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, new object[] { 100f });
                for (int i = 0; i < 16; i++) add.Invoke(run, new object[] {
                    new CaptureRecord(definition.Snapshot(), i + 1, i < 10 ? CaptureSource.Main : CaptureSource.Branch,
                        i < 10 ? 0 : 1 + (i - 10) / 3, 90, 20) });
                _game.Ui.StateChanged(run); _game.Ui.SetAbilities(run, _game.Abilities);
                float inset = size.x / 450f;
                _game.Ui.ApplySafeArea(new Rect(12 * inset, 28 * inset, size.x - 24 * inset, size.y - 92 * inset));
                _game.Ui.Tick(.07f); Canvas.ForceUpdateCanvases();
                Assert.That(Label("HUD name 1").text, Is.EqualTo("主钩"));
                Assert.That(Label("HUD value 1").text, Is.EqualTo("10 / 10"));
                Assert.That(Label("HUD value 3").text, Is.EqualTo("6 / 6"), "Retained catches stay visible even when branches are inactive.");
                var assets = (GameUiAssets)typeof(GameBootstrap).GetField("uiAssets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_game);
                Assert.That(GameObject.Find("HUD icon 0").GetComponent<Image>().sprite, Is.SameAs(assets.protection));
                StringAssert.Contains("Bubble", assets.protection.name, "Protection uses a bubble, not the old mask.");
                Bounds("Stats", "Pause button"); TextFits();
                var left = new Vector3[4]; var right = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    var label = Label("HUD name " + i); var value = Label("HUD value " + i);
                    Assert.That(label.fontStyle,Is.EqualTo(FontStyle.Bold)); Assert.That(value.fontStyle,Is.EqualTo(FontStyle.Bold));
                    label.rectTransform.GetWorldCorners(left); value.rectTransform.GetWorldCorners(right);
                    Assert.That(right[0].x, Is.GreaterThan(left[2].x), "Columns must remain separate at pulse peaks.");
                    Assert.That(value.preferredWidth, Is.LessThanOrEqualTo(value.rectTransform.rect.width), value.text);
                    foreach (var graphic in label.transform.parent.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
                }
                typeof(RunModel).GetMethod("SetDepth", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, new object[] { 99f });
                _game.Ui.Refresh(run); _game.Ui.Tick(.04f);
                Assert.That(Label("HUD value 2").transform.localScale, Is.EqualTo(Vector3.one), "Depth must not bounce every metre.");
                _game.Ui.Tick(.2f); _game.CameraRig.Follow(run, 0, true); _game.SceneView.FollowCamera(Camera.main);
                yield return null;
                ScreenCapture.CaptureScreenshot($"Docs/Verification/hud-restored-{size.x}x{size.y}-20261024.png");
                yield return null; yield return null;
                _game.Ui.ResetRun(); _game.Ui.StateChanged(new RunModel(run.Rules));
                Assert.That(GameObject.Find("HUD chip 3"), Is.Null);
                Assert.That(GameObject.Find("Stats").GetComponent<RectTransform>().rect.height, Is.EqualTo(140));
            }
        }
        [UnityTest]
        public IEnumerator ThreeRealPortraitRatiosAndInsetSafeAreasKeepControlsAndChineseTextVisible()
        {
            var result = SettlementRun();
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null; yield return null;
                Assert.That(Screen.width, Is.EqualTo(450)); Assert.That(Screen.height, Is.EqualTo(height));
                foreach (bool inset in new[] { false, true })
                {
                    _game.Ui.ApplySafeArea(inset ? new Rect(12, 28, 426, height - 92) : new Rect(0, 0, 450, height));
                    _game.Ui.ShowMenu(); Bounds("Start dive", "How to play", "Quit", "Title", "Subtitle"); TextFits();
                    Click("How to play");
                    for (int page = 0; page < 3; page++)
                    { Bounds("Tutorial card", "Tutorial next", "Tutorial previous", "Tutorial close"); TextFits(); if (page < 2) Click("Tutorial next"); }
                    Click("Tutorial close"); _game.Ui.StateChanged(_game.Run); Bounds("Stats", "Pause button"); TextFits();
                    _game.Ui.Pause(true); Bounds("Pause card", "Resume", "Restart", "Back to menu"); TextFits(); _game.Ui.Pause(false);
                    _game.Ui.StateChanged(result); _game.Ui.ShowResult(result); yield return null;
                    Bounds("Result card", "Catch viewport", "Dive again", "Back to menu", "Reward totals"); TextFits();
                }
            }
            var texts = _game.Ui.Canvas.GetComponentsInChildren<Text>(true); var font = texts[0].font;
            string glyphs = new string(texts.SelectMany(t => t.text).Where(c => !char.IsWhiteSpace(c)).Distinct().ToArray());
            font.RequestCharactersInTexture(glyphs, 28);
            foreach (char glyph in glyphs) Assert.That(font.HasCharacter(glyph), Is.True, "Missing glyph: " + glyph);
        }
        [UnityTest]
        public IEnumerator ThreePortraitRatiosPreserveProtectionFullLoadPauseAndRestart()
        {
            var definition = _game.Spawner.Spawned[0].Definition;
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null; yield return null; _game.Ui.ApplySafeArea();
                _game.BeginRun(); _game.Spawner.Clear(); Until(RunState.Diving);
                var fish = _game.Spawner.SpawnOne(definition, Vector2.zero, 1, 4.5f);
                fish.transform.position += _game.Hook.transform.position + Vector3.down * .12f - fish.Head.transform.position;
                _game.AdvanceGameplay(.02f); Assert.That(_game.Run.Protection, Is.EqualTo(1));
                Click("Pause button"); double time = _game.Run.GameTime; var position = _game.Hook.transform.position;
                for (int tick = 0; tick < 40; tick++) _game.AdvanceGameplay(.02f);
                Assert.That(_game.Run.GameTime, Is.EqualTo(time)); Assert.That(_game.Hook.transform.position, Is.EqualTo(position));
                Click("Resume"); _game.Spawner.Clear(); Until(RunState.Ascending);
                for (int i = 0; i < 12; i++)
                {
                    var candidate = _game.Spawner.SpawnOne(definition, Vector2.zero, 1, 4.5f);
                    candidate.transform.position += _game.Hook.transform.position + Vector3.up * .13f - candidate.Head.transform.position;
                }
                _game.AdvanceGameplay(.02f); Assert.That(_game.Run.MainCaptureCount, Is.EqualTo(10));
                Assert.That(Label("HUD value 1").text, Is.EqualTo("10 / 10"));
                Assert.That(Label("HUD value 1").color.r, Is.GreaterThan(Label("HUD value 1").color.g));
                float depth = _game.Run.Depth; _game.AdvanceGameplay(.02f);
                Assert.That(depth - _game.Run.Depth, Is.InRange(.00001f,.1f), "Full-load launch remains smooth at every portrait ratio.");
                for (int i = 0; i < 20; i++) _game.AdvanceGameplay(.02f);
                depth = _game.Run.Depth; _game.AdvanceGameplay(.02f);
                Assert.That(depth - _game.Run.Depth, Is.EqualTo(.65f).Within(.001f));
                Until(RunState.Result); Assert.That(_game.Ui.ResultRows.Sum(row => row.TotalCount), Is.EqualTo(10));
                var id = _game.Run.RunId; Click("Dive again");
                Assert.That(_game.Run.RunId, Is.Not.EqualTo(id)); Assert.That(_game.Run.State, Is.EqualTo(RunState.Countdown));
                Assert.That(_game.Run.TotalCaptureCount, Is.Zero); Assert.That(_game.Spawner.ActiveCount, Is.EqualTo(54));
                Assert.That(_game.Hook.GetComponentsInChildren<FishActor>().Length, Is.Zero);
                Assert.That(Camera.main.orthographicSize * Camera.main.aspect * 2, Is.EqualTo(12).Within(.001));
            }
        }
        private RunModel SettlementRun()
        {
            var model = new RunModel(_game.Run.Rules); State(model, RunState.Result);
            typeof(RunModel).GetMethod("SetDepth", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model, new object[] { model.Rules.MaxDepth });
            var add = typeof(RunModel).GetMethod("AddCapture", BindingFlags.Instance | BindingFlags.NonPublic); int id = 0;
            foreach (var definition in _game.Spawner.Spawned.Select(f => f.Definition).Distinct())
                add.Invoke(model, new object[] { new CaptureRecord(definition.Snapshot(), ++id, CaptureSource.Main, 0, 90, 20) });
            var blue = _game.Spawner.Spawned.First(f => f.Definition.speciesId == "blue").Definition;
            add.Invoke(model, new object[] { new CaptureRecord(blue.Snapshot(), ++id, CaptureSource.Branch, 1, 80, 21) });
            return model;
        }
        private static void State(RunModel run, RunState state) { typeof(RunModel).GetProperty("State").SetValue(run, state); }
        private static void Resolution(int height, int width = 450)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Fishing.Editor.FishingPreview")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null); type.GetMethod("Resolution").Invoke(null, new object[] { width, height });
        }
        private void Bounds(params string[] names)
        {
            Canvas.ForceUpdateCanvases(); var corners = new Vector3[4]; _game.Ui.SafeArea.GetWorldCorners(corners);
            var safe = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            foreach (string name in names)
            {
                var rect = GameObject.Find(name).GetComponent<RectTransform>(); rect.GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(safe.xMin - 1), name); Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(safe.yMin - 1), name);
                Assert.That(corners[2].x, Is.LessThanOrEqualTo(safe.xMax + 1), name); Assert.That(corners[2].y, Is.LessThanOrEqualTo(safe.yMax + 1), name);
            }
        }
        private void TextFits()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in _game.Ui.Canvas.GetComponentsInChildren<Text>())
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .1f), text.name + ": " + text.text);
        }
    }
}
