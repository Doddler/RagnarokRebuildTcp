using UnityEditor;
using UnityEngine;

namespace Scripts.Sprites.Editor
{
    [CustomEditor(typeof(RoSprAsset))]
    public class RoSprInspector : UnityEditor.Editor
    {
        private SerializedProperty sprName;
        private SerializedProperty spriteVersion;
        private SerializedProperty sprites;
        private SerializedProperty palette;
        private SerializedProperty atlas;
        private SerializedProperty atlasRects;

        private void OnEnable()
        {
            sprName = serializedObject.FindProperty("sprName");
            //sprites = serializedObject.FindProperty("sprites");
            spriteVersion = serializedObject.FindProperty("spriteVersion");
            palette = serializedObject.FindProperty("palette");
            atlas = serializedObject.FindProperty("atlas");
            //atlasRects = serializedObject.FindProperty("atlasRects");
        }
        
        public override void OnInspectorGUI()
        {
            DrawImporterGUI();
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawImporterGUI()
        {
            EditorGUILayout.PropertyField(sprName, new GUIContent("Sprite Name"));
            EditorGUILayout.PropertyField(spriteVersion, new GUIContent("Sprite Version"));
            //EditorGUILayout.PropertyField(sprites, new GUIContent("Sprites"), true);
            EditorGUILayout.PropertyField(palette, new GUIContent("Palette"));
            EditorGUILayout.PropertyField(atlas, new GUIContent("Atlas"));
            //EditorGUILayout.PropertyField(atlasRects, new GUIContent("Atlas Rects"), true);
        }
    }
}