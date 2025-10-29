using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.SprHandling
{
    [CustomEditor(typeof(RoSprAsset))]
    public class RoSprInspector : UnityEditor.Editor
    {
        private SerializedProperty sprName;
        private SerializedProperty sprVersion;
        private SerializedProperty sprites;
        private SerializedProperty palette;
        private SerializedProperty atlas;
        private SerializedProperty atlasRects;

        private void OnEnable()
        {
            sprName = serializedObject.FindProperty("sprFileName");
            sprVersion = serializedObject.FindProperty("spriteVersion");
            palette = serializedObject.FindProperty("palette");
            atlas = serializedObject.FindProperty("atlas");
        }

        public override void OnInspectorGUI()
        {
            DrawImporterGUI();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawImporterGUI()
        {
            EditorGUILayout.PropertyField(sprName, new GUIContent("Sprite Name"));
            EditorGUILayout.PropertyField(sprVersion, new GUIContent("Sprite Version"));
            EditorGUILayout.PropertyField(palette, new GUIContent("Palette"));
            EditorGUILayout.PropertyField(atlas, new GUIContent("Atlas"));
        }
    }
}