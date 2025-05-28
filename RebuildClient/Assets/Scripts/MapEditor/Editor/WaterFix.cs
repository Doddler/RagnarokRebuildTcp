using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.MapEditor.Editor
{
    public class WaterFix
    {
        [MenuItem("Ragnarok/Update Walkdata and Fix Water Tiles", false, 130)]
        public static void UpdateWaterTilesOnWalkData()
        {
            var count = 0;
            var guids = AssetDatabase.FindAssets("t:RoMapData", new[] { "Assets/Maps/" });
            foreach (var path in Directory.GetFiles("Assets/Maps/", "*.asset"))
            {
                count++;
                if (count < 420)
                    continue;
                // if (!path.Contains("gef_fild10"))
                //     continue;
                // Debug.Log(path);
                
                var fName = Path.GetFileNameWithoutExtension(path);
                var data = AssetDatabase.LoadAssetAtPath<RoMapData>(path);

                var scenePath = $"Assets/Scenes/Maps/{fName}.unity";

                if (!File.Exists(scenePath))
                    continue;
                
                EditorSceneManager.OpenScene($"Assets/Scenes/Maps/{fName}.unity");
               
                var dir = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, fName + ".gat");
                
                if(!File.Exists(dir))
                    continue;
                Debug.Log(dir);
                
                var waterLevel = -data.Water.Level + (data.Water.WaveHeight / 5f) / 2f;
                var walkData = RagnarokMapImporterWindow.LoadWalkData(dir, waterLevel);
                data.WalkData = walkData;
                data.WalkCellData = walkData.WalkCellData;

                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                
                var walkObj = GameObject.Find($"{fName}_walk");
                if (walkObj != null)
                {
                    var editor = walkObj.GetComponent<RoMapEditor>();
                    if(editor != null)
                        editor.Initialize(walkData);
                }

                var walkProviderGo = GameObject.Find("WalkData Provider");
                if (walkProviderGo != null)
                {
                    var walkProvider = walkProviderGo.GetComponent<RoWalkDataProvider>();
                    if (walkProvider != null)
                        walkProvider.WalkData = walkData.WalkCellData;
                }
                
                EditorSceneManager.SaveOpenScenes();

            }
        }

    }
}