using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    public class WaterFix
    {
        [MenuItem("Ragnarok/Update Walkdata and Fix Water Tiles", false, 130)]
        public static void UpdateWaterTilesOnWalkData()
        {
            var guids = AssetDatabase.FindAssets("t:RoMapData", new[] { "Assets/Maps/" });
            foreach (var path in Directory.GetFiles("Assets/Maps/", "*.asset"))
            {
                // if (!path.Contains("iz_dun00"))
                //     continue;
                // Debug.Log(path);
                var data = AssetDatabase.LoadAssetAtPath<RoMapData>(path);

                var fName = Path.GetFileNameWithoutExtension(path);
                
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
            }
        }

    }
}