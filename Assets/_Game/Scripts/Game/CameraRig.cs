using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Width-matched portrait framing with a half-second direction change and a fixed surface view.</summary>
    public sealed class CameraRig
    {
        private readonly Camera _camera;
        private readonly FishingConfig _config;
        private float _offset = -.4f, _fromOffset = -.4f, _targetOffset = -.4f, _transition = .5f;
        private RunState _lastState = RunState.Menu;
        /// <summary>Creates a basic follow rig without a camera package dependency.</summary>
        public CameraRig(Camera camera, FishingConfig config) { _camera = camera; _config = config; }
        /// <summary>Repositions immediately for a clean restart.</summary>
        public void Reset()
        {
            _offset = _fromOffset = _targetOffset = -.4f; _transition = .5f; _lastState = RunState.Menu;
            Follow(null, 0, true);
        }
        /// <summary>Follows current depth; caller skips this while paused.</summary>
        public void Follow(RunModel run, float dt, bool immediate = false)
        {
            if (run != null && run.IsPaused) return;
            // Reserve room for the ice faces outside the unchanged gameplay boundary.
            _camera.orthographicSize = (_config.halfWidth + 1.5f) / Mathf.Max(.1f, _camera.aspect);
            var state = run != null ? run.State : RunState.Menu;
            bool surface = state == RunState.Menu || state == RunState.Countdown ||
                state == RunState.Surfacing || state == RunState.Result;
            float targetOffset = surface ? -.4f : state == RunState.Ascending ? .6f : -.6f;
            if (state != _lastState)
            { _lastState = state; _fromOffset = _offset; _targetOffset = targetOffset; _transition = 0; }
            _transition = immediate ? .5f : Mathf.Min(.5f, _transition + dt);
            _offset = immediate ? targetOffset : Mathf.Lerp(_fromOffset, _targetOffset,
                Mathf.SmoothStep(0, 1, _transition / .5f));
            float surfaceY = -_camera.orthographicSize * .4f;
            // Stop the return camera at the same surface framing before the flow enters Surfacing.
            float y = surface ? surfaceY : Mathf.Min(surfaceY,
                -run.Depth * _config.unitsPerMetre + _offset * _camera.orthographicSize);
            _camera.transform.position = new Vector3(0, y, -10);
        }
    }
}
