using System;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Player-selected steering scheme, persisted independently from any calibrated neutral pose.</summary>
    public enum FishingControlMode
    {
        Drag,
        Tilt
    }

    /// <summary>Owns persistent steering preferences and event-driven tilt calibration requests.</summary>
    public sealed class FishingControlSettings
    {
        /// <summary>Preference key for the player's requested steering mode.</summary>
        public const string ModeKey = "CasualFishing.ControlMode.v1";
        /// <summary>Preference key for the discrete tilt sensitivity.</summary>
        public const string SensitivityKey = "CasualFishing.TiltSensitivity.v1";

        private FishingControlMode _mode;
        private float _sensitivity;
        private string _calibrationStatus = "";

        /// <summary>Raised after a persisted mode or sensitivity change.</summary>
        public event Action Changed;
        /// <summary>Raised when a supported, active tilt mode asks its input adapter to capture a neutral pose.</summary>
        public event Action CalibrationRequested;
        /// <summary>Raised when the runtime-only calibration feedback changes.</summary>
        public event Action CalibrationStatusChanged;

        /// <summary>Creates settings from validated preferences and caches sensor capability for this session.</summary>
        public FishingControlSettings()
        {
            TiltAvailable = SystemInfo.supportsGyroscope || SystemInfo.supportsAccelerometer;
            int storedMode = PlayerPrefs.GetInt(ModeKey, (int)FishingControlMode.Drag);
            float storedSensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
            _mode = ValidMode(storedMode);
            _sensitivity = ValidSensitivity(storedSensitivity);

            bool repaired = false;
            if (PlayerPrefs.HasKey(ModeKey) && storedMode != (int)_mode)
            {
                PlayerPrefs.SetInt(ModeKey, (int)_mode);
                repaired = true;
            }
            if (PlayerPrefs.HasKey(SensitivityKey) && storedSensitivity != _sensitivity)
            {
                PlayerPrefs.SetFloat(SensitivityKey, _sensitivity);
                repaired = true;
            }
            if (repaired) PlayerPrefs.Save();
        }

        /// <summary>The player's preferred mode, retained even when tilt is unavailable on this device.</summary>
        public FishingControlMode Mode
        {
            get => _mode;
            set
            {
                value = ValidMode((int)value);
                if (_mode == value) return;
                _mode = value;
                PlayerPrefs.SetInt(ModeKey, (int)_mode);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Discrete tilt multiplier: 0.75, 1, or 1.5.</summary>
        public float Sensitivity
        {
            get => _sensitivity;
            set
            {
                value = ValidSensitivity(value);
                if (_sensitivity == value) return;
                _sensitivity = value;
                PlayerPrefs.SetFloat(SensitivityKey, _sensitivity);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Whether this session's device reported a gyroscope or accelerometer.</summary>
        public bool TiltAvailable { get; }
        /// <summary>Requested mode with an unsupported tilt choice safely falling back to drag.</summary>
        public FishingControlMode EffectiveMode => _mode == FishingControlMode.Tilt && !TiltAvailable
            ? FishingControlMode.Drag
            : _mode;
        /// <summary>Runtime calibration feedback; neutral-pose data itself is never persisted.</summary>
        public string CalibrationStatus => _calibrationStatus;

        /// <summary>Requests neutral-pose capture only while supported tilt steering is effective.</summary>
        public void RequestCalibration()
        {
            if (!TiltAvailable)
            {
                SetCalibrationStatus("此设备不支持倾斜操控，将继续使用拖动操控");
                return;
            }
            if (EffectiveMode != FishingControlMode.Tilt)
            {
                SetCalibrationStatus("请先切换到倾斜操控");
                return;
            }
            SetCalibrationStatus("正在校准，请保持设备姿势稳定");
            CalibrationRequested?.Invoke();
        }

        /// <summary>Updates runtime calibration feedback from the tilt input adapter.</summary>
        internal void SetCalibrationStatus(string status)
        {
            status = status ?? "";
            if (string.Equals(_calibrationStatus, status, StringComparison.Ordinal)) return;
            _calibrationStatus = status;
            CalibrationStatusChanged?.Invoke();
        }

        private static FishingControlMode ValidMode(int value) => value == (int)FishingControlMode.Tilt
            ? FishingControlMode.Tilt
            : FishingControlMode.Drag;

        private static float ValidSensitivity(float value)
        {
            if (value == .75f || value == 1f || value == 1.5f) return value;
            return 1f;
        }
    }
}
