using System.IO;
using System.IO.Compression;
using Assets.Scripts.MapEditor.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class BuildTool : IActiveBuildTargetChanged
{
    private static void SwitchBuildTargets(BuildTarget target)
    {
        if (target == BuildTarget.WebGL)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.WebGL;
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.WebGL;
        }
        else
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.StandaloneWindows64;
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
        }
    }

    [MenuItem("Build/Build Everything", false, 1)]
    public static void TheFullMonte()
    {
        RagnarokMapImporterWindow.UpdateAddressables();

        var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        var target = EditorUserBuildSettings.activeBuildTarget;
        
        //webgl
        BuildForPlatform(BuildTargetGroup.WebGL, BuildTarget.WebGL, "Build/WebGL/ragnarok/", true, true);

        //windows64
        BuildForPlatform(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, "Build/PC/RebuildClient.exe", true, true);

        //restore
        SwitchBuildTargets(target);
        
        //zip PC build
        ZipPcBuildIntoWebGL();
    }
    
    [MenuItem("Build/Build Everything (No Addressables)", false, 2)]
    public static void EverythingWithoutAddressables()
    {
        //webgl
        BuildForPlatform(BuildTargetGroup.WebGL, BuildTarget.WebGL, "Build/WebGL/ragnarok/", false, false);

        //windows64
        BuildForPlatform(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, "Build/PC/RebuildClient.exe", false, false);
        
        //zip PC build
        ZipPcBuildIntoWebGL();
    }

    [MenuItem("Build/Test the zip thing", false, 2000)]
    private static void ZipPcBuildIntoWebGL()
    {

        if (Directory.Exists("Build/RagnarokRebuild"))
            Directory.Delete("Build/RagnarokRebuild", true);

        //the double folder name is dumb but we want the zip file to have a folder in it so it's gotta be done
        Directory.CreateDirectory("Build/RagnarokRebuild/RagnarokRebuild");
        foreach (var path in Directory.GetFiles("Build/PC"))
            File.Copy(path, Path.Combine("Build/RagnarokRebuild/RagnarokRebuild", Path.GetFileName(path)));

        CopyFilesRecursively("Build/PC/RebuildClient_Data", "Build/RagnarokRebuild/RagnarokRebuild/RebuildClient_Data");



        if (File.Exists("Build/WebGL/RagnarokRebuild.zip"))
            File.Delete("Build/WebGL/RagnarokRebuild.zip");
        ZipFile.CreateFromDirectory("Build/RagnarokRebuild", "Build/WebGL/RagnarokRebuild.zip");
    }

    private static void BuildForPlatform(BuildTargetGroup group, BuildTarget platform, string location, bool updateAddressables, bool swapToPlatform)
    {
        Debug.Log($"Building for platform {group}:{platform} in destination folder {location} ({(updateAddressables ? "with" : "without")} addressables)");
        if(swapToPlatform)
            SwitchBuildTargets(platform);

        if (updateAddressables)
            BuildTool.UpdateAddressables();

        var folder = location;
        if (folder.ToLower().EndsWith(".exe"))
            folder = Path.GetDirectoryName(folder);

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        if (platform == BuildTarget.WebGL)
        {
            //remove old build folders
            foreach(var dir in Directory.GetDirectories(location, "*", SearchOption.AllDirectories))
                if(dir.Contains("Build_"))
                    Directory.Delete(dir, true);
            var indexPath = Path.Combine(location, "index.html");
            if(File.Exists(indexPath))
                File.Delete(indexPath);
        }
        
        var options = new BuildPlayerOptions();
        options.locationPathName = location;
        options.targetGroup = group;
        options.target = platform;
        options.options = BuildOptions.None;
        options.scenes = new[] { "Assets/Scenes/MainScene.unity" };
        BuildPipeline.BuildPlayer(options);
    }


    private static void CopyFilesRecursively(string sourcePath, string targetPath)
    {
        //Now Create all of the directories
        foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
        }

        //Copy all the files & Replaces any files with the same name
        foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, newPath);
            File.Copy(newPath, Path.Combine(targetPath, relative), true);
        }
    }

    [MenuItem("Build/Build WebGL", false, 50)]
    public static void BuildWebGL()
    {
        BuildForPlatform(BuildTargetGroup.WebGL, BuildTarget.WebGL, "Build/WebGL", false, false);
    }

    [MenuItem("Build/Build Windows", false, 51)]
    public static void BuildWindows()
    {
        BuildForPlatform(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, "Build/PC/RebuildClient.exe", false, false);
    }

    private static void UpdateAddressables()
    {
        var path = ContentUpdateScript.GetContentStateDataPath(false);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        Debug.Log("Path: " + path);
        
        var builderInput = new AddressablesDataBuilderInput(settings);

        var menu = new AddressablesBuildMenuUpdateAPreviousBuild(); //this is fucking scuffed
        menu.OnPrebuild(builderInput);
        
//        settings.RemoteCatalogLoadPath.
        //ContentUpdateScript.BuildContentUpdate(settings, path);

    }

    [MenuItem("Ragnarok/Open Main Scene", priority = 20)]
    private static void LoadMainScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            
            // Debug.Log((SceneView)SceneView.sceneViews[0]);
            // Debug.Log(SceneView.currentDrawingSceneView.camera);
            // ((SceneView)SceneView.sceneViews[0]).camera.transform.position = new Vector3(802.82f, 496.01f, -1131.95f);
            // ((SceneView)SceneView.sceneViews[0]).camera.transform.rotation = Quaternion.identity;


            var bounds = new Bounds(new Vector3(800, 500, 0), new Vector3(600, 600, 600));
            SceneView.lastActiveSceneView.Frame(bounds, true);
            SceneView.lastActiveSceneView.in2DMode = true;
            
            EditorSceneManager.OpenScene($"Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
            
        }
    }

    [MenuItem("Build/Update Addressables (Current Platform)", false, 100)]
    public static void UpdateAddressablesBuild()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        Debug.Log($"Updating addressables for platform {target}");
        
        SwitchBuildTargets(target);
        
        RagnarokMapImporterWindow.UpdateAddressables();
        UpdateAddressables();

    }


    [MenuItem("Build/Update Addressables (All Platforms)", false, 100)]
    public static void UpdateAddressablesBuildAll()
    {
        RagnarokMapImporterWindow.UpdateAddressables();

        //var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        var target = EditorUserBuildSettings.activeBuildTarget;

        SwitchBuildTargets(BuildTarget.WebGL);

        UpdateAddressables();

        SwitchBuildTargets(BuildTarget.StandaloneWindows64);

        UpdateAddressables();
        SwitchBuildTargets(target);
    }


    [MenuItem("Build/Full Addressables Rebuild/Build All", false, 150)]
    public static void FullAddressablesBuild()
    {
        //var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        var target = EditorUserBuildSettings.activeBuildTarget;
        
        AddressableAssetSettings.CleanPlayerContent();

        SwitchBuildTargets(BuildTarget.WebGL);

        RagnarokMapImporterWindow.UpdateAddressables();
        AddressableAssetSettings.BuildPlayerContent();

        SwitchBuildTargets(BuildTarget.StandaloneWindows64);

        RagnarokMapImporterWindow.UpdateAddressables();
        AddressableAssetSettings.BuildPlayerContent();
        
        SwitchBuildTargets(target);

        //var path = ContentUpdateScript.GetContentStateDataPath(false);

        //Debug.Log(path);
    }
    
    [MenuItem("Build/Full Addressables Rebuild/Build WebGL", false, 150)]
    public static void FullAddressablesWebGLBuild()
    {
        //var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        var target = EditorUserBuildSettings.activeBuildTarget;
        
        AddressableAssetSettings.CleanPlayerContent();

        SwitchBuildTargets(BuildTarget.WebGL);

        RagnarokMapImporterWindow.UpdateAddressables();
        AddressableAssetSettings.BuildPlayerContent();

        SwitchBuildTargets(target);

        //var path = ContentUpdateScript.GetContentStateDataPath(false);

        //Debug.Log(path);
    }
    
        
    [MenuItem("Build/Full Addressables Rebuild/Build PC", false, 150)]
    public static void FullAddressablesPCBuild()
    {
        //var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        var target = EditorUserBuildSettings.activeBuildTarget;
        
        AddressableAssetSettings.CleanPlayerContent();

        SwitchBuildTargets(BuildTarget.StandaloneWindows64);

        RagnarokMapImporterWindow.UpdateAddressables();
        AddressableAssetSettings.BuildPlayerContent();

        SwitchBuildTargets(target);

        //var path = ContentUpdateScript.GetContentStateDataPath(false);

        //Debug.Log(path);
    }
    
    public int callbackOrder { get; }
    public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
    {
        //throw new System.NotImplementedException();
    }
}
