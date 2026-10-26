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
        private readonly UnityEngine.UI.Image[] _icons = new UnityEngine.UI.Image[4];
        private readonly float[] _pulse = new float[4];
        private readonly RectTransform _stats;
        private readonly GameObject _branchChip;
        private readonly CanvasGroup _branchOpacity;
        private static readonly Color HudWhite = new Color(.918f, .961f, .980f);
        private static readonly Color HudMuted = new Color(.616f, .722f, .776f);
        private static readonly Color HudFull = new Color(1, .502f, .369f);
        private static readonly Color HudBranch = new Color(.396f, .863f, .925f);
        private bool _branchShown;
        private Color _protectionColor;
        private float _protectionFlash;
        private AbilityController _abilities;
        private readonly Text _countdown;
        private readonly Text _dragHint;
        private readonly Button _pauseButton;
        private readonly TutorialView _tutorial;
        private readonly ResultView _result;
        private readonly FishCatalogView _catalog;
        private readonly Action _start;
        private readonly FeedbackSettingsView _settings;
        private readonly FishingControlSettings _controls;
        private readonly Text _menuHint;
        private readonly GameplayFeedbackView _feedbackView;
        private readonly ReelingView _reeling;
        private readonly MenuPresentation _menuPresentation;
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
        /// <summary>Whether the menu's read-only species reference currently covers input.</summary>
        public bool IsCatalogOpen => _catalog.Root.activeSelf;
        /// <summary>Currently displayed source assets, sorted by per-fish gold and then shells.</summary>
        public IReadOnlyList<FishDefinition> CatalogEntries => _catalog.Entries;
        /// <summary>Scrollable catalog for touch input and responsive layout acceptance.</summary>
        public ScrollRect CatalogScroll => _catalog.Scroll;
        /// <summary>Builds one UI tree with shared assets and scene-owned intent callbacks.</summary>
        public GameUi(Transform parent, GameUiAssets assets, FishDefinition[] species,
            Action start, Action pause, Action resume, Action restart, Action exit, Action quit,
            Camera camera, GameFeedback feedback, FishingControlSettings controls)
        {
            _ui = new UiWidgets(assets, feedback.Play); _start = start; _controls = controls;
            // The legacy dynamic atlas is shared by every label; reserve the settlement glyphs
            // before the menu/tutorial fill it, otherwise species names and unit values cannot be added later.
            assets.font.RequestCharactersInTexture("鳗鱼河豚蓝纹鱼红鱼小红鱼黄蝴蝶鱼小银鱼0123456789×/米 主钩分支", 20, FontStyle.Normal);
            assets.font.RequestCharactersInTexture("主钩分支×0123456789/ ", 13, FontStyle.Normal);
            var canvasGo = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false); Canvas = canvasGo.GetComponent<Canvas>(); Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Logical width stays 720; the reference aspect matches Xiaomi 15's 1200x2670 portrait display.
            scaler.referenceResolution = new Vector2(720, 1602); scaler.matchWidthOrHeight = 0;
            _safe = UiWidgets.Rect("Safe Area", canvasGo.transform);
            _feedbackView = new GameplayFeedbackView(_ui, _safe, camera);
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(parent, false);
            }
            _menu = _ui.Overlay("Menu", _safe, .22f);
            var heading = _ui.Frame("Menu heading card", _menu.transform);
            UiWidgets.Place(heading, new Vector2(.5f, 1), new Vector2(0, -174), new Vector2(624, 238));
            var title = _ui.Label("Title", heading, "Fishrise", new Vector2(0, 18), new Vector2(560, 112), 82);
            title.font = assets.titleFont != null ? assets.titleFont : assets.font;
            title.gameObject.AddComponent<IceTitleGradient>();
            var titleDepth = title.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            titleDepth.effectColor = new Color(.025f, .22f, .30f, .95f); titleDepth.effectDistance = new Vector2(0, -4);
            var titleOutline = title.gameObject.AddComponent<UnityEngine.UI.Outline>();
            titleOutline.effectColor = new Color(.025f, .13f, .19f, .9f); titleOutline.effectDistance = new Vector2(1.2f, -1.2f);
            _ui.AnchoredLabel("Subtitle", heading, "冰湖垂钓 · 看看这一钩的收获", new Vector2(.5f, .24f), new Vector2(0, 10), new Vector2(550, 46), 23);
            var menuRod = _ui.Picture("Menu rod", _menu.transform, assets.rod, new Vector2(0, 20), new Vector2(520, 235));
            _ui.Label("Menu promise", _menu.transform, "潜入冰湖，跃起惊喜", new Vector2(0, -150), new Vector2(600, 54), 26, color: new Color(.80f, .93f, .98f));
            var startButton = _ui.Button("Start dive", _menu.transform, "开始游戏", Vector2.zero, new Vector2(560, 88), StartPressed);
            var tutorialButton = _ui.Button("How to play", _menu.transform, "教学", Vector2.zero, new Vector2(260, 82), () => OpenTutorial(false), false);
            var quitButton = _ui.Button("Quit", _menu.transform, "退出", Vector2.zero, new Vector2(260, 78), quit, false);
            UiWidgets.Place((RectTransform)startButton.transform, new Vector2(.5f, 0), new Vector2(0, 326), new Vector2(560, 88));
            UiWidgets.Place((RectTransform)tutorialButton.transform, new Vector2(.5f, 0), new Vector2(-142, 216), new Vector2(260, 82));
            var catalogButton = _ui.Button("Open catalog", _menu.transform, "鱼类图鉴", Vector2.zero, new Vector2(260, 82), OpenCatalog, false);
            UiWidgets.Place((RectTransform)catalogButton.transform, new Vector2(.5f, 0), new Vector2(142, 216), new Vector2(260, 82));
            UiWidgets.Place((RectTransform)quitButton.transform, new Vector2(.5f, 0), new Vector2(-142, 110), new Vector2(260, 78));
            var settingsButton = _ui.Button("Menu settings", _menu.transform, "设置", Vector2.zero, new Vector2(260, 78), OpenSettings, false);
            UiWidgets.Place((RectTransform)settingsButton.transform, new Vector2(.5f, 0), new Vector2(142, 110), new Vector2(260, 78));
            _menuHint = _ui.AnchoredLabel("Menu hint", _menu.transform, "", new Vector2(.5f, 0), new Vector2(0, 38), new Vector2(600, 32), 18, color: UiWidgets.Muted);
            var menuFish = new[] { assets.blueFish, assets.silverFish, assets.purpleFish };
            foreach (var fish in species)
            {
                if (fish.speciesId == "blue") menuFish[0] = fish.sprite;
                else if (fish.speciesId == "silver") menuFish[1] = fish.sprite;
                else if (fish.speciesId == "red") menuFish[2] = fish.sprite;
            }
            _menuPresentation = _menu.AddComponent<MenuPresentation>();
            _menuPresentation.Bind(_ui, menuRod.rectTransform, new[] { startButton, tutorialButton, catalogButton, quitButton, settingsButton }, menuFish);

            _hud = _ui.Overlay("HUD", _safe, 0);
            // One shared background groups the data; separate columns reserve room even at pulse peaks.
            _stats = _ui.Panel("Stats", _hud.transform, new Color(.024f, .169f, .251f, .80f));
            _stats.anchorMin = _stats.anchorMax = new Vector2(0, 1);
            _stats.pivot = new Vector2(0, 1); _stats.anchoredPosition = new Vector2(24, -24);
            _stats.sizeDelta = new Vector2(288, 140);
            var names = new[] { "保护", "主钩", "深度", "分支" };
            var sprites = new[] { assets.protection, assets.hook, assets.depth, assets.hook };
            for (int i = 0; i < 4; i++)
            {
                var chip = UiWidgets.Rect("HUD chip " + i, _stats);
                chip.anchorMin = chip.anchorMax = new Vector2(0, 1);
                chip.pivot = new Vector2(0, 1); chip.anchoredPosition = new Vector2(0, -10 - i * 40);
                chip.sizeDelta = new Vector2(288, 40);
                _icons[i] = _ui.Picture("HUD icon " + i, chip, sprites[i], Vector2.zero, new Vector2(28, 28));
                UiWidgets.Place(_icons[i].rectTransform, new Vector2(0, .5f), new Vector2(26, 0), new Vector2(28, 28));
                var nameLabel = _ui.AnchoredLabel("HUD name " + i, chip, names[i], new Vector2(0, .5f), new Vector2(50, 0), new Vector2(44, 32), 20, TextAnchor.MiddleLeft, new Color(.827f, .898f, .929f));
                nameLabel.fontStyle = FontStyle.Bold;
                nameLabel.rectTransform.pivot = new Vector2(0, .5f);
                _values[i] = _ui.AnchoredLabel("HUD value " + i, chip, "", new Vector2(1, .5f), new Vector2(-12, 0), new Vector2(160, 36), 24, TextAnchor.MiddleRight, HudWhite);
                _values[i].fontStyle = FontStyle.Bold;
                _values[i].rectTransform.pivot = new Vector2(1, .5f);
                _values[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                if (i == 3) _branchChip = chip.gameObject;
            }
            _branchOpacity = _branchChip.AddComponent<CanvasGroup>();
            _branchOpacity.blocksRaycasts = _branchOpacity.interactable = false;
            _branchOpacity.alpha = 0;
            _branchChip.SetActive(false);
            _dragHint = _ui.AnchoredLabel("Drag hint", _hud.transform, "按住屏幕，左右拖动", new Vector2(.5f, 0), new Vector2(0, 64), new Vector2(600, 36), 17, color: UiWidgets.Muted);
            _countdown = _ui.Label("Countdown", _hud.transform, "", new Vector2(0, 100), new Vector2(320, 190), 112);
            _pauseButton = PauseButton(pause);
            _reeling = new ReelingView(_ui, _hud.transform, _safe, camera, species);

            _result = new ResultView(_ui, _safe, species, restart, exit);
            Surface = new SurfacePresentation(_ui, _safe, species, camera);
            _pause = _ui.Overlay("Pause", _safe, .67f); _pauseFrame = _ui.Frame("Pause card", _pause.transform);
            _ui.Label("Paused", _pauseFrame, "已暂停", new Vector2(0, 225), new Vector2(500, 86), 44);
            _ui.Label("Pause note", _pauseFrame, "休息一下，鱼群也会等你", new Vector2(0, 150), new Vector2(500, 60), 22, color: UiWidgets.Muted);
            _ui.Button("Resume", _pauseFrame, "继续", new Vector2(0, 38), new Vector2(480, 84), resume);
            _ui.Button("Restart", _pauseFrame, "重开", new Vector2(0, -74), new Vector2(480, 84), restart, false);
            _ui.Button("Pause settings", _pauseFrame, "设置", new Vector2(-125, -186), new Vector2(230, 84), OpenSettings, false);
            _ui.Button("Back to menu", _pauseFrame, "退出", new Vector2(125, -186), new Vector2(230, 84), exit, false);
            _tutorial = new TutorialView(_ui, _safe, TutorialClosed, controls);
            _settings = new FeedbackSettingsView(_ui, _safe, feedback, controls);
            _catalog = new FishCatalogView(_ui, _safe, species, CloseCatalog);
            controls.Changed += RefreshControlHints; RefreshControlHints();
            ApplySafeArea(); ShowMenu();
        }
        /// <summary>Builds the pause key with the shared button face and a two-bar icon.</summary>
        private Button PauseButton(Action pause)
        {
            var button = _ui.Button("Pause button", _hud.transform, "", Vector2.zero, new Vector2(78, 78), pause, false);
            var rect = (RectTransform)button.transform;
            UiWidgets.Place(rect, Vector2.one, new Vector2(-57, -57), new Vector2(78, 78));
            var face = button.targetGraphic.rectTransform;
            for (int i = 0; i < 2; i++)
            {
                var bar = UiWidgets.Rect("Pause bar " + i, face);
                var image = bar.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = UiWidgets.White; image.raycastTarget = false;
                bar.anchorMin = bar.anchorMax = new Vector2(.5f, .5f); bar.pivot = new Vector2(.5f, .5f);
                bar.sizeDelta = new Vector2(6, 24); bar.anchoredPosition = new Vector2(i == 0 ? -8 : 8, 0);
            }
            return button;
        }
        private void StartPressed()
        { if (PlayerPrefs.GetInt(TutorialPreferenceKey, 0) == 0) OpenTutorial(true); else _start(); }
        private void OpenTutorial(bool startAfterClose) { _menu.SetActive(false); _tutorial.Open(startAfterClose); }
        private void OpenSettings() { _settings.Open(); }
        private void OpenCatalog() { _menu.SetActive(false); _catalog.Open(); }
        private void CloseCatalog() { _catalog.Close(); _menu.SetActive(true); }
        private void RefreshControlHints()
        {
            _menuHint.text = _controls.EffectiveMode == FishingControlMode.Tilt
                ? "左右轻倾移动鱼钩 · 设置中可校准"
                : (Application.isMobilePlatform ? "拖动移动 · 点按 / 原地按住收线" : "鼠标拖动选路 · 空格点按 / 按住收线");
        }
        private void TutorialClosed(bool startAfterClose)
        {
            if (!startAfterClose) { _menu.SetActive(true); return; }
            PlayerPrefs.SetInt(TutorialPreferenceKey, 1); PlayerPrefs.Save(); _start();
        }
        /// <summary>Closes tutorial back navigation before the controller interprets Escape as pause.</summary>
        public bool HandleBack()
        {
            if (_settings.Root.activeSelf) { _settings.Close(); return true; }
            if (IsCatalogOpen) { CloseCatalog(); return true; }
            if (!IsTutorialOpen) return false; _tutorial.Close(); return true;
        }
        /// <summary>Displays the start page without changing authoritative run data.</summary>
        public void ShowMenu()
        { _menu.SetActive(true); _hud.SetActive(false); _result.Root.SetActive(false); _pause.SetActive(false); _tutorial.Root.SetActive(false); Surface.Reset(); _feedbackView.Reset(); _settings.Close(); _catalog.Close(); }
        /// <summary>Clears all queued presentation from the previous run.</summary>
        public void ResetRun()
        {
            Surface.Reset(); _feedbackView.Reset(); _settings.Close(); _catalog.Close(); _reeling.ResetRun();
            _branchShown = false; _branchChip.SetActive(false); _branchOpacity.alpha = 0;
            _stats.sizeDelta = new Vector2(288, 140); _protectionFlash = 0;
            for (int i = 0; i < 4; i++) { _pulse[i] = 0; _values[i].rectTransform.localScale = Vector3.one; }
        }
        /// <summary>Draws the control bar from the authoritative model without intercepting gameplay touches.</summary>
        public void Reeling(RunModel run, Vector3 hook, Vector2 shake, float dt)
        {
            _reeling.Refresh(run, hook, shake, dt);
            bool diveHint = run != null && !run.IsPaused && run.State == RunState.Diving &&
                run.GameTime < run.Rules.CountdownSeconds + 6;
            bool reelHint = run != null && !run.IsPaused && run.State == RunState.Ascending &&
                run.Rules.Reeling.Enabled && !run.IsFull && run.TotalCaptureCount > 0 && run.Reeling.Active;
            bool tilt = _controls.EffectiveMode == FishingControlMode.Tilt;
            _dragHint.text = Application.isMobilePlatform
                ? (reelHint ? (tilt ? "左右轻倾选路 · 空白处按住收线" : "拖动选路 · 点按 / 原地按住收线")
                    : (tilt ? "左右轻倾移动 · 回正停住" : "左右拖动鱼钩，不分操作区域"))
                : (reelHint ? "拖动选路 · 点按 / 按住空格收线" : "按住鼠标，左右拖动");
            _dragHint.gameObject.SetActive(diveHint || reelHint);
        }
        /// <summary>Shows capture feedback only after the model has committed its unique record.</summary>
        public void Capture(CaptureRecord record, Vector3 world) { _feedbackView.Capture(record, world); }
        /// <summary>Shows a triggered skill only after the model committed the treasure pickup.</summary>
        public void Treasure(SkillTreasureKind kind, Vector3 world) { _feedbackView.Treasure(kind, world); }
        /// <summary>Displays a loss only after its original record has been removed.</summary>
        public void Escaped(CaptureRecord record, string name, Vector3 world) { _feedbackView.Escape(record, UiText.Species(record.SpeciesId, name), world); }
        /// <summary>Shows a burst only when a protection charge is consumed.</summary>
        public void ProtectionBurst(Vector3 world) { _protectionFlash = .16f; _feedbackView.Burst(world); }
        /// <summary>Refreshes numeric presentation from model events, including inactive protection and full-load color.</summary>
        public void Refresh(RunModel run)
        {
            SetValue(0, $"{run.Protection} / {run.Rules.ProtectionCharges}");
            SetValue(1, $"{run.MainCaptureCount} / {run.Rules.MainCapacity}");
            SetValue(2, $"{Mathf.FloorToInt(run.Depth)} / {run.Rules.MaxDepth:0} 米");
            int capacity = _abilities != null ? _abilities.Branches.Effect.HookCount * _abilities.Branches.Effect.Capacity : 0;
            SetValue(3, $"{run.BranchCaptureCount} / {capacity}");
            bool protectionAvailable = run.Protection > 0 && (run.State == RunState.Countdown ||
                run.State == RunState.Diving && run.Depth < run.Rules.BottomZoneDepth);
            _protectionColor = protectionAvailable ? Color.white : new Color(.48f, .60f, .68f, .65f);
            _icons[0].color = Color.Lerp(_protectionColor, Color.white, _protectionFlash / .16f);
            _values[0].color = protectionAvailable ? HudWhite : HudMuted;
            _values[1].color = run.IsFull ? HudFull : HudWhite;
            bool branchActive = _abilities != null && _abilities.Branches.Model.Active;
            _values[3].color = branchActive ? HudBranch : HudMuted;
            bool showBranch = branchActive || run.BranchCaptureCount > 0;
            if (showBranch != _branchShown)
            {
                _branchShown = showBranch;
                _branchChip.SetActive(showBranch);
            }
        }
        /// <summary>Renders ability model events without querying scene objects.</summary>
        public void SetAbilities(RunModel run, AbilityController abilities)
        { _abilities = abilities; Refresh(run); }
        private void SetValue(int index, string value)
        {
            if (_values[index].text == value) return;
            // Continuous depth readings stay still; only committed changes to counts need a pulse.
            if (index != 2 && _values[index].text.Length > 0 && _run != null &&
                _run.State != RunState.Countdown && _run.State != RunState.Menu) _pulse[index] = .14f;
            _values[index].text = value;
        }
        /// <summary>Updates overlay visibility and control availability on state events.</summary>
        public void StateChanged(RunModel run)
        {
            _run = run;
            _catalog.Close();
            if (run.State == RunState.Surfacing) Surface.Begin(run); else Surface.Hide();
            if (run.State == RunState.Surfacing || run.State == RunState.Result) _feedbackView.Reset();
            _menu.SetActive(false); _hud.SetActive(run.State != RunState.Result && run.State != RunState.Surfacing); _result.Root.SetActive(run.State == RunState.Result);
            _pause.SetActive(false); _tutorial.Root.SetActive(false);
            _countdown.text = run.State == RunState.Countdown ? Mathf.CeilToInt(run.Rules.CountdownSeconds).ToString() : "";
            _pauseButton.interactable = run.State == RunState.Diving || run.State == RunState.BottomHold || run.State == RunState.Ascending;
            Refresh(run);
        }
        /// <summary>Animates numeric feedback only on the controller's paused-aware gameplay clock.</summary>
        public void Tick(float dt)
        {
            if (_run != null) { _feedbackView.Tick(_run, dt); Surface.Tick(dt); }
            _result.Tick(dt);
            _protectionFlash = Mathf.Max(0, _protectionFlash - dt);
            _icons[0].color = Color.Lerp(_protectionColor, Color.white, _protectionFlash / .16f);
            float height = Mathf.MoveTowards(_stats.sizeDelta.y, _branchShown ? 180 : 140, dt * 40 / .14f);
            _stats.sizeDelta = new Vector2(288, height);
            _branchOpacity.alpha = Mathf.Clamp01((height - 140) / 40);
            for (int i = 0; i < 4; i++)
            {
                _pulse[i] = Mathf.Max(0, _pulse[i] - dt);
                float scale = _pulse[i] > 0 ? 1 + .055f * Mathf.Sin((1 - _pulse[i] / .14f) * Mathf.PI) : 1;
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
        public void Pause(bool paused) { _pause.SetActive(paused); if (paused) { _reeling.Hide(); _dragHint.gameObject.SetActive(false); } }
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
            _tutorial.Layout(_safe.rect.size); _result.Layout(_safe.rect.size); _catalog.Layout(_safe.rect.size);
            _settings.Layout(_safe.rect.size);
            _menuPresentation.Layout(_safe.rect.size);
            UiWidgets.Place(_pauseFrame, Vector2.one * .5f, Vector2.zero, new Vector2(Mathf.Min(620, _safe.rect.width - 48), 720));
        }
        /// <summary>Releases scene-owned control listeners and surface presentation resources.</summary>
        public void Dispose()
        {
            _controls.Changed -= RefreshControlHints;
            _settings.Dispose(); Surface.Dispose();
        }
    }
}
