using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    //[CustomEditor(typeof(Texture2D))]
    //[CanEditMultipleObjects]
    class TextureEditor : UnityEditor.Editor
    {
        [MenuItem("CONTEXT/Texture2D/Save as PNG")]
        private static void MenuOptionTest(MenuCommand command)
        {
            var obj = command.context as Texture2D;
            if (obj == null)
                return;

            var path = AssetDatabase.GetAssetPath(obj);
            var dir = Path.GetDirectoryName(path);
            var froot = Path.GetFileNameWithoutExtension(path);

            File.WriteAllBytes(Path.Combine(dir, froot + ".png"), obj.EncodeToPNG());
            AssetDatabase.Refresh();
        }

        //public override void OnInspectorGUI()
        //{
        //    if (GUILayout.Button("Save as PNG"))
        //    {
        //        var tex2d = (Texture2D)target;
        //        var path = AssetDatabase.GetAssetPath(tex2d);
        //        var dir = Path.GetDirectoryName(path);
        //        var froot = Path.GetFileNameWithoutExtension(path);

        //        File.WriteAllBytes(Path.Combine(dir, froot + ".png"), tex2d.EncodeToPNG());
        //        AssetDatabase.Refresh();
        //    }

        //    //DrawDefaultInspector();

        //    base.OnInspectorGUI();
        //}
    }
}
