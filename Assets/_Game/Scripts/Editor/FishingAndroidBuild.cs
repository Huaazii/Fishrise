using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Fishing.Editor
{
    /// <summary>Reproducible current Android build, with a disk result readable while the Editor is busy.</summary>
    public static class FishingAndroidBuild
    {
        private const string ReportPath = "Docs/Verification/android-build-20261021.json";
        private const string OutputPath = "Builds/Android/CasualFishing-0.2.0-arm64.apk";
        [Serializable]
        private sealed class Result
        {
            public string state, utc, unity, apk, error;
            public ulong bytes;
            public int errors, warnings;
            public double seconds;
        }
        /// <summary>Builds Menu/Main with the installed Unity Android toolchain.</summary>
        [MenuItem("Fishing/Build Android APK")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Editor must be stopped, compiled and idle before building.");
            Directory.CreateDirectory("Docs/Verification"); Directory.CreateDirectory("Builds/Android");
            var result = new Result { state = "building", utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, apk = OutputPath };
            Save(result);
            try
            {
                var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
                if (settings.FindProperty("activeInputHandler").intValue != 0)
                    throw new InvalidOperationException("Use Legacy Input Manager (0) and restart the Editor before building Android.");
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.huaziii.casualfishing");
                PlayerSettings.productName = "Casual Fishing"; PlayerSettings.bundleVersion = "0.2.0";
                PlayerSettings.Android.bundleVersionCode = 2;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)23;
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                EditorUserBuildSettings.buildAppBundle = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/Menu.unity", "Assets/_Game/Scenes/Main.unity" },
                    target = BuildTarget.Android, locationPathName = OutputPath, options = BuildOptions.Development
                });
                result.state = report.summary.result.ToString(); result.bytes = report.summary.totalSize;
                result.errors = (int)report.summary.totalErrors; result.warnings = (int)report.summary.totalWarnings;
                result.seconds = report.summary.totalTime.TotalSeconds;
                if (report.summary.result != BuildResult.Succeeded) result.error = "Read Editor.log for build errors.";
            }
            catch (Exception exception)
            { result.state = "Exception"; result.error = exception.ToString(); Debug.LogException(exception); }
            Save(result);
        }
        private static void Save(Result result)
        { File.WriteAllText(ReportPath, JsonUtility.ToJson(result, true)); }
    }
}
