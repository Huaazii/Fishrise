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
        [UnitySetUp]
        public IEnumerator Load()
        {
            _hadPreference = PlayerPrefs.HasKey(GameUi.TutorialPreferenceKey); _preference = PlayerPrefs.GetInt(GameUi.TutorialPreferenceKey);
            PlayerPrefs.DeleteKey(GameUi.TutorialPreferenceKey); PlayerPrefs.Save();
            Resolution(800); SceneManager.LoadScene("Main"); yield return null; yield return null; Current();
        }
        [UnityTearDown]
        public IEnumerator Leave()
        {
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
                    Click("Tutorial close"); _game.Ui.StateChanged(_game.Run); Bounds("Stats", "Pause button", "Phase chip"); TextFits();
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
            var add = typeof(RunModel).GetMethod("AddCapture", BindingFlags.Instance | BindingFlags.NonPublic); int id = 0;
            foreach (var definition in _game.Spawner.Spawned.Select(f => f.Definition).Distinct())
                add.Invoke(model, new object[] { new CaptureRecord(definition.Snapshot(), ++id, CaptureSource.Main, 0, 90, 20) });
            var blue = _game.Spawner.Spawned.First(f => f.Definition.speciesId == "blue").Definition;
            add.Invoke(model, new object[] { new CaptureRecord(blue.Snapshot(), ++id, CaptureSource.Branch, 1, 80, 21) });
            return model;
        }
        private static void State(RunModel run, RunState state) { typeof(RunModel).GetProperty("State").SetValue(run, state); }
        private static void Resolution(int height)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Fishing.Editor.FishingPreview")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null); type.GetMethod("Resolution").Invoke(null, new object[] { 450, height });
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
