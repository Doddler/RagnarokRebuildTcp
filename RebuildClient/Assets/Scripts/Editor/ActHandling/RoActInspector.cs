using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.ActHandling
{
    [CustomEditor(typeof(RoActAsset))]
    public class RoActInspector : UnityEditor.Editor
    {
        private SerializedProperty actName;
        private SerializedProperty actVersion;
        private SerializedProperty animationClips;

        private void OnEnable()
        {
            actName = serializedObject.FindProperty("actName");
            actVersion = serializedObject.FindProperty("actVersion");
            animationClips = serializedObject.FindProperty("animationClips");
        }

        public override void OnInspectorGUI()
        {
            DrawImporterGUI();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawImporterGUI()
        {
            EditorGUILayout.PropertyField(actName, new GUIContent("Act Name"));
            EditorGUILayout.PropertyField(actVersion, new GUIContent("Act Version"));
            EditorGUILayout.PropertyField(animationClips, new GUIContent("Animation Clips"));
            
        }
    }
}