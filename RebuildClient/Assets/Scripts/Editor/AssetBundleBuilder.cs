using System.Collections;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class AssetBundleBuilder : MonoBehaviour
{
	[MenuItem("Build/Build Sprite Assets", priority = 0)]
	public static void BuildSpriteAssets()
	{
		//var target = Path.Combine(Application.streamingAssetsPath, @"Sprites.dat");
		if (!Directory.Exists(Application.streamingAssetsPath))
			Directory.CreateDirectory(Application.streamingAssetsPath);
		var bundleList = new List<AssetBundleBuild>();

		var spriteBundle = new AssetBundleBuild();
		spriteBundle.assetBundleName = "Sprites";

		var spritesList = new List<string>();

		foreach (var d in Directory.GetFiles("Assets/Sprites", "*.spr", SearchOption.AllDirectories))
		{
			//var sprite = AssetDatabase.LoadAssetAtPath(d, typeof(RoSpriteData)) as RoSpriteData;
			//Debug.Log(d);
			spritesList.Add(d.Replace("\\", "/"));
		}

		spriteBundle.assetNames = spritesList.ToArray();

		bundleList.Add(spriteBundle);

		BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath, bundleList.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression,
			BuildTarget.StandaloneWindows);
	}
}
