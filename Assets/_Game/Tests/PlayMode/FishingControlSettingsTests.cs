using Fishing.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fishing.Tests
{
    /// <summary>Exercises production preference loading, validation, persistence, and calibration events.</summary>
    [Category("MobileControls")]
    public sealed class FishingControlSettingsTests
    {
        private bool _hadMode, _hadSensitivity;
        private int _mode;
        private float _sensitivity;

        /// <summary>Remembers the exact original preferences, including missing keys.</summary>
        [SetUp]
        public void RememberPreferences()
        {
            _hadMode = PlayerPrefs.HasKey(FishingControlSettings.ModeKey);
            _hadSensitivity = PlayerPrefs.HasKey(FishingControlSettings.SensitivityKey);
            _mode = PlayerPrefs.GetInt(FishingControlSettings.ModeKey);
            _sensitivity = PlayerPrefs.GetFloat(FishingControlSettings.SensitivityKey);
        }

        /// <summary>Restores the exact original preferences even when an assertion fails.</summary>
        [TearDown]
        public void RestorePreferences()
        {
            if (_hadMode) PlayerPrefs.SetInt(FishingControlSettings.ModeKey, _mode);
            else PlayerPrefs.DeleteKey(FishingControlSettings.ModeKey);
            if (_hadSensitivity) PlayerPrefs.SetFloat(FishingControlSettings.SensitivityKey, _sensitivity);
            else PlayerPrefs.DeleteKey(FishingControlSettings.SensitivityKey);
            PlayerPrefs.Save();
        }


        /// <summary>Changed values survive a real preference reload while preferred tilt remains stored on any device.</summary>
        [Test]
        public void ModeAndSensitivityPersistAndReload()
        {
            PlayerPrefs.DeleteKey(FishingControlSettings.ModeKey);
            PlayerPrefs.DeleteKey(FishingControlSettings.SensitivityKey);
            var settings = new FishingControlSettings();

            settings.Mode = FishingControlMode.Tilt;
            settings.Sensitivity = 1.5f;
            var reloaded = new FishingControlSettings();

            Assert.That(reloaded.Mode, Is.EqualTo(FishingControlMode.Tilt));
            Assert.That(reloaded.Sensitivity, Is.EqualTo(1.5f));
            Assert.That(reloaded.EffectiveMode, Is.EqualTo(reloaded.TiltAvailable ? FishingControlMode.Tilt : FishingControlMode.Drag));
        }

        /// <summary>Invalid stored enum and multiplier values are repaired to supported defaults.</summary>
        [Test]
        public void InvalidStoredValuesAreValidatedAndRepaired()
        {
            PlayerPrefs.SetInt(FishingControlSettings.ModeKey, 99);
            PlayerPrefs.SetFloat(FishingControlSettings.SensitivityKey, 42f);
            PlayerPrefs.Save();

            var settings = new FishingControlSettings();

            Assert.That(settings.Mode, Is.EqualTo(FishingControlMode.Drag));
            Assert.That(settings.Sensitivity, Is.EqualTo(1f));
            Assert.That(PlayerPrefs.GetInt(FishingControlSettings.ModeKey), Is.EqualTo((int)FishingControlMode.Drag));
            Assert.That(PlayerPrefs.GetFloat(FishingControlSettings.SensitivityKey), Is.EqualTo(1f));
        }

    }
}
