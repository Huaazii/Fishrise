using System;
using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Chinese menu, event-driven HUD and overlay composition; buttons only report player intent.</summary>
    public sealed class GameUi
    {
        /// <summary>Only the first start-triggered tutorial updates this preference.</summary>
        public const string TutorialPreferenceKey = "CasualFishing.TutorialSeen.v1";
        private readonly UiWidgets _ui;
        private readonly RectTransform _safe, _pauseFrame;
        private readonly GameObject _menu, _hud, _pause;
        private readonly Text[] _values = new Text[4];
        private readonly float[] _pulse = new float[4];
        private readonly AbilityHud _abilityHud;
        private AbilityController _abilities;
        private readonly Text _phase, _countdown;
        private readonly Button _pauseButton;
        private readonly TutorialView _tutorial;
        private readonly ResultView _result;
        private readonly Action _start;
        private readonly FeedbackSettingsView _settings;
        private readonly GameplayFeedbackView _feedbackView;
        private RunModel _run;
        /// <summary>Record-driven out-of-water sequence and cleanup instrumentation.</summary>
        public SurfacePresentation Surface { get; }
        /// <summary>Whether the full-load speed-line cue is currently visible.</summary>
        public bool SpeedLinesVisible => _feedbackView.SpeedLinesVisible;
        /// <summary>Live capture labels, bounded and recycled between runs.</summary>
        public int CapturePopups => _feedbackView.VisibleLabels;
        /// <summary>Single scene-owned overlay canvas.</summary>
        public Canvas Canvas { get; }
        /// <summary>Current safe-area root in canvas space.</summary>
        public RectTransform SafeArea => _safe;
        /// <summary>Whether tutorial input currently covers the menu.</summary>
        public bool IsTutorialOpen => _tutorial.Root.activeSelf;
        /// <summary>Zero-based currently displayed tutorial page.</summary>
        public int TutorialPage => _tutorial.Page;
        /// <summary>Last rendered settlement rows, derived from capture records.</summary>
        public IReadOnlyList<FishSummary> ResultRows => _result.Rows;
        /// <summary>Scrollable settlement presentation.</summary>
        public ScrollRect ResultScroll => _result.Scroll;
        /// <summary>Builds one UI tree with shared assets and scene-owned intent callbacks.</summary>
        public GameUi(Transform parent, GameUiAssets assets, FishDefinition[] species,
            Action start, Action pause, Action resume, Action restart, Action exit, Action quit,
            SonarConfig sonar, BranchHookConfig branches, Action fireSonar, Action deployBranches,
            Camera camera, Action skipSurface, GameFeedback feedback)
        {
            _ui = new UiWidgets(assets); _start = start;
            var canvasGo = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false); Canvas = canvasGo.GetComponent<Canvas>(); Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280); scaler.matchWidthOrHeight = 0;
            _safe = UiWidgets.Rect("Safe Area", canvasGo.transform);
            _feedbackView = new GameplayFeedbackView(_ui, _safe, camera);
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(parent, false);
            }
            _menu = _ui.Overlay("Menu", _safe, .22f);
            var heading = _ui.Frame("Menu heading card", _menu.transform);
            UiWidgets.Place(heading, new Vector2(.5f, 1), new Vector2(0, -195), new Vector2(624, 280));
            _ui.AnchoredLabel("Menu eyebrow", heading, "冰 湖 探 险", new Vector2(.5f, .76f), Vector2.zero, new Vector2(500, 34), 20, color: UiWidgets.Accent);
            _ui.Label("Title", heading, "冰湖垂钓", Vector2.zero, new Vector2(560, 100), 60);
            _ui.AnchoredLabel("Subtitle", heading, "穿过冰层，看看这一钩的收获", new Vector2(.5f, .24f), Vector2.zero, new Vector2(550, 46), 23);
            _ui.Picture("Menu rod", _menu.transform, assets.rod, new Vector2(0, 20), new Vector2(520, 235));
            _ui.Label("Menu promise", _menu.transform, "下潜避开鱼头 · 上收满载而归", new Vector2(0, -150), new Vector2(600, 54), 24);
            var startButton = _ui.Button("Start dive", _menu.transform, "开始游戏", Vector2.zero, new Vector2(560, 88), StartPressed);
            var tutorialButton = _ui.Button("How to play", _menu.transform, "教学", Vector2.zero, new Vector2(560, 82), () => OpenTutorial(false), false);
            var quitButton = _ui.Button("Quit", _menu.transform, "退出", Vector2.zero, new Vector2(300, 78), quit, false);
            UiWidgets.Place((RectTransform)startButton.transform, new Vector2(.5f, 0), new Vector2(0, 326), new Vector2(560, 88));
            UiWidgets.Place((RectTransform)tutorialButton.transform, new Vector2(.5f, 0), new Vector2(0, 216), new Vector2(560, 82));
            UiWidgets.Place((RectTransform)quitButton.transform, new Vector2(.5f, 0), new Vector2(-142, 110), new Vector2(260, 78));
            var settingsButton = _ui.Button("Menu settings", _menu.transform, "设置", Vector2.zero, new Vector2(260, 78), OpenSettings, false);
            UiWidgets.Place((RectTransform)settingsButton.transform, new Vector2(.5f, 0), new Vector2(142, 110), new Vector2(260, 78));
            _ui.AnchoredLabel("Menu hint", _menu.transform, "按住屏幕，左右拖动鱼钩", new Vector2(.5f, 0), new Vector2(0, 38), new Vector2(600, 32), 18, color: UiWidgets.Muted);

            _hud = _ui.Overlay("HUD", _safe, 0);
            var stats = _ui.Panel("Stats", _hud.transform, new Color(.02f, .08f, .13f, .78f));
            UiWidgets.Place(stats, new Vector2(0, 1), new Vector2(174, -117), new Vector2(300, 186));
            var icons = new[] { assets.protection, assets.hook, assets.depth, assets.hook };
            var labels = new[] { "保护", "主钩", "深度", "分支" };
            for (int i = 0; i < 4; i++)
            {
                float y = 63 - i * 42;
                var icon = _ui.Picture("HUD icon " + i, stats, icons[i], Vector2.zero, new Vector2(34, 36));
                UiWidgets.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(28, y), new Vector2(30, 32));
                _ui.AnchoredLabel("HUD name " + i, stats, labels[i], new Vector2(0, .5f), new Vector2(89, y), new Vector2(66, 38), 21, TextAnchor.MiddleLeft);
                _values[i] = _ui.AnchoredLabel("HUD value " + i, stats, "", new Vector2(1, .5f), new Vector2(-104, y), new Vector2(172, 40), 24, TextAnchor.MiddleRight);
            }
            var phase = _ui.Panel("Phase chip", _hud.transform, new Color(.02f, .08f, .13f, .65f));
            UiWidgets.Place(phase, new Vector2(.5f, 0), new Vector2(0, 128), new Vector2(520, 48));
            _phase = _ui.Label("Phase", phase, "", Vector2.zero, new Vector2(510, 44), 24);
            _ui.AnchoredLabel("Drag hint", _hud.transform, "按住屏幕，左右拖动", new Vector2(.5f, 0), new Vector2(0, 64), new Vector2(600, 36), 18, color: UiWidgets.Muted);
            _countdown = _ui.Label("Countdown", _hud.transform, "", new Vector2(0, 100), new Vector2(320, 190), 112);
            _pauseButton = _ui.Button("Pause button", _hud.transform, "暂停", Vector2.zero, new Vector2(88, 88), pause);
            UiWidgets.Place((RectTransform)_pauseButton.transform, Vector2.one, new Vector2(-68, -68), new Vector2(88, 88));
            _abilityHud = new AbilityHud(_ui, _hud.transform, sonar, branches, fireSonar, deployBranches);

            _result = new ResultView(_ui, _safe, species, restart, exit);
            Surface = new SurfacePresentation(_ui, _safe, species, camera, skipSurface);
            _pause = _ui.Overlay("Pause", _safe, .67f); _pauseFrame = _ui.Frame("Pause card", _pause.transform);
            _ui.Label("Paused", _pauseFrame, "已暂停", new Vector2(0, 225), new Vector2(500, 86), 44);
            _ui.Label("Pause note", _pauseFrame, "休息一下，鱼群也会等你", new Vector2(0, 150), new Vector2(500, 60), 22, color: UiWidgets.Muted);
            _ui.Button("Resume", _pauseFrame, "继续", new Vector2(0, 38), new Vector2(480, 84), resume);
            _ui.Button("Restart", _pauseFrame, "重开", new Vector2(0, -74), new Vector2(480, 84), restart, false);
            _ui.Button("Pause settings", _pauseFrame, "设置", new Vector2(-125, -186), new Vector2(230, 84), OpenSettings, false);
            _ui.Button("Back to menu", _pauseFrame, "退出", new Vector2(125, -186), new Vector2(230, 84), exit, false);
            _tutorial = new TutorialView(_ui, _safe, TutorialClosed);
            _settings = new FeedbackSettingsView(_ui, _safe, feedback);
            ApplySafeArea(); ShowMenu();
        }
        private void StartPressed()
        { if (PlayerPrefs.GetInt(TutorialPreferenceKey, 0) == 0) OpenTutorial(true); else _start(); }
        private void OpenTutorial(bool startAfterClose) { _menu.SetActive(false); _tutorial.Open(startAfterClose); }
        private void OpenSettings() { _settings.Open(); }
        private void TutorialClosed(bool startAfterClose)
        {
            if (!startAfterClose) { _menu.SetActive(true); return; }
            PlayerPrefs.SetInt(TutorialPreferenceKey, 1); PlayerPrefs.Save(); _start();
        }
        /// <summary>Closes tutorial back navigation before the controller interprets Escape as pause.</summary>
        public bool HandleBack()
        {
            if (_settings.Root.activeSelf) { _settings.Close(); return true; }
            if (!IsTutorialOpen) return false; _tutorial.Close(); return true;
        }
        /// <summary>Displays the start page without changing authoritative run data.</summary>
        public void ShowMenu()
        { _menu.SetActive(true); _hud.SetActive(false); _result.Root.SetActive(false); _pause.SetActive(false); _tutorial.Root.SetActive(false); Surface.Reset(); _feedbackView.Reset(); _settings.Close(); }
        /// <summary>Clears all queued presentation from the previous run.</summary>
        public void ResetRun() { Surface.Reset(); _feedbackView.Reset(); _settings.Close(); }
        /// <summary>Shows capture feedback only after the model has committed its unique record.</summary>
        public void Capture(CaptureRecord record, Vector3 world) { _feedbackView.Capture(record, world); }
        /// <summary>Shows a burst only when a protection charge is consumed.</summary>
        public void ProtectionBurst(Vector3 world) { _feedbackView.Burst(world); }
        /// <summary>Refreshes numeric presentation from model events, including inactive protection and full-load color.</summary>
        public void Refresh(RunModel run)
        {
            SetValue(0, $"{run.Protection} / {run.Rules.ProtectionCharges}");
            SetValue(1, $"{run.MainCaptureCount} / {run.Rules.MainCapacity}");
            SetValue(2, $"{Mathf.FloorToInt(run.Depth)} / {run.Rules.MaxDepth:0} 米");
            int capacity = _abilities != null ? _abilities.Branches.Effect.HookCount * _abilities.Branches.Effect.Capacity : 0;
            SetValue(3, $"{run.BranchCaptureCount} / {capacity}");
            _values[0].color = run.State == RunState.Diving && run.Depth < run.Rules.BottomZoneDepth ? UiWidgets.White : UiWidgets.Muted;
            _values[1].color = run.IsFull ? UiWidgets.Full : UiWidgets.White;
            _values[3].color = _abilities != null && _abilities.Branches.Model.Active ? UiWidgets.Accent : UiWidgets.Muted;
        }
        /// <summary>Renders ability model events without querying scene objects.</summary>
        public void SetAbilities(RunModel run, AbilityController abilities)
        { _abilities = abilities; _abilityHud.Refresh(run, abilities); Refresh(run); }
        private void SetValue(int index, string value)
        { if (_values[index].text == value) return; _values[index].text = value; _pulse[index] = .18f; }
        /// <summary>Updates overlay visibility and phase text on state events.</summary>
        public void StateChanged(RunModel run)
        {
            _run = run;
            if (run.State == RunState.Surfacing) Surface.Begin(run); else Surface.Hide();
            if (run.State == RunState.Surfacing || run.State == RunState.Result) _feedbackView.Reset();
            _menu.SetActive(false); _hud.SetActive(run.State != RunState.Result && run.State != RunState.Surfacing); _result.Root.SetActive(run.State == RunState.Result);
            _pause.SetActive(false); _tutorial.Root.SetActive(false);
            _phase.text = run.State == RunState.Diving ? "下潜中 · 避开鱼头" : run.State == RunState.BottomHold ? "到达水底 · 寻找收获" :
                run.State == RunState.Ascending ? (run.IsFull ? "满载 · 加速返航" : "上收中 · 接触鱼头捕获") :
                run.State == RunState.Surfacing ? "返回冰面" : "准备下潜";
            _phase.color = run.IsFull ? UiWidgets.Full : UiWidgets.White;
            _countdown.text = run.State == RunState.Countdown ? Mathf.CeilToInt(run.Rules.CountdownSeconds).ToString() : "";
            _pauseButton.interactable = run.State == RunState.Diving || run.State == RunState.BottomHold || run.State == RunState.Ascending;
            Refresh(run);
        }
        /// <summary>Animates numeric feedback only on the controller's paused-aware gameplay clock.</summary>
        public void Tick(float dt)
        {
            if (_run != null) { _feedbackView.Tick(_run, dt); Surface.Tick(dt); }
            _result.Tick(dt);
            for (int i = 0; i < 4; i++)
            {
                _pulse[i] = Mathf.Max(0, _pulse[i] - dt);
                float scale = _pulse[i] > 0 ? 1 + .12f * Mathf.Sin((1 - _pulse[i] / .18f) * Mathf.PI) : 1;
                _values[i].rectTransform.localScale = Vector3.one * scale;
            }
        }
        /// <summary>Shows 3-2-1 using the same elapsed time as the countdown state.</summary>
        public void Countdown(RunModel run, float elapsed)
        {
            _countdown.text = run.State == RunState.Countdown ? Mathf.Max(1, Mathf.CeilToInt(run.Rules.CountdownSeconds - elapsed)).ToString() : "";
            _countdown.rectTransform.localScale = Vector3.one * (1 + .12f * Mathf.Exp(-(elapsed % 1) * 9));
        }
        /// <summary>Toggles the pause overlay without replacing the underlying run state.</summary>
        public void Pause(bool paused) { _pause.SetActive(paused); }
        /// <summary>Renders one source-aware card per captured species from record snapshots.</summary>
        public void ShowResult(RunModel run) { _result.Show(run); }
        /// <summary>Applies the current device's safe area.</summary>
        public void ApplySafeArea() { ApplySafeArea(Screen.safeArea); }
        /// <summary>Applies a pixel-space safe area, also usable by portrait preview acceptance.</summary>
        public void ApplySafeArea(Rect area)
        {
            _safe.anchorMin = new Vector2(area.xMin / Mathf.Max(1, Screen.width), area.yMin / Mathf.Max(1, Screen.height));
            _safe.anchorMax = new Vector2(area.xMax / Mathf.Max(1, Screen.width), area.yMax / Mathf.Max(1, Screen.height));
            _safe.offsetMin = _safe.offsetMax = Vector2.zero; UnityEngine.Canvas.ForceUpdateCanvases();
            _tutorial.Layout(_safe.rect.size); _result.Layout(_safe.rect.size);
            UiWidgets.Place(_pauseFrame, Vector2.one * .5f, Vector2.zero, new Vector2(Mathf.Min(620, _safe.rect.width - 48), 720));
        }
    }
}
