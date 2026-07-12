#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KoG.MiniMvp.EditorTools
{
    /// <summary>Menu + -executeMethod entry for landscape Development APK.</summary>
    public static class KoGAndroidDevBuild
    {
        const string ApkRel = "Builds/Android/KoGMini-dev.apk";

        [MenuItem("KoG/Build Android Dev APK (Landscape)")]
        public static void BuildFromMenu()
        {
            var ok = BuildInternal();
            EditorUtility.DisplayDialog("KoG Android",
                ok ? "APK OK → " + Path.GetFullPath(ApkRel) : "Build FAILED — see Console",
                "OK");
        }

        /// <summary>Unity.exe -executeMethod KoG.MiniMvp.EditorTools.KoGAndroidDevBuild.BuildCli</summary>
        public static void BuildCli()
        {
            var ok = BuildInternal();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool BuildInternal()
        {
            ApplyLandscapeAndIdentity();
            AssetDatabase.SaveAssets();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[KoG] Switch to Android failed");
                    return false;
                }
            }

            var outDir = Path.GetDirectoryName(Path.GetFullPath(ApkRel));
            Directory.CreateDirectory(outDir);
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.buildAppBundle = false;

            var scenes = EditorBuildSettings.scenes;
            var paths = new string[scenes.Length];
            for (var i = 0; i < scenes.Length; i++) paths[i] = scenes[i].path;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = paths,
                locationPathName = Path.GetFullPath(ApkRel),
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });

            Debug.Log("[KoG] Android build " + report.summary.result + " errors=" + report.summary.totalErrors);
            return report.summary.result == BuildResult.Succeeded;
        }

        static void ApplyLandscapeAndIdentity()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.companyName = "KoG Development";
            PlayerSettings.productName = "Kingdom of War Mini";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.kog.kingdomofwar.mini");
        }
    }
}
#endif
