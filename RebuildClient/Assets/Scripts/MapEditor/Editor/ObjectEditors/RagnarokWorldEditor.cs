using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.MapEditor.Editor.ObjectEditors
{
    [CustomEditor(typeof(RagnarokWorld))]
    class RagnarokWorldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
	        if (GUILayout.Button("Load in Scene"))
            {
                var world = (RagnarokWorld)target;
                
                var path = AssetDatabase.GetAssetPath(world);
                var basePath = Path.GetDirectoryName(path);
                var parentPath = Directory.GetParent(basePath).ToString();

                var mapDataPath = Path.Combine(parentPath, world.MapName + ".asset").Replace("\\", "/");

                Debug.Log(mapDataPath);

                var mapData = AssetDatabase.LoadAssetAtPath<RoMapData>(mapDataPath);

                Debug.Log(mapData);

                var builder = new RagnarokWorldSceneBuilder();
                builder.Load(mapData, world);
                


                //var go = new GameObject(data.name);
                //var editor = go.AddComponent<RoMapEditor>();
                //editor.Initialize(data);
                //Selection.activeGameObject = go;
            }

	        if (GUILayout.Button("Load Effect Placeholders"))
	        {
		        var world = (RagnarokWorld) target;

		        var path = AssetDatabase.GetAssetPath(world);
		        var basePath = Path.GetDirectoryName(path);
		        var parentPath = Directory.GetParent(basePath).ToString();

		        var mapDataPath = Path.Combine(parentPath, world.MapName + ".asset").Replace("\\", "/");

		        Debug.Log(mapDataPath);

		        var mapData = AssetDatabase.LoadAssetAtPath<RoMapData>(mapDataPath);

		        Debug.Log(mapData);

		        var builder = new RagnarokWorldSceneBuilder();
		        builder.LoadEffectPlaceholders(mapData, world);
            }

            base.OnInspectorGUI();
        }
    }
}
