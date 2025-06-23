using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace Assets.Scripts.Utility.Editor
{
	// Baking the Sprite Atlas preview image into a PNG file.
	public static partial class CustomContextMenu
	{
		[MenuItem("Assets/Atlas to PNG", true)]
		static bool _BakeAtlasesValidate() => Selection.activeObject is SpriteAtlas;
		[MenuItem("Assets/Atlas to PNG")]
		static void _BakeAtlases()
		{
			Debug.Log($"Start: Atlas to PNG");
			var type_SpriteAtlasExtensions = typeof(UnityEditor.U2D.SpriteAtlasExtensions);
			var m_GetPreviewTextures = type_SpriteAtlasExtensions.GetMethod("GetPreviewTextures", BindingFlags.Static | BindingFlags.NonPublic);
			if (m_GetPreviewTextures == null)
			{
				Debug.LogError("Failed to get UnityEditor.U2D.SpriteAtlasExtensions");
				return;
			}
			foreach (var obj in Selection.objects)
			{
				var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(obj));
				Debug.Log("Try exporting selected atlas: " + obj);
				if (obj is not SpriteAtlas atlas)
					continue;
				var textures = m_GetPreviewTextures.Invoke(null, new object[] { atlas }) as Texture2D[];
				if (textures == null)
				{
					Debug.LogError("Failed to get texture results");
					continue;
				}
				foreach (var texture in textures)
				{
					var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, $"{obj.name}.png"));
					var new_tex = _DuplicateTexture(texture);
					File.WriteAllBytes(path, new_tex.EncodeToPNG());
					AssetDatabase.Refresh();
					var importer = AssetImporter.GetAtPath(path) as TextureImporter;
					importer.alphaIsTransparency = true;
					importer.SaveAndReimport();
					Debug.Log("Saved texture to " + path);
				}
			}
		}

		static Texture2D _DuplicateTexture(Texture2D source)
		{
			var render_tex = RenderTexture.GetTemporary(
				source.width,
				source.height,
				0,
				RenderTextureFormat.ARGB32,
				RenderTextureReadWrite.sRGB);
			Graphics.Blit(source, render_tex);
			var previous = RenderTexture.active;
			RenderTexture.active = render_tex;
			var result = new Texture2D(source.width, source.height);
			result.ReadPixels(new Rect(0, 0, render_tex.width, render_tex.height), 0, 0);
			result.Apply();
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(render_tex);
			return result;
		}
	}
}