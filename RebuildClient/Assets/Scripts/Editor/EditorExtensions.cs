using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    public static class EditorGuiLayoutUtility
    {
        public static readonly Color DEFAULT_COLOR = new Color(0f, 0f, 0f, 0.3f);
        public static readonly Vector2 DEFAULT_LINE_MARGIN = new Vector2(2f, 2f);

        public const float DEFAULT_LINE_HEIGHT = 1f;

        public static void HorizontalLine(Color color, float height, Vector2 margin)
        {
            GUILayout.Space(margin.x);

            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, height), color);

            GUILayout.Space(margin.y);
        }
        public static void HorizontalLine(Color color, float height) => EditorGuiLayoutUtility.HorizontalLine(color, height, DEFAULT_LINE_MARGIN);
        public static void HorizontalLine(Color color, Vector2 margin) => EditorGuiLayoutUtility.HorizontalLine(color, DEFAULT_LINE_HEIGHT, margin);
        public static void HorizontalLine(float height, Vector2 margin) => EditorGuiLayoutUtility.HorizontalLine(DEFAULT_COLOR, height, margin);

        public static void HorizontalLine(Color color) => EditorGuiLayoutUtility.HorizontalLine(color, DEFAULT_LINE_HEIGHT, DEFAULT_LINE_MARGIN);
        public static void HorizontalLine(float height) => EditorGuiLayoutUtility.HorizontalLine(DEFAULT_COLOR, height, DEFAULT_LINE_MARGIN);
        public static void HorizontalLine(Vector2 margin) => EditorGuiLayoutUtility.HorizontalLine(DEFAULT_COLOR, DEFAULT_LINE_HEIGHT, margin);

        public static void HorizontalLine() => EditorGuiLayoutUtility.HorizontalLine(DEFAULT_COLOR, DEFAULT_LINE_HEIGHT, DEFAULT_LINE_MARGIN);

#if UNITY_EDITOR
#endif
    }
}
