using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    //[CustomEditor(typeof(Texture2D))]
    //[CanEditMultipleObjects]
    static class TextureEditor
    {
        [MenuItem("CONTEXT/Mesh/Save data")]
        private static void MeshTest(MenuCommand command)
        {
            var obj = command.context as Mesh;
            if (obj == null)
                return;

            var textOut = new StringBuilder();

            textOut.AppendLine($"Vertex ({obj.vertices}):");
            for (var i = 0; i < obj.vertices.Length; i++)
                textOut.AppendLine(obj.vertices[i].ToString());
            
            textOut.AppendLine("");
            textOut.AppendLine($"UVs ({obj.uv.Length}):");
            for (var i = 0; i < obj.uv.Length; i++)
                textOut.AppendLine(obj.uv[i].ToString());

            textOut.AppendLine("");
            textOut.AppendLine($"Normals ({obj.normals.Length}):");
            for (var i = 0; i < obj.normals.Length; i++)
                textOut.AppendLine(obj.normals[i].ToString());
            
            textOut.AppendLine("");
            textOut.AppendLine($"Colors ({obj.colors.Length}):");
            for (var i = 0; i < obj.colors.Length; i++)
                textOut.AppendLine(obj.colors[i].ToString());
            
            textOut.AppendLine("");
            textOut.AppendLine($"Color32s ({obj.colors32.Length}):");
            for (var i = 0; i < obj.colors32.Length; i++)
                textOut.AppendLine(obj.colors32[i].ToString());
            
            textOut.AppendLine("");
            textOut.AppendLine($"Triangles ({obj.triangles.Length}):");
            for (var i = 0; i < obj.triangles.Length; i++)
                textOut.AppendLine(obj.triangles[i].ToString());

            textOut.AppendLine("");
            textOut.AppendLine($"Tangents ({obj.tangents.Length}):");
            for (var i = 0; i < obj.tangents.Length; i++)
                textOut.AppendLine(obj.tangents[i].ToString());
            //
            // var path = AssetDatabase.GetAssetPath(obj);

            File.WriteAllText($"Assets/debugmesh.txt", textOut.ToString());
            AssetDatabase.Refresh();
        }
        
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
        //
        // [MenuItem("CONTEXT/SpriteAtlasImporter/Save as PNG")]
        // private static void AtlasTest(MenuCommand command)
        // {
        //     var obj = command.context as SpriteAtlasImporter;
        //     if (obj == null)
        //         return;
        //     
        //     
        //     var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(obj.assetPath);
        //     // var atlas = SpriteAtlasAsset.Load(obj.assetPath);
        //     if (atlas == null)
        //         return;
        //
        //     var s = new Sprite[atlas.spriteCount];
        //
        //     atlas.GetSprites(s);
        //     var tex = s[0].texture;
        //
        //     // var item atlas.sp
        //     
        //     var path = AssetDatabase.GetAssetPath(obj);
        //     var dir = Path.GetDirectoryName(path);
        //     var froot = Path.GetFileNameWithoutExtension(path);
        //
        //     File.WriteAllBytes(Path.Combine(dir, froot + ".png"), tex.EncodeToPNG());
        //     AssetDatabase.Refresh();
        // }

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
