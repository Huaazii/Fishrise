using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Fishing.Game
{
    /// <summary>Adapts mobile gestures or calibrated tilt, retaining mouse/Space controls on desktop.</summary>
    public sealed class DragInput : IDisposable
    {
        private const float RelativeDragGain = 1.25f;
        private readonly FishingControlSettings _settings;
        private readonly FishingTouchRouter _touches = new FishingTouchRouter();
        private readonly TiltSteering _tilt = new TiltSteering();
        private readonly bool _useGyro;
        private bool _dragging, _spaceArmed, _ownsGyro, _gyroWasEnabled;
        private float _startPointerX, _startHookX, _hookX;
        private FishingControlMode _configuredMode;
        /// <summary>Retrieval intent independent of horizontal steering.</summary>
        public bool ReelHeld { get; private set; }
        /// <summary>Current requested horizontal world coordinate.</summary>
        public float TargetX { get; private set; }
        /// <summary>Binds scene-owned preferences and restores any gyro state it changes on disposal.</summary>
        public DragInput(FishingControlSettings settings)
        {
            _settings = settings; _useGyro = SystemInfo.supportsGyroscope;
            settings.Changed += SettingsChanged; settings.CalibrationRequested += CalibrateTilt;
            SettingsChanged();
        }
        /// <summary>Resets ownership and motion on state changes, pause and restart.</summary>
        public void Reset(float hookX)
        {
            _hookX = TargetX = hookX; _dragging = false;
            _touches.Reset(); _tilt.ResetMotion(); ReelHeld = _spaceArmed = false;
        }
        private void SettingsChanged()
        {
            Reset(_hookX);
            var mode = _settings.EffectiveMode;
            if (mode == FishingControlMode.Tilt)
            {
                if (_useGyro && !_ownsGyro)
                {
                    _gyroWasEnabled = Input.gyro.enabled; Input.gyro.enabled = true; _ownsGyro = true;
                }
                if (_configuredMode != mode || (!_tilt.IsCalibrated && !_tilt.IsCalibrating)) CalibrateTilt();
            }
            else
            {
                RestoreGyro();
                _settings.SetCalibrationStatus(_settings.Mode == FishingControlMode.Tilt
                    ? "设备不支持倾斜，当前使用拖动操控" : "当前使用拖动，无需校准");
            }
            _configuredMode = mode;
        }
        /// <summary>Captures a fresh natural grip in the menu, pause sheet or new-run countdown.</summary>
        public void CalibrateTilt()
        {
            if (_settings.EffectiveMode != FishingControlMode.Tilt) return;
            _tilt.BeginCalibration();
            _settings.SetCalibrationStatus("保持自然握姿约 0.35 秒，正在校准");
        }
        /// <summary>Updates only the sensor, including while the gameplay clock is paused for calibration.</summary>
        public void SampleTilt(float dt)
        {
            if (_settings.EffectiveMode != FishingControlMode.Tilt) return;
            bool calibrating = _tilt.IsCalibrating;
            _tilt.Sample(_useGyro ? Input.gyro.gravity : Input.acceleration, dt, _settings.Sensitivity);
            if (calibrating && _tilt.IsCalibrated) _settings.SetCalibrationStatus("校准完成，回正停住，左右轻倾移动");
        }
        /// <summary>Maps touch gestures on mobile; desktop keeps mouse dragging and Space reeling.</summary>
        public void Read(Camera camera, float hookX, bool followTouchPoint, bool allowReel = false)
        {
            _hookX = hookX;
            bool space = Input.GetKey(KeyCode.Space);
            if (!space) _spaceArmed = true;
            ReelHeld = allowReel && space && _spaceArmed;
            if (Application.isMobilePlatform || Input.touchCount > 0)
            {
                bool drag = _settings.EffectiveMode == FishingControlMode.Drag;
                _touches.BeginFrame(Screen.safeArea, allowReel, drag, Time.unscaledTime,
                    Mathf.Max(6, Screen.width * (12f / 720)));
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    _touches.Process(touch.fingerId, touch.position, touch.phase, OverUi(touch.fingerId));
                }
                _touches.EndFrame();
                if (_touches.SteeringStarted) Begin(_touches.SteeringOriginX, hookX);
                _dragging = _touches.Steering;
                if (_dragging) Apply(camera, _touches.PointerX, followTouchPoint);
                ReelHeld |= allowReel && _touches.Reeling;
                return;
            }
            _touches.Reset();
            if (Input.GetMouseButtonDown(0) && !OverUi(-1)) Begin(Input.mousePosition.x, hookX);
            if (!Input.GetMouseButton(0)) _dragging = false;
            if (_dragging) Apply(camera, Input.mousePosition.x, followTouchPoint);
        }
        /// <summary>Integrates tilt velocity on the physics clock, without accumulating an unconsumed movement target.</summary>
        public float MovementTarget(float hookX, float speed, float dt)
        {
            _hookX = hookX;
            if (_settings.EffectiveMode == FishingControlMode.Tilt)
                TargetX = hookX + _tilt.Horizontal * speed * dt;
            return TargetX;
        }
        private void Begin(float pointer, float hook)
        { _startPointerX = pointer; _startHookX = hook; _dragging = true; }
        private void Apply(Camera camera, float pointer, bool follow)
        {
            float width = camera.orthographicSize * 2 * camera.aspect;
            TargetX = follow ? (pointer / Screen.width - .5f) * width :
                _startHookX + (pointer - _startPointerX) / Screen.width * width * RelativeDragGain;
        }
        private static bool OverUi(int finger)
        { return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(finger); }
        private void RestoreGyro()
        {
            if (!_ownsGyro) return;
            Input.gyro.enabled = _gyroWasEnabled; _ownsGyro = false;
        }
        /// <summary>Releases preference listeners and only the gyro state owned by this adapter.</summary>
        public void Dispose()
        {
            _settings.Changed -= SettingsChanged; _settings.CalibrationRequested -= CalibrateTilt;
            RestoreGyro();
        }
    }
}
