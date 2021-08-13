using System.IO;
using Assets.Scripts.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.MapEditor.Editor.ObjectEditors
{
	public class RagnarokMapDataImporter
	{
		private readonly string assetPath;
		private readonly string sceneName;

		private RoMapData data;

        public RagnarokMapDataImporter(RoMapData data)
		{
			this.data = data;
			sceneName = data.name;
		}

		public RagnarokMapDataImporter(string assetPath, string sceneName)
		{
			this.assetPath = assetPath;
			this.sceneName = sceneName;
		}

		public void Import(bool makeScene, bool loadMapMesh, bool loadModels, bool loadWalkData, bool exportWalkData)
		{
			Scene scene = default;
			if (makeScene)
				scene = MakeScene();

            if(loadMapMesh)
				LoadMapMeshData();

            if(loadModels)
				LoadMapAssets();

            if(loadWalkData)
				LoadWalkData();

            if (exportWalkData)
	            ExportWalkData();
            
            if (makeScene)
			{
				SetSceneLightingSettings.SetLightingSettings();
                if (!Directory.Exists("Assets/Scenes/Maps/"))
	                Directory.CreateDirectory("Assets/Scenes/Maps/");
				EditorSceneManager.SaveScene(scene, "Assets/Scenes/Maps/" + scene.name + ".unity");
			}

		}


        private void UnloadOldScenes()
        {
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name != "MainScene")
                    SceneManager.UnloadSceneAsync(scene);
            }
        }

        private Scene MakeScene()
        {
            UnloadOldScenes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = sceneName;
            EditorSceneManager.SetActiveScene(scene);

            if (data == null)
	            data = AssetDatabase.LoadAssetAtPath<RoMapData>(assetPath);

            return scene;
        }

        private void LoadMapMeshData()
        {
            var go = new GameObject(data.name);
            var editor = go.AddComponent<RoMapEditor>();
            editor.Initialize(data);
            Selection.activeGameObject = go;
        }

        private void UpdateWalkDataProvider(RoMapData walkData)
        {
            if (walkData.WalkCellData == null)
            {
                Debug.LogWarning($"Walk data entity {walkData.name} did not have walk cell data.");
                return;
            }

            var wp = GameObject.FindObjectOfType<RoWalkDataProvider>();
            if (wp == null)
            {
                var go = new GameObject("WalkData Provider");
                wp = go.AddComponent<RoWalkDataProvider>();
            }

            wp.WalkData = walkData.WalkCellData;
        }

        private void LoadWalkData()
        {
            if (data.WalkData == null)
            {
                var path = AssetDatabase.GetAssetPath(data);
                var dirName = Path.GetDirectoryName(path);
                var walkPath = Path.Combine(dirName, "altitude/" + data.name + "_walk.asset");

                data.WalkData = AssetDatabase.LoadAssetAtPath<RoMapData>(walkPath);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }

            if (data.WalkData != null)
            {
                var wd = data.WalkData;
                var go = new GameObject(wd.name);
                var editor = go.AddComponent<RoMapEditor>();
                editor.Initialize(wd);
                editor.LeaveEditMode();

                UpdateWalkDataProvider(wd);
            }
        }

        private void LoadMapAssets()
        {
            var mapPath = AssetDatabase.GetAssetPath(data);
            var mapName = data.name;
            var rootPath = Path.GetDirectoryName(mapPath);
            var worldPath = Path.Combine(rootPath, "world", mapName + "_world.asset");
            Debug.Log(worldPath);
            if (File.Exists(worldPath))
            {
                var world = AssetDatabase.LoadAssetAtPath<RagnarokWorld>(worldPath);
                if (world != null)
                {
                    var builder = new RagnarokWorldSceneBuilder();
                    builder.Load(data, world);
                }
                else
                    Debug.Log("Failed to load world data at path: " + worldPath);
            }
            else
                Debug.LogError("Could not find world data at path: " + worldPath);
        }

        private void ExportWalkData()
        {
            //var path = EditorUtility.SaveFilePanel("Save Map Data", "Assets/Maps/exportdata/", data.name + ".walk", "walk");

            //if (!string.IsNullOrWhiteSpace(path))

            var path = "Assets/Maps/exportdata/" + data.name + ".walk";
            data.WalkData.WalkCellData.ExportToFile(path);
        }
    }

    [CustomEditor(typeof(RoMapData))]
    class RagnarokMapDataEditor : UnityEditor.Editor
    {


        public override void OnInspectorGUI()
        {
	        if (GUILayout.Button("Load Into New Scene"))
	        {
                var importer = new RagnarokMapDataImporter((RoMapData)target);
                importer.Import(true, true, true, true, true);
	        }

            if (GUILayout.Button("Load Full Map Data"))
            {
	            var importer = new RagnarokMapDataImporter((RoMapData)target);
	            importer.Import(false, true, true, true, false);
            }

            if (GUILayout.Button("Load Mesh & Assets in Scene"))
            {
	            var importer = new RagnarokMapDataImporter((RoMapData)target);
	            importer.Import(false, true, true, false, false);
            }

            if (GUILayout.Button("Load Assets Only in Scene"))
            {
	            var importer = new RagnarokMapDataImporter((RoMapData)target);
	            importer.Import(false, false, true, false, false);
            }

            if (GUILayout.Button("Load Mesh Only in Scene"))
            {
	            var importer = new RagnarokMapDataImporter((RoMapData)target);
	            importer.Import(false, true, false, false, false);
            }

            if (GUILayout.Button("Load Walk Data Only in Scene"))
            {
	            var importer = new RagnarokMapDataImporter((RoMapData)target);
	            importer.Import(false, false, false, true, false);
                
            }


            if (GUILayout.Button("Export Walk Data"))
            {
	            var importer = new RagnarokMapDataImporter((RoMapData)target);
	            importer.Import(false, false, false, false, true);
            }

			base.OnInspectorGUI();

        }
    }
}
