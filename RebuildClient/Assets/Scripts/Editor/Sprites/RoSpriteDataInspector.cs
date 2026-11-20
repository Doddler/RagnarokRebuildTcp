using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Sprites
{
    [CustomEditor(typeof(RoSpriteData))]
    public sealed class RoSpriteDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var spriteData = (RoSpriteData)target;
            if (string.IsNullOrEmpty(spriteData.SourceSprGuid))
                EditorGUILayout.HelpBox("Reimport the source SPR or ACT to link this asset to its SPR preview.", MessageType.Info);
            else if (GUILayout.Button("Open SPR Asset"))
                OpenSourceAsset(spriteData.SourceSprGuid);

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private static void OpenSourceAsset(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<RoSpriteAsset>(path);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Open SPR Asset", "The linked SPR preview could not be found.", "OK");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
