using System;
using UnityEditor;
using UnityEngine;
using static UnityEditor.Recorder.Examples.FFmpegEncoderSettings;

namespace UnityEditor.Recorder.Examples
{
    [CustomPropertyDrawer(typeof(FFmpegEncoderSettings))]
    class FFmpegEncoderSettingsPropertyDrawer : PropertyDrawer
    {
        static class Styles
        {
            internal static readonly GUIContent FormatLabel = new("Codec format", "The choice of codec format.");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return 0;
        }

        bool IsCodecFormatSupported(Enum arg)
        {
            var toCheck = (OutputFormat)arg;
            return IsOutputFormatSupported(toCheck);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            var ffmpegPath = property.FindPropertyRelative("ffmpegPath");
            ffmpegPath.stringValue = EditorGUILayout.TextField("FFmpeg Path", ffmpegPath.stringValue);
            // Some properties we want to draw
            var format = property.FindPropertyRelative("outputFormat");

            // Display choice of codec format, with some options potentially disabled
            format.intValue = (int)(OutputFormat)EditorGUILayout.EnumPopup(Styles.FormatLabel, (OutputFormat)format.intValue, IsCodecFormatSupported, true);

            EditorGUI.EndProperty();
        }
    }
}
