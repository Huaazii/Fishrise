using UnityEngine;
using UnityEngine.EventSystems;

namespace Fishing.Game
{
    /// <summary>Shared relative-pointer mapping for editor mouse and Android touch.</summary>
    public sealed class DragInput
    {
        private bool _dragging;
        private float _startPointerX;
        private float _startHookX;
        private int _fingerId;
        /// <summary>Current requested horizontal world coordinate.</summary>
        public float TargetX { get; private set; }
        /// <summary>Resets dragging on state changes, pause and restart.</summary>
        public void Reset(float hookX) { _dragging = false; TargetX = hookX; _fingerId = -1; }
        /// <summary>Maps the same pointer displacement to world space on both input devices.</summary>
        public void Read(Camera camera, float hookX, bool followTouchPoint)
        {
            if (Input.touchCount > 0)
            {
                if (!_dragging)
                {
                    var first = Input.GetTouch(0);
                    if (first.phase != TouchPhase.Began || OverUi(first.fingerId)) return;
                    Begin(first.position.x, hookX, first.fingerId);
                }
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != _fingerId) continue;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) _dragging = false;
                    else Apply(camera, touch.position.x, followTouchPoint);
                    return;
                }
                _dragging = false; return;
            }
            if (Input.GetMouseButtonDown(0) && !OverUi(-1)) Begin(Input.mousePosition.x, hookX, -1);
            if (!Input.GetMouseButton(0)) _dragging = false;
            if (_dragging) Apply(camera, Input.mousePosition.x, followTouchPoint);
        }
        private void Begin(float pointer, float hook, int finger)
        { _startPointerX = pointer; _startHookX = hook; _fingerId = finger; _dragging = true; }
        private void Apply(Camera camera, float pointer, bool follow)
        {
            float width = camera.orthographicSize * 2 * camera.aspect;
            TargetX = follow ? (pointer / Screen.width - .5f) * width :
                _startHookX + (pointer - _startPointerX) / Screen.width * width;
        }
        private static bool OverUi(int finger)
        { return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(finger); }
    }
}
