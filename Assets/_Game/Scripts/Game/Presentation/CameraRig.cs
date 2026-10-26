using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Buffered portrait follow, viewport seabed bounds and subtle accelerated-retrieval shake.</summary>
    public sealed class CameraRig
    {
        private readonly Camera _camera;
        private readonly FishingConfig _config;
        private readonly float _seabedY;
        private readonly Transform _hook;
        private System.Collections.Generic.IReadOnlyList<BranchHookView> _branches;
        private float _offset = -.4f, _fromOffset = -.4f, _targetOffset = -.4f, _transition = 1;
        private float _baseY, _velocity, _shakeTime, _shakeBlend;
        private float _surfaceImpact;
        private RunState _lastState = RunState.Menu;
        /// <summary>Current presentation-only displacement, separated from the smoothed follow position.</summary>
        public Vector2 ShakeOffset { get; private set; }
        /// <summary>Creates a basic follow rig without a camera package dependency.</summary>
        public CameraRig(Camera camera, FishingConfig config, float? seabedY = null, Transform hook = null)
        { _camera = camera; _config = config; _hook = hook; _seabedY = seabedY ?? -(config.maxDepth * config.unitsPerMetre + 3); }
        /// <summary>Uses the existing branch views to keep their complete carried silhouettes above the approaching view's bottom edge.</summary>
        public void BindBranches(System.Collections.Generic.IReadOnlyList<BranchHookView> branches) { _branches=branches; }
        /// <summary>Repositions immediately for a clean restart.</summary>
        public void Reset()
        {
            _offset = _fromOffset = _targetOffset = -.4f; _transition = 1; _lastState = RunState.Menu;
            _velocity = _shakeTime = _shakeBlend = _surfaceImpact = 0; ShakeOffset = Vector2.zero;
            Follow(null, 0, true);
        }
        /// <summary>Adds the brief water-break impulse after the close shot has already settled.</summary>
        public void PresentSurface(float elapsed, float duration, bool active)
        {
            float t = elapsed * 2.3f / Mathf.Max(.01f, duration);
            float impact = t - .08f;
            _surfaceImpact = active && impact >= 0 ? Mathf.Sin(impact * 65) * Mathf.Exp(-impact * 22) * .045f : 0;
        }
        /// <summary>Prepares the close shot during the last ascent, finishing before the hook reaches water.</summary>
        public float SurfaceFraming(RunModel run)
        {
            if (run == null) return 0;
            if (run.State == RunState.Surfacing || run.State == RunState.Result) return 1;
            if (run.State != RunState.Ascending) return 0;
            // Use the run's maximum speed so filling the hook midway cannot abruptly change the framing range.
            // Full load always triggers the five-times sprint, with or without the reeling extension.
            float speed = Mathf.Max(run.Rules.AscentSpeed * Mathf.Max(1, run.Rules.FullMultiplier),
                run.Rules.Reeling.Enabled ? run.Rules.Reeling.MaxSpeed : 0);
            float startDepth = Mathf.Max(12, speed * 1.4f + 1.5f);
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(startDepth, 1.5f, run.Depth));
        }
        /// <summary>Follows current depth; caller skips this while paused.</summary>
        public void Follow(RunModel run, float dt, bool immediate = false)
        {
            if ((run != null && run.IsPaused) || (!immediate && dt <= 0)) return;
            // Reserve room for the ice faces outside the unchanged gameplay boundary.
            var state = run != null ? run.State : RunState.Menu;
            float cinematic = SurfaceFraming(run);
            float halfWidth = Mathf.Lerp(_config.halfWidth + 1.5f, 4.7f, cinematic);
            _camera.orthographicSize = halfWidth / Mathf.Max(.1f, _camera.aspect);
            bool surface = state == RunState.Menu || state == RunState.Countdown ||
                state == RunState.Surfacing || state == RunState.Result;
            float targetOffset = surface ? -.4f : state == RunState.Ascending ? .6f : -.6f;
            if (state != _lastState)
            { _lastState = state; _fromOffset = _offset; _targetOffset = targetOffset; _transition = 0; }
            _transition = immediate ? 1 : Mathf.Min(1, _transition + dt);
            _offset = immediate ? targetOffset : Mathf.Lerp(_fromOffset, _targetOffset,
                Mathf.SmoothStep(0, 1, _transition / .5f));
            float surfaceY = -_camera.orthographicSize * .4f;
            // Bound the whole viewport, rather than just its centre, before smoothing the target.
            float bottomY = Mathf.Min(surfaceY, _seabedY + _camera.orthographicSize);
            float targetY = surface ? surfaceY : Mathf.Clamp(
                -run.Depth * _config.unitsPerMetre + _offset * _camera.orthographicSize, bottomY, surfaceY);
            if (immediate) { _baseY = targetY; _velocity = 0; }
            else
            {
                // Retain the stronger buffer through the direction blend, then release it gradually.
                float smoothTime = Mathf.Lerp(_config.cameraTurnSmoothTime, _config.cameraSmoothTime,
                    Mathf.SmoothStep(0, 1, (_transition - .5f) / .5f));
                float followSpeed = Mathf.Max(_config.diveSpeed, _config.ascentSpeed) * _config.unitsPerMetre;
                if (run != null && run.IsFull) followSpeed *= _config.fullMultiplier;
                // Changing screen composition must not make the camera race far ahead of hook motion.
                _baseY = Mathf.SmoothDamp(_baseY, targetY, ref _velocity, Mathf.Max(.01f, smoothTime), followSpeed * 1.5f, dt);
            }
            _baseY = Mathf.Clamp(_baseY, bottomY, surfaceY);
            if ((_baseY <= bottomY && _velocity < 0) || (_baseY >= surfaceY && _velocity > 0)) _velocity = 0;
            if (immediate) { _shakeTime = _shakeBlend = 0; ShakeOffset = Vector2.zero; }
            else
            {
                bool fast = state == RunState.Ascending && run.IsFull;
                _shakeTime += dt;
                _shakeBlend = Mathf.MoveTowards(_shakeBlend, fast ? 1 : 0, dt / .25f);
                float fade = surface ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(run.Depth * _config.unitsPerMetre / _camera.orthographicSize));
                float amplitude = _config.fastAscentShakeAmplitude * _config.unitsPerMetre * _shakeBlend * fade * (1 - cinematic);
                float phase = _shakeTime * _config.fastAscentShakeFrequency * Mathf.PI * 2;
                ShakeOffset = new Vector2(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f + 1.3f) * .45f) * amplitude;
                if (state == RunState.Surfacing) ShakeOffset += new Vector2(_surfaceImpact * .35f, _surfaceImpact);
            }
            // Shake is applied last, so it also has to respect both framing limits.
            float y = Mathf.Clamp(_baseY + ShakeOffset.y, bottomY, surfaceY);
            ShakeOffset = new Vector2(ShakeOffset.x, y - _baseY);
            // Lower the ice shelf to 38% height, reserving the upper view for the larger actor and released fish.
            y = Mathf.Lerp(y, .98f + _camera.orthographicSize * .24f, cinematic);
            // Keep the approaching hook above the lower 12% until the water surface can take over the composition.
            if (state == RunState.Ascending && cinematic > 0)
            {
                y = Mathf.Min(y, -run.Depth * _config.unitsPerMetre + _camera.orthographicSize * .76f);
                if(_branches!=null) foreach(var branch in _branches)
                    if(branch.gameObject.activeSelf) y=Mathf.Min(y,branch.RetrievalBounds.min.y+_camera.orthographicSize*.94f);
            }
            // Retain this same centre through surfacing: a side return must not cause a horizontal cut at water entry.
            float closeX = _hook != null ? Mathf.Clamp(0, _hook.position.x - 4.2f, _hook.position.x + 4.2f) : 0;
            _camera.transform.position = new Vector3(closeX * cinematic + ShakeOffset.x, y, -10);
        }
    }
}
