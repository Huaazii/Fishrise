using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Quiet menu-only rod motion and distant fish; active without creating or advancing a fishing run.</summary>
    public sealed class MenuPresentation : MonoBehaviour
    {
        private RectTransform _rod, _menu;
        private RectTransform[] _buttons;
        private readonly Image[] _fish = new Image[3];
        private readonly Vector2[] _buttonPositions = new Vector2[5];
        private MenuAmbientGraphic _ambient;
        private Vector2 _rodOrigin;
        private float _clock;
        private bool _background;

        internal void Bind(UiWidgets ui, RectTransform rod, Button[] buttons, Sprite[] fish)
        {
            _menu = (RectTransform)transform; _rod = rod; _rodOrigin = rod.anchoredPosition;
            _buttons = new RectTransform[buttons.Length];
            for (int i = 0; i < buttons.Length; i++) { _buttons[i] = (RectTransform)buttons[i].transform; _buttonPositions[i] = _buttons[i].anchoredPosition; }
            var back = UiWidgets.Rect("Menu atmosphere", transform); back.SetAsFirstSibling();
            _ambient = back.gameObject.AddComponent<MenuAmbientGraphic>(); _ambient.raycastTarget = false;
            for (int i = 0; i < _fish.Length; i++)
            {
                _fish[i] = ui.Picture("Menu distant fish " + i, back, fish[i], Vector2.zero, new Vector2(90 + i * 18, 58 + i * 8));
                _fish[i].color = new Color(.29f, .62f, .76f, .13f + i * .015f);
            }
        }
        internal void Layout(Vector2 available)
        {
            float lift = Mathf.Clamp((available.y - 1280) * .30f, 0, 100);
            for (int i = 0; i < _buttons.Length; i++) _buttons[i].anchoredPosition = _buttonPositions[i] + Vector2.up * lift;
        }
        private void Update()
        {
            if (_rod == null || _background) return;
            _clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            _rod.anchoredPosition = _rodOrigin + new Vector2(Mathf.Sin(_clock * .43f) * 3, Mathf.Sin(_clock * .73f) * 5);
            _rod.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_clock * .55f) * 1.3f);
            _ambient.Clock = _clock; _ambient.SetVerticesDirty();
            float width = _menu.rect.width;
            for (int i = 0; i < _fish.Length; i++)
            {
                float direction = i == 1 ? -1 : 1;
                float x = (-width * .6f + Mathf.Repeat(_clock * (12 + i * 3) + (i + .2f) * width * .4f, width * 1.2f)) * direction;
                float y = _menu.rect.height * .07f + (i - 1) * 85 + Mathf.Sin(_clock * .28f + i) * 20;
                _fish[i].rectTransform.anchoredPosition = new Vector2(x, y);
                _fish[i].rectTransform.localScale = new Vector3(direction, 1 + Mathf.Sin(_clock * 2 + i) * .025f, 1);
            }
        }
        private void OnDisable()
        { if (_rod != null) { _rod.anchoredPosition = _rodOrigin; _rod.localRotation = Quaternion.identity; } }
        private void OnApplicationPause(bool paused) { _background = paused; }
    }
}
