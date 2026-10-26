using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Moves only a button's decorative surface, keeping its touch rectangle and text contrast stable.</summary>
    public sealed class IceButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ICancelHandler
    {
        private Button _button;
        private RectTransform _surface;
        private IceButtonGraphic _ice;
        private const float Raised = 7;
        private bool _primary;
        private Color _normalRim;
        private bool _held;
        private float _press, _velocity;

        /// <summary>Connects the shared button factory's visual child without replacing UGUI command handling.</summary>
        public void Bind(Button button, RectTransform surface, bool primary)
        {
            _button = button; _surface = surface; _primary = primary;
            _ice = surface.GetComponentInChildren<IceButtonGraphic>();
            _normalRim = _ice.FaceColor;
            ResetSurface();
        }

        /// <summary>Accepts only an eligible left mouse or touch press.</summary>
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && _button.IsInteractable()) _held = true; }
        /// <summary>Releases the surface while the Button itself decides whether to invoke its command.</summary>
        public void OnPointerUp(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) _held = false; }
        /// <summary>Leaving the fixed hit rectangle cancels the pressed appearance.</summary>
        public void OnPointerExit(PointerEventData data) { _held = false; }
        /// <summary>Cancellation clears a pending visual press.</summary>
        public void OnCancel(BaseEventData data) { _held = false; }

        private void Update()
        {
            if (_surface == null) return;
            if (!_button.IsInteractable()) _held = false;
            _press = Mathf.SmoothDamp(_press, _held ? 1 : 0, ref _velocity, _held ? .045f : .07f,
                Mathf.Infinity, Time.unscaledDeltaTime);
            _surface.anchoredPosition = new Vector2(0, Mathf.Lerp(Raised, 0, _press));
            _surface.localScale = Vector3.one * Mathf.Lerp(1, .975f, _press);
            _ice.color = _button.IsInteractable() ? _normalRim : new Color(.12f, .22f, .28f);
            if (_ice != null && _primary)
            {
                float phase = Mathf.Repeat(Time.unscaledTime, 7.5f);
                _ice.Glint = phase < .8f && !_held && _button.IsInteractable() ? phase / .8f : -1;
            }
        }
        private void ResetSurface()
        {
            _held = false; _press = _velocity = 0;
            if (_surface == null) return;
            _surface.anchoredPosition = new Vector2(0, Raised); _surface.localScale = Vector3.one;
            if (_ice != null) _ice.Glint = -1;
        }
        private void OnDisable() { ResetSurface(); }
        private void OnApplicationFocus(bool focused) { if (!focused) ResetSurface(); }
    }
}
