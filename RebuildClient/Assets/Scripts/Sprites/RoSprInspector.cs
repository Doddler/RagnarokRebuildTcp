using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEngine;

namespace Sprites
{
    [CustomEditor(typeof(RoSprAsset))]
    public class RoSprInspector : Editor
    {
        private SerializedProperty sprName;
        private SerializedProperty spriteVersion;
        private SerializedProperty sprites;
        private SerializedProperty palette;

        private void OnEnable()
        {
            sprName = serializedObject.FindProperty("sprName");
            sprites = serializedObject.FindProperty("sprites");
            spriteVersion = serializedObject.FindProperty("spriteVersion");
            palette = serializedObject.FindProperty("palette");
            Debug.Log($"Palette: {palette.objectReferenceValue}");
        }
        
        public override void OnInspectorGUI()
        {
            DrawImporterGUI();
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawImporterGUI()
        {
            //TODO: Create Property Fields of all RoSprAsset fields.
            EditorGUILayout.PropertyField(sprName, new GUIContent("Sprite Name"));
            EditorGUILayout.PropertyField(spriteVersion, new GUIContent("Sprite Version"));
            EditorGUILayout.PropertyField(sprites, new GUIContent("Sprites"), true);
            EditorGUILayout.PropertyField(palette, new GUIContent("Palette"));
            Debug.Log($"Reference {palette.objectReferenceValue}");
            var texture = (Texture2D)palette.objectReferenceValue;
            if (texture != null)
            {
                GUILayout.Label(texture);
            }
            else
            {
                Debug.Log("Palette recovery is borked");
            }
        }
    }
}