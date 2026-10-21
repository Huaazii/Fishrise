using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>One shared settings sheet for menu and pause, using persisted feedback preferences.</summary>
    internal sealed class FeedbackSettingsView
    {
        internal readonly GameObject Root;
        private readonly Button _sound, _haptics;
        private readonly GameFeedback _feedback;
        internal FeedbackSettingsView(UiWidgets ui, Transform parent, GameFeedback feedback)
        {
            _feedback = feedback; Root = ui.Overlay("Feedback settings", parent, .8f);
            var card = ui.Frame("Settings card", Root.transform);
            UiWidgets.Place(card, Vector2.one * .5f, Vector2.zero, new Vector2(600, 580));
            ui.Label("Settings title", card, "设置", new Vector2(0, 190), new Vector2(450, 70), 44);
            _sound = ui.Button("Toggle sound", card, "", new Vector2(0, 68), new Vector2(460, 82), () => { feedback.ToggleSound(); Refresh(); });
            _haptics = ui.Button("Toggle haptics", card, "", new Vector2(0, -40), new Vector2(460, 82), () => { feedback.ToggleHaptics(); Refresh(); });
            ui.Label("Haptics note", card, "震动在支持的 Android 设备上生效", new Vector2(0, -108), new Vector2(510, 36), 19, color: UiWidgets.Muted);
            ui.Button("Close settings", card, "返回", new Vector2(0, -190), new Vector2(460, 76), Close, false);
            Close();
        }
        internal void Open() { Refresh(); Root.SetActive(true); Root.transform.SetAsLastSibling(); }
        internal void Close() { Root.SetActive(false); }
        private void Refresh()
        {
            UiWidgets.ButtonText(_sound, "音效：" + (_feedback.SoundEnabled ? "开" : "关"));
            UiWidgets.ButtonText(_haptics, "震动：" + (_feedback.HapticsEnabled ? "开" : "关"));
        }
    }
}
