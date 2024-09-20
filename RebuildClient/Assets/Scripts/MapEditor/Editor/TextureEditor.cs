using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Assets.Scripts.MapEditor.Editor
{
    //[CustomEditor(typeof(Texture2D))]
    //[CanEditMultipleObjects]
    static class TextureEditor
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
        
        [MenuItem("CONTEXT/SpriteAtlasImporter/Save as PNG")]
        private static void AtlasTest(MenuCommand command)
        {
            var obj = command.context as SpriteAtlasImporter;
            if (obj == null)
                return;
            
            
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(obj.assetPath);
            // var atlas = SpriteAtlasAsset.Load(obj.assetPath);
            if (atlas == null)
                return;

            var s = new Sprite[atlas.spriteCount];

            atlas.GetSprites(s);
            var tex = s[0].texture;

            // var item atlas.sp
            
            var path = AssetDatabase.GetAssetPath(obj);
            var dir = Path.GetDirectoryName(path);
            var froot = Path.GetFileNameWithoutExtension(path);

            File.WriteAllBytes(Path.Combine(dir, froot + ".png"), tex.EncodeToPNG());
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
