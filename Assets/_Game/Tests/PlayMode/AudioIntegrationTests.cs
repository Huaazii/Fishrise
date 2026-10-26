using System.Collections;
using System.Linq;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Real imported-bank bindings, independent mute controls and animation-owned sound timing.</summary>
    public sealed class AudioIntegrationTests : AbilityTestScene
    {
        /// <summary>Both scenes use a complete bank and the fixed pool blends actual depth without allocating voices.</summary>
        [UnityTest]
        public IEnumerator SharedBankAndDepthBlendUseBoundedSources()
        {
            var bank = UnityEditor.AssetDatabase.LoadAssetAtPath<GameAudioAssets>("Assets/_Game/Configs/GameAudioAssets.asset");
            Assert.That(bank, Is.Not.Null);
            Assert.That(bank.sounds.Length, Is.EqualTo(14));
            Assert.That(bank.sounds.All(s => s.clips.Length > 0 && s.clips.All(c => c != null)), Is.True);
            Assert.That(bank.music.channels, Is.EqualTo(2));
            Assert.That(bank.music.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(bank.underwaterMusic.channels, Is.EqualTo(2));
            Assert.That(bank.underwaterMusic.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(bank.wind.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(bank.underwater.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
            var voices = Game.GetComponentsInChildren<AudioSource>(); Assert.That(voices.Length, Is.EqualTo(13));
            Assert.That(Object.FindObjectsOfType<AudioSource>().Length, Is.EqualTo(15));
            Assert.That(Object.FindObjectsOfType<AudioListener>().Count(l => l.enabled), Is.EqualTo(1));
            Game.Feedback.Tick(1, null);
            var wind = GameObject.Find("Ice wind audio").GetComponent<AudioSource>();
            var water = GameObject.Find("Underwater audio").GetComponent<AudioSource>();
            Assert.That(wind.volume, Is.GreaterThan(0)); Assert.That(water.volume, Is.Zero);
            Until(RunState.BottomHold); Game.Feedback.Tick(1, Game.Run);
            Assert.That(wind.volume, Is.Zero); Assert.That(water.volume, Is.GreaterThan(0));
            Game.BeginRun(); Assert.That(Game.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(13));
            Assert.That(Object.FindObjectsOfType<AudioSource>().Length, Is.EqualTo(15));
            yield return null;
        }

        /// <summary>Music and effects mute independently; paused audio retains position and bursts are bounded.</summary>
        [UnityTest]
        public IEnumerator IndependentMutesPauseAndBurstGatePreservePreferences()
        {
            bool sound = Game.Feedback.SoundEnabled, music = Game.Feedback.MusicEnabled;
            bool hadSound = PlayerPrefs.HasKey(GameFeedback.SoundKey), hadMusic = PlayerPrefs.HasKey(GameFeedback.MusicKey);
            try
            {
                if (!Game.Feedback.SoundEnabled) Game.Feedback.ToggleSound();
                if (!Game.Feedback.MusicEnabled) Game.Feedback.ToggleMusic();
                SettleMenuMusic(); var musicVoice = Game.Feedback.Music.ActiveSource;
                Game.Feedback.ToggleMusic(); Assert.That(musicVoice.mute, Is.True); Assert.That(Game.Feedback.SoundEnabled, Is.True);
                Assert.That(Game.Feedback.Music.Voices.All(v => v.mute), Is.True);
                Game.Feedback.ToggleMusic(); Game.Feedback.ToggleSound();
                Assert.That(musicVoice.mute, Is.False); Assert.That(GameObject.Find("Underwater audio").GetComponent<AudioSource>().mute, Is.True);
                Game.Feedback.Play(GameFeedback.Cue.Sonar); Assert.That(Game.Feedback.PlayedCount(GameFeedback.Cue.Sonar), Is.Zero);
                Game.Feedback.ToggleSound(); Game.Feedback.ResetRun();
                for (int i = 0; i < 20; i++) Game.Feedback.Play(GameFeedback.Cue.Sonar);
                Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar), Is.EqualTo(20));
                Assert.That(Game.Feedback.PlayedCount(GameFeedback.Cue.Sonar), Is.EqualTo(1));
                Game.Feedback.Tick(.15f, Game.Run); Game.Feedback.Play(GameFeedback.Cue.Sonar);
                Assert.That(Game.Feedback.PlayedCount(GameFeedback.Cue.Sonar), Is.EqualTo(2));
                Game.Feedback.Pause(true);
                // Streaming pause reaches the DSP thread on its next buffer; measure only after that command settles.
                yield return new WaitForSecondsRealtime(.1f); int position = musicVoice.timeSamples;
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(musicVoice.timeSamples, Is.EqualTo(position));
                Game.Feedback.Play(GameFeedback.Cue.Full); Assert.That(Game.Feedback.PlayedCount(GameFeedback.Cue.Full), Is.Zero);
                Game.Feedback.Play(GameFeedback.Cue.Ui); Assert.That(Game.Feedback.PlayedCount(GameFeedback.Cue.Ui), Is.EqualTo(1));
                Game.Feedback.Play(GameFeedback.Cue.Ui); Assert.That(Game.Feedback.PlayedCount(GameFeedback.Cue.Ui), Is.EqualTo(2));
                Game.Feedback.Pause(false); yield return null; yield return null;
                Assert.That(musicVoice.isPlaying, Is.True);
            }
            finally
            {
                Game.Feedback.Pause(false);
                if (Game.Feedback.SoundEnabled != sound) Game.Feedback.ToggleSound();
                if (Game.Feedback.MusicEnabled != music) Game.Feedback.ToggleMusic();
                if (!hadSound) PlayerPrefs.DeleteKey(GameFeedback.SoundKey);
                if (!hadMusic) PlayerPrefs.DeleteKey(GameFeedback.MusicKey);
                PlayerPrefs.Save();
            }
        }

        private void SettleMenuMusic()
        { for (int i = 0; i < 200; i++) Game.Feedback.Tick(.02f, null); }

        /// <summary>The rhythmic menu theme hands over to the selected underwater track without restarting every tick.</summary>
        [UnityTest]
        public IEnumerator MenuAndUnderwaterThemesCrossfadeAndLoopsOverlap()
        {
            var bank = UnityEditor.AssetDatabase.LoadAssetAtPath<GameAudioAssets>("Assets/_Game/Configs/GameAudioAssets.asset");
            Assert.That(bank.music.name, Is.EqualTo("magiksolo-aquarium-fish-132518"));
            Assert.That(bank.underwaterMusic.name, Does.StartWith("2 - Phat Phrog"));
            SettleMenuMusic(); var music = Game.Feedback.Music;
            Assert.That(music.ActiveClip, Is.SameAs(bank.music));
            int starts = music.StartCommands;
            for (int i = 0; i < 50; i++) Game.Feedback.Tick(.02f, null);
            Assert.That(music.StartCommands, Is.EqualTo(starts));
            Until(RunState.BottomHold);
            for (int i = 0; i < 200; i++) Game.Feedback.Tick(.02f, Game.Run);
            Assert.That(music.ActiveClip, Is.SameAs(bank.underwaterMusic));
            Assert.That(music.IsBlending, Is.False);
            music.ActiveSource.time = music.ActiveClip.length - 1.8f;
            Game.Feedback.Tick(.02f, Game.Run);
            Assert.That(music.IsBlending, Is.True); Assert.That(music.TargetClip, Is.SameAs(bank.underwaterMusic));
            Game.Feedback.Tick(.8f, Game.Run);
            Assert.That(music.Voices.All(v => v.volume > 0 && v.isPlaying), Is.True);
            Game.Feedback.Tick(2, Game.Run);
            Assert.That(music.IsBlending, Is.False); Assert.That(music.ActiveSource.time, Is.LessThan(5));
            yield return null;
        }

        /// <summary>Changing Menu/Main scenes keeps the same music voices, position and start count during countdown.</summary>
        [UnityTest]
        public IEnumerator MusicSurvivesMenuMainSceneChangesWithoutRestart()
        {
            SettleMenuMusic(); var music = Game.Feedback.Music; var voice = music.ActiveSource;
            voice.time = 25; int starts = music.StartCommands, id = voice.GetInstanceID();
            foreach (string scene in new[] { "Menu", "Main" })
            {
                SceneManager.LoadScene(scene); yield return null; yield return null;
                Game = Object.FindObjectOfType<GameBootstrap>(); Game.enabled = false;
                Assert.That(Game.Feedback.Music, Is.SameAs(music));
                Assert.That(music.ActiveSource.GetInstanceID(), Is.EqualTo(id));
                Assert.That(music.ActiveSource.time, Is.InRange(20, 35));
                Assert.That(music.StartCommands, Is.EqualTo(starts));
                Assert.That(Object.FindObjectsOfType<AudioSource>().Length, Is.EqualTo(15));
            }
        }

        /// <summary>Pause freezes an in-progress theme fade and both stream positions; reset does not allocate music voices.</summary>
        [UnityTest]
        public IEnumerator PauseFreezesBothMusicStreamsDuringTransition()
        {
            SettleMenuMusic(); var music = Game.Feedback.Music;
            Until(RunState.BottomHold); SettleMenuMusic();
            Game.Feedback.Tick(.3f, Game.Run); Assert.That(music.IsBlending, Is.True);
            Game.Feedback.Pause(true);
            try
            {
                yield return new WaitForSecondsRealtime(.1f);
                var samples = music.Voices.Select(v => v.timeSamples).ToArray();
                var volumes = music.Voices.Select(v => v.volume).ToArray();
                Game.Feedback.Tick(2, Game.Run); yield return new WaitForSecondsRealtime(.1f);
                CollectionAssert.AreEqual(samples, music.Voices.Select(v => v.timeSamples));
                CollectionAssert.AreEqual(volumes, music.Voices.Select(v => v.volume));
                Assert.That(music.IsBlending, Is.True);
            }
            finally { Game.Feedback.Pause(false); }
            Game.Feedback.Tick(2, Game.Run); Assert.That(music.IsBlending, Is.False);
            Game.BeginRun(); Assert.That(Object.FindObjectsOfType<AudioSource>().Length, Is.EqualTo(15));
        }

        /// <summary>Each fish sounds on release, barrel arrival and reward rather than all at the start of surfacing.</summary>
        [UnityTest]
        public IEnumerator SurfaceAudioFollowsActualFishAnimationAndDoesNotReplay()
        {
            Until(RunState.Ascending);
            AtHead(Blue, Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset)); Steps(2);
            Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(1));
            Until(RunState.Surfacing);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.FishWhoosh), Is.Zero);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.FishLand), Is.Zero);
            Until(RunState.Result);
            foreach (var cue in new[] { GameFeedback.Cue.FishWhoosh, GameFeedback.Cue.FishLand, GameFeedback.Cue.Reward })
                Assert.That(Game.Feedback.EventCount(cue), Is.EqualTo(1), cue.ToString());
            Steps(10);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.FishLand), Is.EqualTo(1));
            Game.BeginRun(); Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.FishLand), Is.Zero);
            yield return null;
        }
    }
}
