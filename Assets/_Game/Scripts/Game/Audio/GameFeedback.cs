using System;
using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Bounded event sounds, depth-blended ambience, music preferences and Android light haptics.</summary>
    public sealed class GameFeedback : IDisposable
    {
        /// <summary>Sound identifiers correspond to committed game events, never collision candidates.</summary>
        public enum Cue { Dive, Protection, Capture, Full, Surface, Result, Sonar, Branches, Treasure, Ui, Escape, FishWhoosh, FishLand, Reward }
        /// <summary>Preference key shared by menu and pause settings.</summary>
        public const string SoundKey = "CasualFishing.Sound.v1";
        /// <summary>Independent preference for background music.</summary>
        public const string MusicKey = "CasualFishing.Music.v1";
        /// <summary>Preference key for capture, protection and accelerated-ascent haptics.</summary>
        public const string HapticsKey = "CasualFishing.Haptics.v1";
        private readonly AudioSource[] _voices = new AudioSource[9];
        private const int CueCount = (int)Cue.Reward + 1;
        private readonly AudioClip[][] _clips = new AudioClip[CueCount][];
        private readonly AudioClip[] _generated = new AudioClip[CueCount];
        private readonly float[] _gains = new float[CueCount], _intervals = new float[CueCount], _last = new float[CueCount];
        private readonly int[] _events = new int[CueCount], _played = new int[CueCount];
        private readonly GameAudioAssets _assets;
        private readonly AudioSource _wind, _underwater, _reel, _ui;
        private readonly FishingMusic _music;
        internal FishingMusic Music => _music;
        private float _time, _duck;
        private int _voice;
        private bool _paused;
        /// <summary>Whether event sounds are enabled.</summary>
        public bool SoundEnabled { get; private set; } = PlayerPrefs.GetInt(SoundKey, 1) != 0;
        /// <summary>Whether background music is enabled independently of sound effects.</summary>
        public bool MusicEnabled { get; private set; } = PlayerPrefs.GetInt(MusicKey, 1) != 0;
        /// <summary>Whether device haptics are enabled.</summary>
        public bool HapticsEnabled { get; private set; } = PlayerPrefs.GetInt(HapticsKey, 1) != 0;
        /// <summary>Haptic commands issued in the current run, including Editor instrumentation.</summary>
        public int HapticCommands { get; private set; }
        /// <summary>Creates fixed voice pools and shared-asset loops; missing cues retain an original procedural fallback.</summary>
        public GameFeedback(Transform parent, GameAudioAssets assets = null)
        {
            _assets = assets;
            var root = new GameObject("Event audio"); root.transform.SetParent(parent, false);
            for (int i = 0; i < _voices.Length; i++) _voices[i] = Voice(root.transform, "Event voice " + i);
            _ui = Voice(root.transform, "UI audio");
            _music = FishingMusic.Acquire();
            _wind = Loop(root.transform, "Ice wind audio", assets != null ? assets.wind : null);
            _underwater = Loop(root.transform, "Underwater audio", assets != null ? assets.underwater : null);
            _reel = Loop(root.transform, "Reel audio", assets != null ? assets.reel : null);
            _wind.priority = _underwater.priority = 180;
            if (assets != null && assets.sounds != null)
                foreach (var sound in assets.sounds)
                {
                    int index = (int)sound.cue;
                    if (index < 0 || index >= CueCount || sound.clips == null || sound.clips.Length == 0 || Array.Exists(sound.clips, clip => clip == null)) continue;
                    _clips[index] = sound.clips; _gains[index] = sound.volume; _intervals[index] = sound.interval;
                }
            float[] frequencies = { 380, 170, 660, 880, 520, 1040, 430, 560, 1175 };
            for (int i = 0; i < _clips.Length; i++)
            {
                _last[i] = float.NegativeInfinity;
                if (_clips[i] != null) continue;
                float length = i == (int)Cue.Result ? .32f : .13f;
                const int rate = 22050; var samples = new float[(int)(rate * length)];
                for (int j = 0; j < samples.Length; j++)
                {
                    float t = (float)j / rate, p = t / length;
                    float pitch = frequencies[i % frequencies.Length] * (1 + (i == (int)Cue.Protection ? -.45f : .3f) * p);
                    samples[j] = Mathf.Sin(2 * Mathf.PI * pitch * t) * Mathf.Sin(p * Mathf.PI) * (1 - p) * .65f;
                }
                var clip = AudioClip.Create("Fishing " + (Cue)i, samples.Length, 1, rate, false); clip.SetData(samples, 0);
                _generated[i] = clip; _clips[i] = new[] { clip }; _gains[i] = .24f;
            }
            ApplyMute();
        }
        private static AudioSource Voice(Transform parent, string name)
        {
            var child = new GameObject(name); child.transform.SetParent(parent, false);
            var voice = child.AddComponent<AudioSource>(); voice.playOnAwake = false;
            voice.spatialBlend = 0; voice.volume = 1; voice.priority = 80; return voice;
        }
        private static AudioSource Loop(Transform parent, string name, AudioClip clip)
        {
            var voice = Voice(parent, name); voice.clip = clip; voice.loop = true; voice.volume = 0;
            if (clip != null) voice.Play(); return voice;
        }
        /// <summary>Crossfades surface and underwater beds using presentation time, without consuming gameplay randomness.</summary>
        public void Tick(float dt, RunModel run)
        {
            if (_paused) return;
            _time += dt; _duck = Mathf.Max(0, _duck - dt);
            if (_assets == null) return;
            bool submerged = run != null && (run.State == RunState.Diving || run.State == RunState.BottomHold || run.State == RunState.Ascending);
            float depth = submerged ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(run.Depth / 6)) : 0;
            float focus = _duck > 0 ? .45f : 1;
            _music.Tick(dt, _assets, !submerged, focus);
            Fade(_wind, _assets.windVolume * (1 - depth), dt);
            Fade(_underwater, _assets.underwaterVolume * depth, dt);
            bool ascending = run != null && run.State == RunState.Ascending;
            float tension = ascending && run.TotalCaptureCount > 0 ? Mathf.Clamp01(run.Reeling.Speed / 7) : .2f;
            Fade(_reel, ascending ? _assets.reelVolume * Mathf.Lerp(.4f, 1, tension) : 0, dt);
            _reel.pitch = Mathf.Lerp(.8f, 1.35f, tension);
        }
        private static void Fade(AudioSource source, float target, float dt)
        { source.volume = Mathf.MoveTowards(source.volume, target, dt * .3f); }
        /// <summary>Committed event count, independent of the player's sound setting.</summary>
        public int EventCount(Cue cue) => _events[(int)cue];
        /// <summary>Actual audio play commands after mute/pause filtering.</summary>
        public int PlayedCount(Cue cue) => _played[(int)cue];
        /// <summary>Plays one cue and optionally requests a short device vibration.</summary>
        public void Play(Cue cue)
        {
            _events[(int)cue]++;
            if (_paused && cue != Cue.Ui) return;
            int index = (int)cue;
            if (SoundEnabled && (cue == Cue.Ui || _time - _last[index] >= _intervals[index]))
            {
                var voice = cue == Cue.Ui ? _ui : _voices[_voice++ % _voices.Length];
                var variants = _clips[index];
                // Reusing a source replaces its sound; PlayOneShot would stack hidden voices during dense catches.
                voice.Stop(); voice.clip = variants[_played[index] % variants.Length]; voice.volume = _gains[index]; voice.Play();
                _played[index]++; _last[index] = _time;
                if (cue == Cue.Protection || cue == Cue.Full || cue == Cue.Surface || cue == Cue.Result) _duck = .45f;
            }
            if (cue == Cue.Capture || cue == Cue.Protection) Vibrate(20, 60);
        }
        /// <summary>Requests a single stronger pulse when a full main hook actually starts accelerated ascent.</summary>
        public void PulseFastAscent(int milliseconds, int amplitude) { Vibrate(milliseconds, amplitude); }
        private void Vibrate(int milliseconds, int amplitude)
        {
            if (_paused || !HapticsEnabled) return;
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
                    using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, amplitude))
                        vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", (long)milliseconds);
            }
#endif
        }
        /// <summary>Updates and persists the sound preference; muting stops any currently ringing cue.</summary>
        public void ToggleSound()
        {
            SoundEnabled = !SoundEnabled; PlayerPrefs.SetInt(SoundKey, SoundEnabled ? 1 : 0); PlayerPrefs.Save();
            if (!SoundEnabled) foreach (var voice in _voices) voice.Stop();
            if (!SoundEnabled) _ui.Stop();
            ApplyMute();
        }
        /// <summary>Persists independent music muting without changing gameplay sound or haptic settings.</summary>
        public void ToggleMusic()
        { MusicEnabled = !MusicEnabled; PlayerPrefs.SetInt(MusicKey, MusicEnabled ? 1 : 0); PlayerPrefs.Save(); ApplyMute(); }
        private void ApplyMute()
        { _music.SetMuted(!MusicEnabled); _wind.mute = _underwater.mute = _reel.mute = _ui.mute = !SoundEnabled; }
        /// <summary>Updates and persists the haptic preference.</summary>
        public void ToggleHaptics()
        { HapticsEnabled = !HapticsEnabled; PlayerPrefs.SetInt(HapticsKey, HapticsEnabled ? 1 : 0); PlayerPrefs.Save(); }
        /// <summary>Pauses/resumes in-flight cues with the gameplay clock.</summary>
        public void Pause(bool paused)
        {
            _paused = paused;
            foreach (var voice in _voices) { if (paused) voice.Pause(); else voice.UnPause(); }
            _music.Pause(paused);
            foreach (var loop in new[] { _wind, _underwater, _reel }) { if (paused) loop.Pause(); else loop.UnPause(); }
            if (paused) _ui.Stop();
        }
        /// <summary>Resets event instrumentation and stops voices for a fresh run.</summary>
        public void ResetRun()
        {
            Array.Clear(_events, 0, _events.Length); Array.Clear(_played, 0, _played.Length);
            HapticCommands = 0; _voice = 0; _time = _duck = 0; Pause(false); foreach (var voice in _voices) voice.Stop();
            _ui.Stop(); _reel.volume = 0;
            for (int i = 0; i < _last.Length; i++) _last[i] = float.NegativeInfinity;
        }
        /// <summary>Releases only fallback clips; imported shared assets remain owned by Unity.</summary>
        public void Dispose() { foreach (var clip in _generated) if (clip != null) UnityEngine.Object.Destroy(clip); }
    }
}
