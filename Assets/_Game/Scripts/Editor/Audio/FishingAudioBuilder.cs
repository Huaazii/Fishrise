using System;
using System.Linq;
using Fishing.Game;
using UnityEditor;
using UnityEngine;

namespace Fishing.Editor
{
    /// <summary>Imports the selected, documented audio bank and binds it through the existing shared UI asset.</summary>
    public static class FishingAudioBuilder
    {
        private const string Folder = "Assets/_Game/Audio/";
        private const string MenuMusic = Folder + "Music/magiksolo-aquarium-fish-132518.mp3";
        private const string DiveMusic = Folder + "Music/2 - Phat Phrog Studios - Underwater Ambience - 3 Minute.wav";
        /// <summary>Updates this bank in place, preserving its GUID and both scenes' shared asset references.</summary>
        [MenuItem("Fishing/Prepare Audio")]
        public static void Build()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Music has its own import policy; process only the selected SFX and ambience bank.
                if (!path.StartsWith(Folder + "SFX/", StringComparison.Ordinal) && !path.StartsWith(Folder + "Ambience/", StringComparison.Ordinal)) continue;
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool loop = path.EndsWith("IceWind.wav", StringComparison.Ordinal) || path.EndsWith("Underwater.wav", StringComparison.Ordinal);
                importer.forceToMono = true; importer.loadInBackground = false;
                var settings = importer.defaultSampleSettings;
                settings.preloadAudioData = true;
                settings.loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                settings.quality = .7f; settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                settings.sampleRateOverride = 22050;
                importer.defaultSampleSettings = settings; importer.SetOverrideSampleSettings("Android", settings); importer.SaveAndReimport();
            }
            const string pathBank = "Assets/_Game/Configs/GameAudioAssets.asset";
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioAssets>(pathBank);
            if (bank == null) { bank = ScriptableObject.CreateInstance<GameAudioAssets>(); AssetDatabase.CreateAsset(bank, pathBank); }
            bank.wind = Clip("IceWind"); bank.underwater = Clip("Underwater"); bank.reel = Clip("Reel");
            bank.sounds = new[] {
                Sound(GameFeedback.Cue.Dive,.40f,"Dive"), Sound(GameFeedback.Cue.Protection,.40f,"Protection"),
                Sound(GameFeedback.Cue.Capture,.30f,"CaptureA","CaptureB"), Sound(GameFeedback.Cue.Full,.28f,"Full"),
                Sound(GameFeedback.Cue.Surface,.44f,"Surface"), Sound(GameFeedback.Cue.Result,.30f,"Result"),
                Sound(GameFeedback.Cue.Sonar,.30f,"Sonar"), Sound(GameFeedback.Cue.Branches,.24f,"Branches"),
                Sound(GameFeedback.Cue.Treasure,.27f,"Treasure"), Sound(GameFeedback.Cue.Ui,.20f,"Ui"),
                Sound(GameFeedback.Cue.Escape,.25f,"Escape"), Sound(GameFeedback.Cue.FishWhoosh,.16f,"FishWhoosh"),
                Sound(GameFeedback.Cue.FishLand,.23f,"FishLandA","FishLandB"), Sound(GameFeedback.Cue.Reward,.13f,"Reward")
            };
            foreach (var sound in bank.sounds)
                if (sound.cue == GameFeedback.Cue.FishWhoosh || sound.cue == GameFeedback.Cue.FishLand || sound.cue == GameFeedback.Cue.Reward) sound.interval = .12f;
            var ui = AssetDatabase.LoadAssetAtPath<GameUiAssets>("Assets/_Game/Configs/GameUiAssets.asset");
            ui.audio = bank; EditorUtility.SetDirty(bank); EditorUtility.SetDirty(ui);
            AssetDatabase.SaveAssetIfDirty(bank); AssetDatabase.SaveAssetIfDirty(ui);
            BuildBgm();
        }
        /// <summary>Changes only the two selected music imports and BGM fields; preserves all SFX tuning and other pack files.</summary>
        [MenuItem("Fishing/Prepare BGM")]
        public static void BuildBgm()
        {
            var bank = AssetDatabase.LoadAssetAtPath<GameAudioAssets>("Assets/_Game/Configs/GameAudioAssets.asset");
            foreach (string path in new[] { MenuMusic, DiveMusic })
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = false; importer.loadInBackground = false;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming; settings.preloadAudioData = true;
                settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .75f;
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; settings.sampleRateOverride = 44100;
                importer.defaultSampleSettings = settings; importer.SetOverrideSampleSettings("Android", settings); importer.SaveAndReimport();
            }
            bank.music = AssetDatabase.LoadAssetAtPath<AudioClip>(MenuMusic);
            bank.underwaterMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(DiveMusic);
            bank.musicVolume = .24f; bank.underwaterMusicVolume = .18f;
            bank.musicTransitionSeconds = 1.6f; bank.musicLoopBlendSeconds = 2f; bank.menuMusicEndPadding = 1f;
            EditorUtility.SetDirty(bank); AssetDatabase.SaveAssetIfDirty(bank);
        }
        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + (name == "IceWind" || name == "Underwater" ? "Ambience/" : "SFX/") + name + ".wav");
        private static GameAudioAssets.Sound Sound(GameFeedback.Cue cue, float volume, params string[] names) =>
            new GameAudioAssets.Sound { cue = cue, volume = volume, clips = names.Select(Clip).ToArray() };
    }
}
