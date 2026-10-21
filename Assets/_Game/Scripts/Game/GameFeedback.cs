using System;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Event-owned synthesized cues, persisted mute settings and Android light haptics.</summary>
    public sealed class GameFeedback : IDisposable
    {
        /// <summary>Sound identifiers correspond to committed game events, never collision candidates.</summary>
        public enum Cue { Dive, Protection, Capture, Full, Surface, Result, Sonar, Branches }
        /// <summary>Preference key shared by menu and pause settings.</summary>
        public const string SoundKey = "CasualFishing.Sound.v1";
        /// <summary>Preference key for capture/protection haptics.</summary>
        public const string HapticsKey = "CasualFishing.Haptics.v1";
        private readonly AudioSource[] _voices = new AudioSource[8];
        private readonly AudioClip[] _clips = new AudioClip[8];
        private readonly int[] _events = new int[8], _played = new int[8];
        private int _voice;
        private bool _paused;
        /// <summary>Whether event sounds are enabled.</summary>
        public bool SoundEnabled { get; private set; } = PlayerPrefs.GetInt(SoundKey, 1) != 0;
        /// <summary>Whether device haptics are enabled.</summary>
        public bool HapticsEnabled { get; private set; } = PlayerPrefs.GetInt(HapticsKey, 1) != 0;
        /// <summary>Haptic commands issued in the current run, including Editor instrumentation.</summary>
        public int HapticCommands { get; private set; }
        /// <summary>Creates bounded voices and original procedural cues without external audio assets.</summary>
        public GameFeedback(Transform parent)
        {
            var root = new GameObject("Event audio"); root.transform.SetParent(parent, false);
            float[] frequencies = { 380, 170, 660, 880, 520, 1040, 430, 560 };
            for (int i = 0; i < _clips.Length; i++)
            {
                _voices[i] = root.AddComponent<AudioSource>(); _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0; _voices[i].volume = .24f;
                float length = i == (int)Cue.Result ? .32f : .13f;
                const int rate = 22050; var samples = new float[(int)(rate * length)];
                for (int j = 0; j < samples.Length; j++)
                {
                    float t = (float)j / rate, p = t / length;
                    float pitch = frequencies[i] * (1 + (i == (int)Cue.Protection ? -.45f : .3f) * p);
                    samples[j] = Mathf.Sin(2 * Mathf.PI * pitch * t) * Mathf.Sin(p * Mathf.PI) * (1 - p) * .65f;
                }
                _clips[i] = AudioClip.Create("Fishing " + (Cue)i, samples.Length, 1, rate, false); _clips[i].SetData(samples, 0);
            }
        }
        /// <summary>Committed event count, independent of the player's sound setting.</summary>
        public int EventCount(Cue cue) => _events[(int)cue];
        /// <summary>Actual audio play commands after mute/pause filtering.</summary>
        public int PlayedCount(Cue cue) => _played[(int)cue];
        /// <summary>Plays one cue and optionally requests a short device vibration.</summary>
        public void Play(Cue cue)
        {
            _events[(int)cue]++;
            if (_paused) return;
            if (SoundEnabled)
            { _voices[_voice++ % _voices.Length].PlayOneShot(_clips[(int)cue]); _played[(int)cue]++; }
            if (!HapticsEnabled || (cue != Cue.Capture && cue != Cue.Protection)) return;
            HapticCommands++;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) return;
                if (version.GetStatic<int>("SDK_INT") >= 26)
                {
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", 20L, 60))
                        vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", 20L);
            }
#endif
        }
        /// <summary>Updates and persists the sound preference; muting stops any currently ringing cue.</summary>
        public void ToggleSound()
        {
            SoundEnabled = !SoundEnabled; PlayerPrefs.SetInt(SoundKey, SoundEnabled ? 1 : 0); PlayerPrefs.Save();
            if (!SoundEnabled) foreach (var voice in _voices) voice.Stop();
        }
        /// <summary>Updates and persists the haptic preference.</summary>
        public void ToggleHaptics()
        { HapticsEnabled = !HapticsEnabled; PlayerPrefs.SetInt(HapticsKey, HapticsEnabled ? 1 : 0); PlayerPrefs.Save(); }
        /// <summary>Pauses/resumes in-flight cues with the gameplay clock.</summary>
        public void Pause(bool paused)
        { _paused = paused; foreach (var voice in _voices) { if (paused) voice.Pause(); else voice.UnPause(); } }
        /// <summary>Resets event instrumentation and stops voices for a fresh run.</summary>
        public void ResetRun()
        {
            Array.Clear(_events, 0, _events.Length); Array.Clear(_played, 0, _played.Length);
            HapticCommands = 0; _voice = 0; _paused = false; foreach (var voice in _voices) voice.Stop();
        }
        /// <summary>Releases the runtime-generated clip resources with the scene owner.</summary>
        public void Dispose() { foreach (var clip in _clips) UnityEngine.Object.Destroy(clip); }
    }
}
