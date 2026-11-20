using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Sprites
{
    [CustomEditor(typeof(RoSpriteAsset))]
    public class RoSpriteAssetInspector : UnityEditor.Editor
    {
        private const int PreviewHeight = 200;
        private int imageIndex;
        private float zoom = 2;
        private int background;
        private Vector2 scroll;

        internal float PreviewZoom => zoom;
        internal Rect ZoomControlRect { get; private set; }

        public override void OnInspectorGUI()
        {
            // Unity disables inspectors for imported assets. These controls only change
            // preview state; they never edit the imported asset.
            var wasEnabled = GUI.enabled;
            try
            {
                GUI.enabled = true;
                DrawViewer((RoSpriteAsset)target);
            }
            finally
            {
                GUI.enabled = wasEnabled;
            }
        }

        private void DrawViewer(RoSpriteAsset asset)
        {
            EditorGUILayout.LabelField(asset.sprFileName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("SPR version", asset.sprVersion);
            EditorGUILayout.LabelField("Images", $"{asset.images.Length} ({asset.indexedImageCount} indexed)");
            var actPath = RoSpriteImporter.FindSibling(AssetDatabase.GetAssetPath(asset), ".act");
            if (actPath == null)
                EditorGUILayout.HelpBox("Add a same-name .act file beside this SPR to import animations.", MessageType.Info);
            else if (GUILayout.Button("Show ACT Source"))
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(actPath));
            if (asset.images.Length > 0)
            {
                imageIndex = EditorGUILayout.IntSlider("Image", imageIndex, 0, asset.images.Length - 1);
                var texture = asset.images[imageIndex];
                EditorGUILayout.LabelField("Dimensions", $"{texture.width} × {texture.height}");
                EditorGUILayout.LabelField("Format", imageIndex < asset.indexedImageCount ? "Indexed" : "True color");
                var zoomRect = EditorGUILayout.GetControlRect();
                if (Event.current.type == EventType.Repaint)
                    ZoomControlRect = zoomRect;
                zoom = EditorGUI.Slider(zoomRect, "Zoom", zoom, 0.25f, 16);
                background = EditorGUILayout.Popup("Background", background, new[] { "Checkerboard", "Black", "White", "Gray" });
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(PreviewHeight));
                var rect = GUILayoutUtility.GetRect(texture.width * zoom, texture.height * zoom,
                    GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                if (background == 0) EditorGUI.DrawTextureTransparent(rect, texture);
                else
                {
                    EditorGUI.DrawRect(rect, background == 1 ? Color.black : background == 2 ? Color.white : Color.gray);
                    GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            var paletteRect = GUILayoutUtility.GetRect(192, 192, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(paletteRect, asset.palette, ScaleMode.ScaleToFit, false);
        }
    }
}
