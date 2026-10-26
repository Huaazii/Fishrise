using System.Runtime.CompilerServices;
using UnityEngine;

// Acceptance tests inspect stream handover without exposing the music implementation as a gameplay API.
[assembly: InternalsVisibleTo("Fishing.Game.Tests")]

namespace Fishing.Game
{
    /// <summary>Two persistent music voices crossfade scene themes and non-looping track endings.</summary>
    internal sealed class FishingMusic
    {
        private static FishingMusic _shared;
        private readonly GameObject _root;
        private readonly AudioSource[] _voices = new AudioSource[2];
        private readonly AudioLowPassFilter[] _filters = new AudioLowPassFilter[2];
        private readonly float[] _levels = new float[2];
        private int _active = -1, _incoming;
        private float _blendTime, _blendDuration, _outgoingLevel, _age, _focus = 1;
        private bool _blending, _paused;
        internal AudioClip ActiveClip => _active >= 0 ? _voices[_active].clip : null;
        internal AudioSource ActiveSource => _active >= 0 ? _voices[_active] : null;
        internal AudioClip TargetClip => _blending ? _voices[_incoming].clip : ActiveClip;
        internal bool IsBlending => _blending;
        internal int StartCommands { get; private set; }
        internal AudioSource[] Voices => _voices;

        private FishingMusic()
        {
            _root = new GameObject("Fishrise music"); Object.DontDestroyOnLoad(_root);
            for (int i = 0; i < _voices.Length; i++)
            {
                var child = new GameObject(i == 0 ? "Music audio" : "Music blend audio"); child.transform.SetParent(_root.transform, false);
                var voice = child.AddComponent<AudioSource>(); voice.playOnAwake = false;
                voice.loop = false; voice.spatialBlend = 0; voice.priority = 160; voice.volume = 0;
                _voices[i] = voice; _filters[i] = child.AddComponent<AudioLowPassFilter>();
                _filters[i].cutoffFrequency = 16000;
            }
        }
        internal static FishingMusic Acquire()
        {
            // Unity destroys persistent objects on leaving Play; do not reuse their static wrappers next session.
            if (_shared == null || _shared._root == null) _shared = new FishingMusic();
            return _shared;
        }
        internal void SetMuted(bool muted) { foreach (var voice in _voices) voice.mute = muted; }
        internal void Pause(bool paused)
        {
            _paused = paused;
            foreach (var voice in _voices) { if (paused) voice.Pause(); else voice.UnPause(); }
        }
        internal void Tick(float dt, GameAudioAssets assets, bool surface, float focus)
        {
            if (_paused || assets == null) return;
            var desired = surface || assets.underwaterMusic == null ? assets.music : assets.underwaterMusic;
            if (desired == null) return;
            float gain = surface || assets.underwaterMusic == null ? assets.musicVolume : assets.underwaterMusicVolume;
            if (_active < 0 && !_blending) Begin(desired, Mathf.Min(.7f, assets.musicTransitionSeconds));
            _focus = Mathf.MoveTowards(_focus, focus, dt * 3);
            if (!_blending)
            {
                var current = _voices[_active]; _age += dt;
                // Theme requests during a blend wait for its completion, so rapid navigation never cuts a partly audible voice.
                if (current.clip != desired) Begin(desired, assets.musicTransitionSeconds);
                else
                {
                    float endPadding = current.clip == assets.music ? assets.menuMusicEndPadding : .03f;
                    float remaining = current.clip.length - endPadding - current.time;
                    if (remaining <= assets.musicLoopBlendSeconds || (!current.isPlaying && _age > .3f))
                        Begin(desired, assets.musicLoopBlendSeconds);
                }
            }
            if (_blending)
            {
                _blendTime += dt; float progress = Mathf.Clamp01(_blendTime / _blendDuration);
                // Incoming gain belongs to its actual track, even if a newer theme request is waiting.
                float incomingGain = _voices[_incoming].clip == assets.music ? assets.musicVolume : assets.underwaterMusicVolume;
                _levels[_incoming] = Mathf.Sin(progress * Mathf.PI * .5f) * incomingGain;
                if (_active >= 0) _levels[_active] = Mathf.Cos(progress * Mathf.PI * .5f) * _outgoingLevel;
                if (progress >= 1)
                {
                    if (_active >= 0) { _voices[_active].Stop(); _levels[_active] = 0; }
                    _active = _incoming; _blending = false; _age = 0;
                }
            }
            else _levels[_active] = Mathf.MoveTowards(_levels[_active], gain, dt * .3f);
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i].volume = _levels[i] * _focus;
                _filters[i].cutoffFrequency = _voices[i].clip == assets.music ? 16000 : 11000;
            }
        }
        private void Begin(AudioClip clip, float duration)
        {
            _incoming = _active == 0 ? 1 : 0;
            var voice = _voices[_incoming]; voice.Stop(); voice.clip = clip; voice.volume = 0;
            voice.time = 0; voice.Play(); StartCommands++;
            _levels[_incoming] = 0; _outgoingLevel = _active >= 0 ? _levels[_active] : 0;
            _blendTime = 0; _blendDuration = Mathf.Max(.1f, duration); _blending = true;
        }
    }
}
