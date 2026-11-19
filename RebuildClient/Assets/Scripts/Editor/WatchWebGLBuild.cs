using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// WebGL build for the bot dashboard's Watch tab (Other/RoBotClient/RoBotClient.Web/Features/Watch), which serves it.
/// Addressables are built with the "Local" profile so assets ship in the build instead of loading from dodsrv.com.
/// Headless: Unity -batchmode -quit -projectPath RebuildClient -executeMethod WatchWebGLBuild.Build -logFile -
/// </summary>
public static class WatchWebGLBuild
{
    public const string Output = "Build/WatchWebGL";

    [MenuItem("Build/Build WebGL for bot Watch tab", false, 52)]
    public static void Build()
    {
        var previousGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        var previousTarget = EditorUserBuildSettings.activeBuildTarget;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var previousProfile = settings.activeProfileId;
        var ok = false;
        try
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            settings.activeProfileId = settings.profileSettings.GetProfileId("Local");

            AddressableAssetSettings.BuildPlayerContent(out var addressables);
            if (!string.IsNullOrEmpty(addressables.Error))
                throw new System.Exception("Addressables build failed: " + addressables.Error);

            if (Directory.Exists(Output))
                Directory.Delete(Output, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                locationPathName = Output,
                targetGroup = BuildTargetGroup.WebGL,
                target = BuildTarget.WebGL,
                scenes = new[] { "Assets/Scenes/MainScene.unity" },
            });
            ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log($"Watch WebGL build: {report.summary.result} -> {Path.GetFullPath(Output)}");
        }
        finally
        {
            settings.activeProfileId = previousProfile; // leave the project as it was
            AssetDatabase.SaveAssets(); // the addressables build saved the Local profile to disk
            EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }
}
