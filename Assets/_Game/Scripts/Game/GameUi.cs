using System;
using System.Text;
using Fishing.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Runtime-built placeholder UGUI. Buttons report intent; this view never writes rules.</summary>
    public sealed class GameUi
    {
        private readonly RectTransform _safe;
        private readonly GameObject _menu, _hud, _result, _pause, _help;
        private readonly Text _stats, _phase, _countdown, _summary;
        private readonly Button _pauseButton;
        private readonly Font _font;
        /// <summary>Creates the single set of UI callbacks owned by this scene.</summary>
        public GameUi(Transform parent, Action start, Action pause, Action resume, Action restart, Action exit, Action quit)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280); scaler.matchWidthOrHeight = .5f;
            _safe = Rect("Safe Area", canvasGo.transform); ApplySafeArea();
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(parent, false);
            }

            _menu = Panel("Menu", _safe, new Color(.025f, .1f, .18f, .92f));
            Label("Title", _menu.transform, "ICE LINE", new Vector2(0, 300), new Vector2(650, 130), 72, new Color(.66f, .94f, 1));
            Label("Subtitle", _menu.transform, "A little dive. A big catch.", new Vector2(0, 195), new Vector2(640, 65), 28);
            AddButton(_menu.transform, "Start dive", new Vector2(0, 10), start);
            AddButton(_menu.transform, "How to play", new Vector2(0, -110), () => _help.SetActive(true));
            AddButton(_menu.transform, "Quit", new Vector2(0, -230), quit);

            _hud = Panel("HUD", _safe, Color.clear);
            _stats = Label("Stats", _hud.transform, "", Vector2.zero, new Vector2(500, 150), 27);
            Anchor(_stats.rectTransform, new Vector2(0, 1), new Vector2(275, -105));
            _stats.alignment = TextAnchor.MiddleLeft;
            _phase = Label("Phase", _hud.transform, "", new Vector2(0, -420), new Vector2(650, 70), 31);
            _countdown = Label("Countdown", _hud.transform, "", new Vector2(0, 100), new Vector2(400, 200), 100);
            _pauseButton = AddButton(_hud.transform, "II", Vector2.zero, pause, new Vector2(90, 75));
            Anchor((RectTransform)_pauseButton.transform, new Vector2(1, 1), new Vector2(-65, -75));
            var hint = Label("Drag hint", _hud.transform, "Hold and drag left / right", Vector2.zero, new Vector2(650, 60), 24);
            Anchor(hint.rectTransform, new Vector2(.5f, 0), new Vector2(0, 50));

            _result = Panel("Result", _safe, new Color(.025f, .1f, .18f, .96f));
            Label("Result title", _result.transform, "YOUR CATCH", new Vector2(0, 420), new Vector2(650, 100), 55);
            _summary = Label("Summary", _result.transform, "", new Vector2(0, 80), new Vector2(620, 510), 28);
            AddButton(_result.transform, "Dive again", new Vector2(0, -300), restart);
            AddButton(_result.transform, "Back to menu", new Vector2(0, -420), exit);

            _pause = Panel("Pause", _safe, new Color(.025f, .1f, .18f, .95f));
            Label("Paused", _pause.transform, "PAUSED", new Vector2(0, 260), new Vector2(650, 100), 62);
            AddButton(_pause.transform, "Resume", new Vector2(0, 50), resume);
            AddButton(_pause.transform, "Restart", new Vector2(0, -70), restart);
            AddButton(_pause.transform, "Back to menu", new Vector2(0, -190), exit);

            _help = Panel("Help", _safe, new Color(.025f, .1f, .18f, .99f));
            Label("Help title", _help.transform, "HOW TO PLAY", new Vector2(0, 360), new Vector2(650, 100), 48);
            Label("Help text", _help.transform,
                "Hold and drag to move the hook.\n\nAvoid fish heads on the way down.\nTwo bubbles protect you from mistakes.\n\nCatch fish on the way back up.\nA full hook returns faster!",
                new Vector2(0, 50), new Vector2(620, 490), 28);
            AddButton(_help.transform, "Got it", new Vector2(0, -350), () => _help.SetActive(false));
            _pause.SetActive(false); _help.SetActive(false); _result.SetActive(false);
            ShowMenu();
        }
        /// <summary>Displays the menu skeleton.</summary>
        public void ShowMenu()
        { _menu.SetActive(true); _hud.SetActive(false); _result.SetActive(false); _pause.SetActive(false); _help.SetActive(false); }
        /// <summary>Refreshes numeric HUD from model events.</summary>
        public void Refresh(RunModel run)
        {
            _stats.text = $"PROTECTION  {run.Protection} / {run.Rules.ProtectionCharges}\nCATCH  {run.MainCaptureCount} / {run.Rules.MainCapacity}\nDEPTH  {run.Depth:0} / {run.Rules.MaxDepth:0} m";
        }
        /// <summary>Updates visibility on a state event.</summary>
        public void StateChanged(RunModel run)
        {
            _menu.SetActive(false); _hud.SetActive(run.State != RunState.Result);
            _result.SetActive(run.State == RunState.Result); _pause.SetActive(false); _help.SetActive(false);
            _phase.text = run.State == RunState.Diving ? "DIVE - avoid fish heads" :
                run.State == RunState.BottomHold ? "BOTTOM - gathering fish" :
                run.State == RunState.Ascending ? (run.IsFull ? "FULL - fast return!" : "RETURN - catch fish") :
                run.State == RunState.Surfacing ? "Back to the ice..." : "Get ready";
            _pauseButton.interactable = run.State == RunState.Diving || run.State == RunState.BottomHold || run.State == RunState.Ascending;
            Refresh(run);
        }
        /// <summary>Updates only the countdown presentation from state elapsed time.</summary>
        public void Countdown(RunModel run, float elapsed)
        { _countdown.text = run.State == RunState.Countdown ? Mathf.Max(1, Mathf.CeilToInt(run.Rules.CountdownSeconds - elapsed)).ToString() : ""; }
        /// <summary>Toggles the pause overlay independently of flow state.</summary>
        public void Pause(bool paused) { _pause.SetActive(paused); }
        /// <summary>Builds a minimal source-aware result without scene-object queries.</summary>
        public void ShowResult(RunModel run)
        {
            var text = new StringBuilder();
            text.AppendLine($"Deepest dive: {run.ReachedDepth:0} m\n");
            foreach (var row in Settlement.Summarize(run.Captures))
                text.AppendLine($"{row.SpeciesId.ToUpperInvariant()}   x{row.TotalCount}   +{row.Gold} gold");
            if (run.TotalCaptureCount == 0) text.AppendLine("An empty hook. Try another route!");
            text.AppendLine($"\nGOLD  {run.Gold}     SHELLS  {run.Shells}");
            _summary.text = text.ToString();
        }
        /// <summary>Adapts the screen overlay to the current safe area.</summary>
        public void ApplySafeArea()
        {
            Rect area = Screen.safeArea;
            _safe.anchorMin = new Vector2(area.xMin / Mathf.Max(1, Screen.width), area.yMin / Mathf.Max(1, Screen.height));
            _safe.anchorMax = new Vector2(area.xMax / Mathf.Max(1, Screen.width), area.yMax / Mathf.Max(1, Screen.height));
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        private static GameObject Panel(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = color.a > 0; return rect.gameObject;
        }
        private Text Label(string name, Transform parent, string content, Vector2 position, Vector2 size, int fontSize, Color? color = null)
        {
            var rect = Rect(name, parent); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<Text>(); text.font = _font; text.text = content;
            text.fontSize = fontSize; text.color = color ?? Color.white; text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false; return text;
        }
        private Button AddButton(Transform parent, string label, Vector2 position, Action clicked, Vector2? size = null)
        {
            var rect = Rect(label, parent); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size ?? new Vector2(430, 92); rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.08f, .43f, .58f, 1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => clicked());
            Label("Label", rect, label, Vector2.zero, rect.sizeDelta - new Vector2(12, 4), 30);
            return button;
        }
        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position)
        { rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; }
    }
}
