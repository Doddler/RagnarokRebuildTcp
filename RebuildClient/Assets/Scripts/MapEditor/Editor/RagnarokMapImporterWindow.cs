using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.MapEditor.Editor.ObjectEditors;
using B83.Image.BMP;
using RebuildData.Shared.ClientTypes;
using SFB;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
// ReSharper disable StringIndexOfIsCultureSpecific.1

namespace Assets.Scripts.MapEditor.Editor
{

	class RagnarokMapImporterWindow : EditorWindow
	{
		//[MenuItem("Ragnarok/Test Import Effect")]
		//static void TestImportEffect()
		//{

		//}

		[MenuItem("Ragnarok/Build Sprite Attack Timing")]
		public static void BuildSpriteAttackTiming()
		{
			var guids = AssetDatabase.FindAssets("t:RoSpriteData", new[] { "Assets/Sprites" });

			var output = new List<string>();

			for (var i = 0; i < guids.Length; i++)
			{
				var path = AssetDatabase.GUIDToAssetPath(guids[i]);
				var asset = AssetDatabase.LoadAssetAtPath<RoSpriteData>(path);
				if (asset.Type != SpriteType.Monster && asset.Type != SpriteType.Monster2 && asset.Type != SpriteType.Pet)
					continue;
				
				var actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack1);
				if (actionId == -1)
					actionId = RoAnimationHelper.GetMotionIdForSprite(asset.Type, SpriteMotion.Attack2);
				if (actionId == -1)
					continue;

				var frames = asset.Actions[actionId].Frames;
				var found = false;
				for (var j = 0; j < frames.Length; j++)
				{
					if (frames[j].IsAttackFrame)
					{
						var time = j * asset.Actions[actionId].Delay;
						output.Add($"{asset.Name}:{time}");
						found = true;
						break;
					}
				}

				if (found)
					continue;
			}

			File.WriteAllLines(@"Assets/Sprites/AttackTiming.txt", output);
		}

		[MenuItem("Ragnarok/Update Addressables")]
		public static void UpdateAddressables()
		{
			var settings = AddressableAssetSettingsDefaultObject.Settings;
			var defGroup = settings.DefaultGroup;
			var mapGroup = settings.FindGroup("Scenes");
			var musicGroup = settings.FindGroup("Music");
            var entriesAdded = new List<AddressableAssetEntry>();
			var entriesRemoved = new List<AddressableAssetEntry>();
			
			var monText = AssetDatabase.LoadAssetAtPath(@"Assets/Data/monsterclass.json", typeof(TextAsset)) as TextAsset;
			var monsters = JsonUtility.FromJson<DatabaseMonsterClassData>(monText.text);
			
			//update sprites
			var guids = AssetDatabase.FindAssets("t:RoSpriteData", new[] { "Assets/Sprites" });

			for (int i = 0; i < guids.Length; i++)
			{
				var path = AssetDatabase.GUIDToAssetPath(guids[i]);
				var fName = Path.GetFileName(path);
				if (path.Contains("Monsters") && monsters.MonsterClassData.All(m => m.SpriteName != fName))
				{
					//Debug.Log("Not found: " + fName);
					var existing = defGroup.GetAssetEntry(guids[i]);
					if (existing == null)
						continue;
					settings.RemoveAssetEntry(guids[i], true);
					entriesRemoved.Add(existing);
					continue;
				}

				var entry = settings.CreateOrMoveEntry(guids[i], defGroup, readOnly: false, postEvent: false);
				//Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
				entry.address = AssetDatabase.GUIDToAssetPath(guids[i]);
				entry.labels.Add("Sprite");

				entriesAdded.Add(entry);
			}
			
			var mapText = AssetDatabase.LoadAssetAtPath(@"Assets/Data/maps.json", typeof(TextAsset)) as TextAsset;
			var maps = JsonUtility.FromJson<ClientMapList>(mapText.text);
            var musicNames = new List<string>();

			//update scenes
			guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/Maps" });

			for (int i = 0; i < guids.Length; i++)
			{
				var path = AssetDatabase.GUIDToAssetPath(guids[i]);
				var fName = Path.GetFileNameWithoutExtension(path);

                var map = maps.MapEntries.FirstOrDefault(m => m.Code == fName);

				if (map == null)
				{
					//Debug.Log("Not found: " + fName);
					var existing = mapGroup.GetAssetEntry(guids[i]);
					if (existing == null)
						continue;
					settings.RemoveAssetEntry(guids[i], true);
					entriesRemoved.Add(existing);
					continue;
				}

				var entry = settings.CreateOrMoveEntry(guids[i], mapGroup, readOnly: false, postEvent: false);
				//Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
				entry.address = path;
				entry.labels.Add("Maps");
				
				if(!musicNames.Contains(map.Music))
					musicNames.Add(map.Music);

				entriesAdded.Add(entry);
            }
			
            //update sprites
            guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Music" });

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var fName = Path.GetFileName(path);
                if (!musicNames.Contains(fName))
                {
                    //Debug.Log("Not found: " + fName);
                    var existing = musicGroup.GetAssetEntry(guids[i]);
                    if (existing == null)
                        continue;
                    settings.RemoveAssetEntry(guids[i], true);
                    entriesRemoved.Add(existing);
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guids[i], musicGroup, readOnly: false, postEvent: false);
                //Debug.Log(AssetDatabase.GUIDToAssetPath(guids[i]));
                entry.address = AssetDatabase.GUIDToAssetPath(guids[i]);
                entry.labels.Add("Music");

                entriesAdded.Add(entry);
            }

			settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entriesAdded, true);
			settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entriesRemoved, true);
		}

		//[MenuItem("Ragnarok/Test Import Model")]
		static void TestImportModel()
		{
			RagnarokModelLoader.LoadModelTest();
		}

		//[MenuItem("Ragnarok/Import Test Walk Data")]
		static RoMapData LoadWalkData(string importPath)
		{

			var altitude = new RagnarokWalkableDataImporter();

			//var importPath = @"G:\Projects2\Ragnarok\Resources\data\6@tower.gat";


			var walkData = altitude.LoadWalkData(importPath);
			walkData.name = Path.GetFileNameWithoutExtension(importPath) + "_walk";

			var saveDir = "Assets/maps/altitude";

			if (!Directory.Exists(saveDir))
				Directory.CreateDirectory(saveDir);

			var dataPath = Path.Combine(saveDir, walkData.name + ".asset").Replace("\\", "/");

			AssetDatabase.CreateAsset(walkData, dataPath);

			walkData.RefreshTextureLookup();
			walkData.RebuildAtlas();

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			walkData = AssetDatabase.LoadAssetAtPath<RoMapData>(dataPath);

			return walkData;
		}

        [MenuItem("Ragnarok/Import All Maps")]
        static void ImportAllFiles()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/maps.json");
            var maps = JsonUtility.FromJson<ClientMapList>(asset.text);
			
            foreach (var map in maps.MapEntries)
            {
                //Debug.Log($"Assets/Scenes/Maps/{map.Code}.unity");

                var scene = SceneManager.GetSceneByPath($"Assets/Scenes/Maps/{map.Code}.unity");
				if(scene.IsValid())
					continue;

				//Debug.Log(Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, map.Code + ".gnd"));

				ImportMap(Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, map.Code + ".gnd"));
            }

		}

        static void ImportMap(string f)
        {
            var saveDir = Path.Combine(Application.dataPath, "maps").Replace("\\", "/");

			var lastDirectory = Path.GetDirectoryName(f);
			var baseName = Path.GetFileNameWithoutExtension(f);
			Debug.Log(f);

			var relativeDir = saveDir.Substring(saveDir.IndexOf("Assets/"));

			var loader = new RagnarokMapLoader();
			var data = loader.ImportMap(f, relativeDir);
			var dataPath = Path.Combine(relativeDir, data.name + ".asset").Replace("\\", "/");

			AssetDatabase.CreateAsset(data, dataPath);

			data = AssetDatabase.LoadAssetAtPath<RoMapData>(dataPath);

			data.RefreshTextureLookup();
			data.RebuildAtlas();

			var gatPath = Path.Combine(lastDirectory, baseName + ".gat");
			data.WalkData = LoadWalkData(gatPath);


			var resourcePath = Path.Combine(lastDirectory, baseName + ".rsw");
			if (File.Exists(resourcePath))
			{
				var world = RagnarokResourceLoader.LoadResourceFile(resourcePath);
				world.name = baseName + " world data";

				var worldFolder = Path.Combine(relativeDir, "world");
				if (!Directory.Exists(worldFolder))
					Directory.CreateDirectory(worldFolder);

				var worldAssetPath = Path.Combine(worldFolder, baseName + "_world.asset").Replace("\\", "/");

				AssetDatabase.CreateAsset(world, worldAssetPath);

				foreach (var model in world.Models)
				{
					var baseFolder = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "model");

					var modelPath = Path.Combine(baseFolder, model.FileName);
					var relative = DirectoryHelper.GetRelativeDirectory(baseFolder, Path.GetDirectoryName(modelPath));
					var mBaseName = Path.GetFileNameWithoutExtension(model.FileName);

					var prefabFolder = Path.Combine("Assets/models/prefabs/", relative).Replace("\\", "/");
					var prefabPath = Path.Combine(prefabFolder, mBaseName + ".prefab").Replace("\\", "/");

					if (!Directory.Exists(prefabFolder))
						Directory.CreateDirectory(prefabFolder);


					if (!File.Exists(prefabPath))
					{
						var modelLoader = new RagnarokModelLoader();

						modelLoader.LoadModel(modelPath, relative);
						var obj = modelLoader.Compile();

						PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);

						var prefabRef = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
						DestroyImmediate(obj);

					}

				}

				//var builder = new RagnarokWorldSceneBuilder();
				//builder.Load(data, world);

			}

			AssetDatabase.SaveAssets();

			EditorUtility.UnloadUnusedAssetsImmediate();

			var importer = new RagnarokMapDataImporter(dataPath, baseName);
			importer.Import(true, true, true, true, true);

			AssetDatabase.SaveAssets();
		}


		[MenuItem("Ragnarok/Import Maps")]
		static void ImportFiles()
		{
			var files = StandaloneFileBrowser.OpenFilePanel("Open File", RagnarokDirectory.GetRagnarokDataDirectory, "gnd", true);

			if (files.Length <= 0)
				return;

			//var saveDir = EditorUtility.SaveFolderPanel("Save Folder", Application.dataPath, "");
			var saveDir = Path.Combine(Application.dataPath, "maps").Replace("\\", "/");
			//Debug.Log(saveDir);

			foreach (var f in files)
            {
                ImportMap(f);
            }
		}

		//[MenuItem("Ragnarok/Test Import Texture")]
		//static void TestImportTexture()
		//{
		//    //var file = EditorUtility.OpenFilePanel("Load Image", )
		//    var tex = LoadTexture(@"G:\Projects2\Ragnarok\Resources\data\texture\기타마을\sage_bar01.bmp");
		//    var png = tex.EncodeToPNG();
		//    File.WriteAllBytes(@"G:\Projects2\test.png", png);
		//}

		private static Texture2D LoadTexture(string path)
		{
			var bmp = new BMPLoader();
			bmp.ForceAlphaReadWhenPossible = false;
			var img = bmp.LoadBMP(path);

			var colors = (Color32[])img.imageData.Clone();

			var width = img.info.width;
			var height = img.info.height;

			//magic pink conversion and transparent color expansion
			for (var y = 0; y < width; y++)
			{
				for (var x = 0; x < height; x++)
				{
					var count = 0;
					var r = 0;
					var g = 0;
					var b = 0;

					var color = colors[x + y * width];
					if (color.r != 255 || color.g != 0 || color.b != 255)
						continue;

					//Debug.Log("OHWOW: " + color);

					for (var y2 = -1; y2 <= 1; y2++)
					{
						for (var x2 = -1; x2 <= 1; x2++)
						{
							if (y + y2 < 0 || y + y2 >= width)
								continue;
							if (x + x2 < 0 || x + x2 >= height)
								continue;
							var color2 = colors[x + x2 + (y + y2) * width];

							if (color2.r == 255 && color2.g == 0 && color2.b == 255)
								continue;

							count++;

							r += color2.r;
							g += color2.g;
							b += color2.b;
						}
					}

					if (count > 0)
					{
						var r2 = (byte)Mathf.Clamp(r / count, 0, 255);
						var g2 = (byte)Mathf.Clamp(g / count, 0, 255);
						var b2 = (byte)Mathf.Clamp(b / count, 0, 255);

						//Debug.Log($"{x},{y} - change {color} to {r2},{g2},{b2}");

						img.imageData[x + y * width] = new Color32(r2, g2, b2, 0);
					}
					else
						img.imageData[x + y * width] = new Color32(0, 0, 0, 0);
				}
			}

			return img.ToTexture2D();
		}
	}
}
