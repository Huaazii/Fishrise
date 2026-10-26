using System.Collections;
using System.Collections.Generic;
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
    /// <summary>Adversarial pointer, selection, readability and modal checks for all constructed buttons.</summary>
    public sealed class ButtonStateIntegrationTests : AbilityTestScene
    {
        protected override bool InteractiveReeling => true;
        private readonly Dictionary<string, int?> _preferences = new Dictionary<string, int?>();
        /// <summary>Preserves preference values and missing keys before exercising real settings callbacks.</summary>
        [UnitySetUp]
        public IEnumerator SavePreferences()
        {
            foreach (string key in new[] { GameUi.TutorialPreferenceKey, "CasualFishing.Sound.v1", "CasualFishing.Haptics.v1" })
                _preferences[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
            yield break;
        }
        /// <summary>Restores all preference keys even when a pointer/state assertion fails.</summary>
        [UnityTearDown]
        public IEnumerator RestorePreferences()
        {
            foreach (var entry in _preferences)
                if (entry.Value.HasValue) PlayerPrefs.SetInt(entry.Key, entry.Value.Value); else PlayerPrefs.DeleteKey(entry.Key);
            PlayerPrefs.Save(); yield break;
        }
        private static Button Button(string name) => GameObject.Find(name).GetComponent<Button>();
        private static PointerEventData Pointer(Button button)
        {
            var rect = (RectTransform)button.transform;
            return new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        }
        private static GameObject TargetAt(Button button)
        {
            Canvas.ForceUpdateCanvases(); var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(Pointer(button), hits);
            return hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        }
        private static IEnumerator Press(string name)
        {
            // Graphic.depth and raycast ordering settle on the first rendered frame after opening an overlay.
            yield return null;
            var button = Button(name); Assert.That(TargetAt(button), Is.EqualTo(button.gameObject), name + " raycast");
            var pointer = Pointer(button);
            button.OnPointerEnter(pointer); button.OnPointerDown(pointer); button.OnPointerUp(pointer); button.OnPointerClick(pointer);
            if (button != null) button.OnPointerExit(pointer);
            yield return null;
        }
        private static float Luminance(Color color)
        {
            float Channel(float x) => x <= .04045f ? x / 12.92f : Mathf.Pow((x + .055f) / 1.055f, 2.4f);
            return Channel(color.r) * .2126f + Channel(color.g) * .7152f + Channel(color.b) * .0722f;
        }
        private static void Readable(Button button, string state)
        {
            Color background = button.targetGraphic.color * button.targetGraphic.canvasRenderer.GetColor();
            Assert.That(background.a, Is.EqualTo(1).Within(.001), button.name + " " + state + " opaque background");
            foreach (var text in button.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrEmpty(text.text)) continue;
                Color foreground = text.color * text.canvasRenderer.GetColor();
                float a = Luminance(foreground), b = Luminance(background);
                float contrast = (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f);
                Assert.That(contrast, Is.GreaterThanOrEqualTo(4.5f), button.name + " " + state + " " + text.text);
            }
        }
        private static void Audit(Button button)
        {
            var savedColors = button.colors; bool savedInteractable = button.interactable;
            var savedClick = button.onClick; int calls = 0;
            button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(() => calls++);
            try
            {
                var colors = savedColors; colors.fadeDuration = 0; button.colors = colors; button.interactable = true;
                EventSystem.current.SetSelectedGameObject(null); var pointer = Pointer(button);
                button.OnPointerUp(pointer); button.OnPointerExit(pointer); Readable(button, "normal");
                button.OnPointerEnter(pointer); Readable(button, "hover");
                button.OnPointerDown(pointer); Readable(button, "pressed");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null, button.name + " stole Submit focus");
                button.OnPointerUp(pointer); button.OnPointerClick(pointer); button.OnPointerExit(pointer);
                Assert.That(calls, Is.EqualTo(1)); Readable(button, "released");
                Assert.That(Vector4.Distance(button.targetGraphic.canvasRenderer.GetColor(), colors.normalColor), Is.LessThan(.001f));
                // Force keyboard selection as well: no state is allowed to fall back to the default near-white tint.
                EventSystem.current.SetSelectedGameObject(button.gameObject); Readable(button, "selected");
                button.OnPointerDown(pointer); button.interactable = false; button.OnPointerUp(pointer);
                button.OnPointerClick(pointer); button.OnSubmit(new BaseEventData(EventSystem.current));
                Assert.That(calls, Is.EqualTo(1), button.name + " accepted a disabled command"); Readable(button, "disabled mid-press");
                button.interactable = true; Readable(button, "re-enabled selection");
                EventSystem.current.SetSelectedGameObject(null); button.OnPointerExit(pointer);
                // Cancelled and right-button presses do not invoke the command.
                button.OnPointerEnter(pointer); button.OnPointerDown(pointer); button.OnPointerExit(pointer); button.OnPointerUp(pointer);
                pointer.button = PointerEventData.InputButton.Right;
                button.OnPointerDown(pointer); button.OnPointerUp(pointer); button.OnPointerClick(pointer);
                Assert.That(calls, Is.EqualTo(1)); Readable(button, "cancelled/right button");
                button.gameObject.SetActive(false); button.gameObject.SetActive(true); Canvas.ForceUpdateCanvases(); Readable(button, "reopened");
            }
            finally
            {
                EventSystem.current.SetSelectedGameObject(null); button.onClick = savedClick;
                button.interactable = savedInteractable; button.colors = savedColors;
            }
        }
        private void AuditVisible(HashSet<int> seen)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var button in Game.Ui.Canvas.GetComponentsInChildren<Button>())
            { Audit(button); seen.Add(button.GetInstanceID()); }
        }
        /// <summary>Every actual menu/tutorial/settings/catalog/game/pause/surface/result button survives the full visual state matrix.</summary>
        [UnityTest]
        public IEnumerator EveryButtonRemainsReadableAcrossPointerSelectedDisabledAndReopenStates()
        {
            var seen = new HashSet<int>(); Game.Ui.ShowMenu(); AuditVisible(seen);
            yield return Press("How to play"); AuditVisible(seen); yield return Press("Tutorial next"); AuditVisible(seen);
            yield return Press("Tutorial next"); AuditVisible(seen); yield return Press("Tutorial close");
            yield return Press("Open catalog"); AuditVisible(seen); yield return Press("Close catalog");
            yield return Press("Menu settings"); AuditVisible(seen); yield return Press("Close settings");
            Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Diving); AuditVisible(seen);
            yield return Press("Pause button"); AuditVisible(seen); yield return Press("Pause settings"); AuditVisible(seen);
            yield return Press("Close settings"); yield return Press("Resume"); Until(RunState.Ascending);
            AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Steps(2); Game.Run.Reeling.GrantGrace(100);
            Until(RunState.Surfacing); AuditVisible(seen); Until(RunState.Result); AuditVisible(seen);
            Assert.That(seen.Count, Is.EqualTo(Game.Ui.Canvas.GetComponentsInChildren<Button>(true).Length));
            Assert.That(Game.Ui.Canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Phase chip" || t.name == "Phase"), Is.False);
            yield return null;
        }
        /// <summary>Pause eligibility and modal shields hold; pointer use leaves no Space/Submit target.</summary>
        [UnityTest]
        public IEnumerator PointerCommandsHonorEligibilityCooldownsModalsAndDoNotRetainSubmitFocus()
        {
            EventSystem.current.SetSelectedGameObject(null);
            bool sound = Game.Feedback.SoundEnabled, haptics = Game.Feedback.HapticsEnabled;
            try
            {
                Assert.That(Button("Pause button").interactable, Is.False); yield return Press("Pause button"); Assert.That(Game.Run.IsPaused, Is.False);
                Until(RunState.Diving); Assert.That(Button("Pause button").interactable, Is.True);
                Assert.That(Game.Ui.Canvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "Sonar button" || b.name == "Branch button"), Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                yield return Press("Pause button"); Assert.That(Game.Run.IsPaused, Is.True);
                Assert.That(TargetAt(Button("Pause button")), Is.Not.EqualTo(Button("Pause button").gameObject));
                yield return Press("Pause settings"); Assert.That(TargetAt(Button("Resume")), Is.Not.EqualTo(Button("Resume").gameObject));
                yield return Press("Toggle sound"); Assert.That(Game.Feedback.SoundEnabled, Is.Not.EqualTo(sound)); yield return Press("Toggle sound");
                yield return Press("Toggle haptics"); Assert.That(Game.Feedback.HapticsEnabled, Is.Not.EqualTo(haptics)); yield return Press("Toggle haptics");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                yield return Press("Close settings"); yield return Press("Resume"); Assert.That(Game.Run.IsPaused, Is.False);
                Until(RunState.BottomHold); Assert.That(Button("Pause button").interactable, Is.True);
                Until(RunState.Ascending); yield return Press("Pause button"); Assert.That(Game.Run.IsPaused, Is.True);
                yield return Press("Resume"); Assert.That(Game.Run.IsPaused, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                yield return null;
            }
            finally
            {
                if (Game.Feedback.SoundEnabled != sound) Game.Feedback.ToggleSound();
                if (Game.Feedback.HapticsEnabled != haptics) Game.Feedback.ToggleHaptics();
            }
        }
        /// <summary>Real pointer callbacks close/reopen pages, start/restart the run and return to menu after automatic surfacing.</summary>
        [UnityTest]
        public IEnumerator PointerNavigationCallbacksStillWorkAfterRepeatedOpenCloseAndRestart()
        {
            Game.Ui.ShowMenu(); EventSystem.current.SetSelectedGameObject(null);
            yield return Press("How to play"); yield return Press("Tutorial previous"); Assert.That(Game.Ui.TutorialPage, Is.Zero);
            yield return Press("Tutorial next"); yield return Press("Tutorial previous"); Assert.That(Game.Ui.TutorialPage, Is.Zero);
            yield return Press("Tutorial close"); yield return Press("Open catalog"); Assert.That(Game.Ui.IsCatalogOpen, Is.True);
            yield return Press("Close catalog"); Assert.That(Game.Ui.IsCatalogOpen, Is.False);
            var before = Game.Run.RunId; yield return Press("Start dive");
            if (Game.Ui.IsTutorialOpen) { yield return Press("Tutorial next"); yield return Press("Tutorial next"); yield return Press("Tutorial next"); }
            Assert.That(Game.Run.RunId, Is.Not.EqualTo(before)); Until(RunState.Diving);
            yield return Press("Pause button"); before = Game.Run.RunId; yield return Press("Restart");
            Assert.That(Game.Run.RunId, Is.Not.EqualTo(before)); Assert.That(Game.Run.IsPaused, Is.False);
            Game.Spawner.Clear(); Until(RunState.Result);
            Assert.That(Game.Run.State, Is.EqualTo(RunState.Result)); before = Game.Run.RunId; yield return Press("Dive again");
            Assert.That(Game.Run.RunId, Is.Not.EqualTo(before)); Game.Spawner.Clear(); Until(RunState.Diving);
            yield return Press("Pause button"); yield return Press("Back to menu"); yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
            Assert.That(GameObject.Find("Start dive"), Is.Not.Null);
        }
    }
}
