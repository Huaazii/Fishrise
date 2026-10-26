using UnityEngine;

namespace Fishing.Game
{
    /// <summary>One shared settings sheet for menu and pause, using persisted feedback preferences.</summary>
    internal sealed class FeedbackSettingsView
    {
        internal readonly GameObject Root;
        private readonly RectTransform _card;
        private readonly UnityEngine.UI.Text _title, _hapticsNote, _controlModeNote, _calibrationStatus;
        private readonly UnityEngine.UI.Button _sound, _music, _haptics, _controlMode, _sensitivity, _calibrate, _close;
        private readonly GameFeedback _feedback;
        private readonly FishingControlSettings _controls;
        private Vector2 _available;
        internal FeedbackSettingsView(UiWidgets ui, Transform parent, GameFeedback feedback, FishingControlSettings controls)
        {
            _feedback = feedback; _controls = controls; Root = ui.Overlay("Feedback settings", parent, .8f);
            _card = ui.Frame("Settings card", Root.transform);
            _title = ui.Label("Settings title", _card, "设置", Vector2.zero, new Vector2(450, 70), 44);
            _music = ui.Button("Toggle music", _card, "", Vector2.zero, new Vector2(460, 82), () => { feedback.ToggleMusic(); Refresh(); });
            _sound = ui.Button("Toggle sound", _card, "", Vector2.zero, new Vector2(460, 82), () => { feedback.ToggleSound(); Refresh(); });
            _haptics = ui.Button("Toggle haptics", _card, "", Vector2.zero, new Vector2(460, 82), () => { feedback.ToggleHaptics(); Refresh(); });
            var noteColor = Color.Lerp(UiWidgets.Muted, UiWidgets.White, .25f);
            _hapticsNote = ui.Label("Haptics note", _card, "震动仅在支持的 Android 设备上生效", Vector2.zero, new Vector2(510, 36), 20, color: noteColor);
            _controlMode = ui.Button("Toggle control mode", _card, "", Vector2.zero, new Vector2(460, 82), ToggleControlMode);
            _controlModeNote = ui.Label("Control mode note", _card, "", Vector2.zero, new Vector2(510, 64), 21, color: noteColor);
            _sensitivity = ui.Button("Tilt sensitivity", _card, "", Vector2.zero, new Vector2(460, 82), CycleSensitivity, false);
            _calibrate = ui.Button("Calibrate tilt", _card, "校准倾斜", Vector2.zero, new Vector2(460, 82), controls.RequestCalibration, false);
            _calibrationStatus = ui.Label("Calibration status", _card, "", Vector2.zero, new Vector2(510, 48), 20, color: noteColor);
            _close = ui.Button("Close settings", _card, "返回", Vector2.zero, new Vector2(460, 76), Close, false);
            controls.Changed += Refresh;
            controls.CalibrationStatusChanged += RefreshCalibrationStatus;
            Layout(new Vector2(720, 1602));
            Refresh(); Close();
        }
        internal void Open() { Refresh(); Root.SetActive(true); Root.transform.SetAsLastSibling(); }
        internal void Close() { Root.SetActive(false); }
        internal void Dispose()
        {
            _controls.Changed -= Refresh;
            _controls.CalibrationStatusChanged -= RefreshCalibrationStatus;
        }
        internal void Layout(Vector2 available)
        {
            _available = available;
            bool compact = available.y < 1100;
            bool tilt = _controls.Mode == FishingControlMode.Tilt;
            bool activeTilt = _controls.EffectiveMode == FishingControlMode.Tilt;
            float width = Mathf.Min(600, Mathf.Max(320, available.x - (compact ? 24 : 48)));
            float desiredHeight = tilt ? (activeTilt ? 992 : 888) : 768;
            if (compact) desiredHeight = tilt ? (activeTilt ? 688 : 616) : 536;
            float height = Mathf.Min(desiredHeight, Mathf.Max(720, available.y - (compact ? 24 : 48)));
            UiWidgets.Place(_card, Vector2.one * .5f, Vector2.zero, new Vector2(width, height));
            float itemWidth = Mathf.Min(460, width - 70);
            float noteWidth = Mathf.Min(510, width - 54);
            _title.fontSize = compact ? 32 : 44;
            _hapticsNote.fontSize = compact ? 19 : 20;
            _controlModeNote.fontSize = compact ? 19 : 20;
            _calibrationStatus.fontSize = compact ? 19 : 20;
            int primaryFont = compact ? 26 : 30, secondaryFont = compact ? 24 : 27;
            ButtonFont(_music, primaryFont); ButtonFont(_sound, primaryFont);
            ButtonFont(_haptics, primaryFont); ButtonFont(_controlMode, primaryFont);
            ButtonFont(_sensitivity, secondaryFont); ButtonFont(_calibrate, secondaryFont); ButtonFont(_close, secondaryFont);
            if (compact)
            {
                Place(_title.rectTransform, 0, height * .5f - 48, noteWidth, 52);
                Place(_music.transform, 0, height * .5f - 110, itemWidth, 54);
                Place(_sound.transform, 0, height * .5f - 170, itemWidth, 54);
                Place(_haptics.transform, 0, height * .5f - 230, itemWidth, 54);
                Place(_hapticsNote.rectTransform, 0, height * .5f - 280, noteWidth, 32);
                Place(_controlMode.transform, 0, height * .5f - 330, itemWidth, 54);
                Place(_controlModeNote.rectTransform, 0, height * .5f - 386, noteWidth, 32);
                Place(_sensitivity.transform, 0, height * .5f - (activeTilt ? 398 : 446), itemWidth, 54);
                Place(_calibrate.transform, 0, height * .5f - 458, itemWidth, 54);
                Place(_calibrationStatus.rectTransform, 0, height * .5f - 514, noteWidth, 44);
                Place(_close.transform, 0, -height * .5f + 91, itemWidth, 54);
                return;
            }
            Place(_title.rectTransform, 0, height * .5f - 66, noteWidth, 70);
            Place(_music.transform, 0, height * .5f - 168, itemWidth, 82);
            Place(_sound.transform, 0, height * .5f - 268, itemWidth, 82);
            Place(_haptics.transform, 0, height * .5f - 368, itemWidth, 82);
            Place(_hapticsNote.rectTransform, 0, height * .5f - 428, noteWidth, 36);
            Place(_controlMode.transform, 0, height * .5f - 500, itemWidth, 82);
            Place(_controlModeNote.rectTransform, 0, height * .5f - 572, noteWidth, 40);
            Place(_sensitivity.transform, 0, height * .5f - (activeTilt ? 600 : 648), itemWidth, 82);
            Place(_calibrate.transform, 0, height * .5f - 700, itemWidth, 82);
            Place(_calibrationStatus.rectTransform, 0, height * .5f - 764, noteWidth, 48);
            Place(_close.transform, 0, -height * .5f + 134, itemWidth, 76);
        }
        private static void Place(Transform transform, float x, float y, float width, float height)
        { UiWidgets.Place((RectTransform)transform, Vector2.one * .5f, new Vector2(x, y), new Vector2(width, height)); }
        private static void ButtonFont(UnityEngine.UI.Button button, int size)
        { button.GetComponentInChildren<UnityEngine.UI.Text>(true).fontSize = size; }
        private void ToggleControlMode()
        { _controls.Mode = _controls.Mode == FishingControlMode.Drag ? FishingControlMode.Tilt : FishingControlMode.Drag; }
        private void CycleSensitivity()
        {
            if (_controls.Sensitivity < 1f) _controls.Sensitivity = 1f;
            else if (_controls.Sensitivity < 1.5f) _controls.Sensitivity = 1.5f;
            else _controls.Sensitivity = .75f;
        }
        private void Refresh()
        {
            UiWidgets.ButtonText(_sound, "音效：" + (_feedback.SoundEnabled ? "开" : "关"));
            UiWidgets.ButtonText(_music, "音乐：" + (_feedback.MusicEnabled ? "开" : "关"));
            UiWidgets.ButtonText(_haptics, "震动：" + (_feedback.HapticsEnabled ? "开" : "关"));
            UiWidgets.ButtonText(_controlMode, "操控：" + (_controls.Mode == FishingControlMode.Tilt ? "倾斜" : "拖动"));
            bool tilt = _controls.Mode == FishingControlMode.Tilt;
            bool activeTilt = _controls.EffectiveMode == FishingControlMode.Tilt && _controls.TiltAvailable;
            _sensitivity.gameObject.SetActive(tilt);
            _calibrate.gameObject.SetActive(activeTilt);
            _calibrationStatus.gameObject.SetActive(activeTilt);
            _controlModeNote.gameObject.SetActive(tilt && !activeTilt);
            if (tilt) UiWidgets.ButtonText(_sensitivity, "倾斜灵敏度：" + SensitivityText(_controls.Sensitivity));
            _calibrate.interactable = activeTilt;
            _controlModeNote.text = "此设备不支持倾斜, 已改用拖动";
            RefreshCalibrationStatus();
            Layout(_available);
        }
        private void RefreshCalibrationStatus()
        {
            if (!string.IsNullOrEmpty(_controls.CalibrationStatus)) _calibrationStatus.text = _controls.CalibrationStatus;
            else if (_controls.EffectiveMode == FishingControlMode.Tilt) _calibrationStatus.text = "尚未校准";
            else _calibrationStatus.text = "切换到倾斜操控后可校准";
        }
        private static string SensitivityText(float sensitivity)
        {
            if (sensitivity < 1f) return "0.75×";
            if (sensitivity > 1f) return "1.5×";
            return "1.0×";
        }
    }
}
