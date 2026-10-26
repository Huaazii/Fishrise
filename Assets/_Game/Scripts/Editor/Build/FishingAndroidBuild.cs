using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fishing.Editor
{
    /// <summary>Reproducible Android builds, with a disk result readable while the Editor is busy.</summary>
    public static class FishingAndroidBuild
    {
        private const string Version = "0.3.1";
        private const int VersionCode = 4;
        private const string KnownAndroidInputWarning = "Active Input Handling is set to Both";
        [Serializable]
        private sealed class Result
        {
            public string state, utc, unity, apk, error;
            public ulong bytes, apkBytes;
            public int errors, warnings, rawWarnings;
            public string suppressed;
            public double seconds;
        }
        /// <summary>Builds the shippable APK without development flags.</summary>
        [MenuItem("Fishing/Build Android APK")]
        public static void BuildRelease() { Build(false); }
        /// <summary>Builds a development APK for profiling and verbose logs.</summary>
        [MenuItem("Fishing/Build Android APK (Development)")]
        public static void BuildDevelopment() { Build(true); }
        /// <summary>Builds Menu/Main with the installed Unity Android toolchain.</summary>
        private static void Build(bool development)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Editor must be stopped, compiled and idle before building.");
            string mode = development ? "development" : "release";
            string output = $"Builds/Android/Fishrise-{Version}-arm64{(development ? "-dev" : "")}.apk";
            string reportPath = $"Docs/Verification/android-build-{mode}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            Directory.CreateDirectory("Docs/Verification"); Directory.CreateDirectory("Builds/Android");
            var result = new Result { state = "building", utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, apk = output };
            Save(reportPath, result);
            try
            {
                var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
                // 0 = Old, 1 = New, 2 = Both (Input System's EditorPlayerSettingHelpers).
                // The unity-cli-bridge test package pulls in com.unity.inputsystem,
                // so "Input Manager (Old)" only (0) makes Unity warn on every domain reload.
                int inputHandler = settings.FindProperty("activeInputHandler").intValue;
                if (inputHandler != 0 && inputHandler != 2)
                    throw new InvalidOperationException(
                        "Set Project Settings > Player > Other Settings > Active Input Handling to 'Both' or 'Input Manager (Old)', then restart the Editor before building Android.");
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.huaziii.casualfishing");
                PlayerSettings.productName = "Fishrise"; PlayerSettings.bundleVersion = Version;
                PlayerSettings.Android.bundleVersionCode = VersionCode;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)23;
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                EditorUserBuildSettings.buildAppBundle = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/Menu.unity", "Assets/_Game/Scenes/Main.unity" },
                    target = BuildTarget.Android, locationPathName = output,
                    options = development ? BuildOptions.Development : BuildOptions.None
                });
                result.state = report.summary.result.ToString(); result.bytes = report.summary.totalSize;
                result.apkBytes = File.Exists(output) ? (ulong)new FileInfo(output).Length : 0;
                result.errors = (int)report.summary.totalErrors;
                result.rawWarnings = (int)report.summary.totalWarnings;
                result.warnings = CountUnreviewedWarnings(report, out result.suppressed);
                result.seconds = report.summary.totalTime.TotalSeconds;
                if (report.summary.result != BuildResult.Succeeded) result.error = "Read Editor.log for build errors.";
            }
            catch (Exception exception)
            { result.state = "Exception"; result.error = exception.ToString(); Debug.LogException(exception); }
            Save(reportPath, result);
            Debug.Log($"Android {mode} build: {result.state}, {result.errors} errors, {result.warnings} warnings, {result.bytes} bytes -> {output}");
        }
        /// <summary>
        /// Counts warnings that still need review. Unity always reports "Active Input Handling is
        /// set to Both" on Android; nothing in this project calls the Input System API, so that
        /// single platform note is recorded verbatim in <c>suppressed</c> instead of inflating the count.
        /// </summary>
        private static int CountUnreviewedWarnings(BuildReport report, out string suppressed)
        {
            int count = 0;
            var known = new List<string>();
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                {
                    if (message.type != LogType.Warning) continue;
                    if (message.content.IndexOf(KnownAndroidInputWarning, StringComparison.Ordinal) >= 0)
                        known.Add(message.content);
                    else count++;
                }
            suppressed = known.Count == 0 ? string.Empty : string.Join(" | ", known.ToArray());
            return count;
        }
        private static void Save(string path, Result result)
        { File.WriteAllText(path, JsonUtility.ToJson(result, true)); }
    }
}
