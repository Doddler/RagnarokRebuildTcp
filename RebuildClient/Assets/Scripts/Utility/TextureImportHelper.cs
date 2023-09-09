using System;
using System.Collections.Generic;
using System.IO;
using B83.Image.BMP;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace Assets.Scripts
{

    public class TextureImportHelper
    {
        public static void SetTexturesReadable(List<Texture2D> textures)
        {
            var hasChanges = false;
            foreach (var t in textures)
            {
                if (!t.isReadable)
                {
                    var texPath = AssetDatabase.GetAssetPath(t);
                    var tImporter = AssetImporter.GetAtPath(texPath) as TextureImporter;
                    if (tImporter != null)
                    {
                        tImporter.isReadable = true;

                        AssetDatabase.ImportAsset(texPath);
                        hasChanges = true;
                    }
                }
            }

            if(hasChanges)
                AssetDatabase.Refresh();
        }

        public static Texture2D SaveAndUpdateTexture(Texture2D texture, string outputPath, Action<TextureImporter> callback = null)
        {

	        var bytes = texture.EncodeToPNG();
	        File.WriteAllBytes(outputPath, bytes);

	        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
	        AssetDatabase.Refresh();

	        TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(outputPath);
	        importer.textureType = TextureImporterType.Default;
	        importer.npotScale = TextureImporterNPOTScale.None;
	        importer.textureCompression = TextureImporterCompression.Compressed;
	        importer.crunchedCompression = true;
	        importer.compressionQuality = 50;
	        importer.wrapMode = TextureWrapMode.Clamp;
	        importer.isReadable = false;
	        importer.mipmapEnabled = false;
	        importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;

            if (callback != null)
                callback(importer);

	        importer.SaveAndReimport();
            
	        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);

            return texture;
        }
        
        public static Texture2D GetOrImportTextureToProject(string textureName, string importPath, string outputPath)
        {

            var texPath = Path.Combine(importPath, "texture", textureName);

            if (!File.Exists(texPath))
                texPath = Path.Combine(importPath, textureName);

            if (!File.Exists(texPath))
                texPath = importPath;

            //Debug.Log(texPath);

            if (File.Exists(texPath))
            {
                var bpath = Path.GetDirectoryName(textureName);
                var fname = Path.GetFileNameWithoutExtension(textureName);
                var texOutPath = Path.Combine(outputPath, DirectoryHelper.GetRelativeDirectory(importPath, Path.GetDirectoryName(texPath)));
                var pngPath = Path.Combine(texOutPath, fname + ".png");
                
                if (!File.Exists(pngPath))
                {
                    var tex2D = LoadTexture(texPath);

                    tex2D.name = textureName;

                    PathHelper.CreateDirectoryIfNotExists(texOutPath);

                    File.WriteAllBytes(pngPath, tex2D.EncodeToPNG());

                    //Debug.Log("Png file does not exist: " + pngPath);

                    AssetDatabase.Refresh();
                }

                var texout = AssetDatabase.LoadAssetAtPath(pngPath, typeof(Texture2D)) as Texture2D;

                return texout;
            }

            throw new Exception($"Could not find texture {textureName} in the import path {importPath}");
        }

        public static Texture2D LoadTexture(string path)
        {
            if (Path.GetExtension(path).ToLower() == ".tga")
            {
                return TGALoader.LoadTGA(path);
            }

            var bmp = new BMPLoader();
            bmp.ForceAlphaReadWhenPossible = false;
            var img = bmp.LoadBMP(path);

            if (img == null)
                throw new Exception("Failed to load: " + path);

            var colors = (Color32[])img.imageData.Clone();

            var width = img.info.width;
            var height = img.info.height;

            //magic pink conversion and transparent color expansion
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var count = 0;
                    var r = 0;
                    var g = 0;
                    var b = 0;

                    if (x + y * width >= colors.Length)
                        Debug.LogWarning($"For some reason looking out of bounds on color table on texture {path} w{width} h{height} position {x} {y} ({x + y * width}");
                    var color = colors[x + y * width];
                    //Debug.Log(color);
                    if (color.r < 254 || color.g != 0 || color.b < 254)
                        continue;

                    //Debug.Log("OHWOW: " + color);

                    for (var y2 = -1; y2 <= 1; y2++)
                    {
                        for (var x2 = -1; x2 <= 1; x2++)
                        {
                            if (y + y2 < 0 || y + y2 >= height)
                                continue;
                            if (x + x2 < 0 || x + x2 >= width)
                                continue;

                            var color2 = colors[x + x2 + (y + y2) * width];

                            if (color2.r >= 254 && color2.g == 0 && color2.b >= 254)
                                continue;

                            count++;

                            r += color2.r;
                            g += color2.g;
                            b += color2.b;
                        }
                    }

                    if (count > 0)
                    {
                        var r2 = (byte)Mathf.Clamp(r / count, 0, 255);
                        var g2 = (byte)Mathf.Clamp(g / count, 0, 255);
                        var b2 = (byte)Mathf.Clamp(b / count, 0, 255);

                        //Debug.Log($"{x},{y} - change {color} to {r2},{g2},{b2}");

                        img.imageData[x + y * width] = new Color32(r2, g2, b2, 0);
                    }
                    else
                        img.imageData[x + y * width] = new Color32(0, 0, 0, 0);
                }
            }

            return img.ToTexture2D();
        }


        public static void PatchAtlasEdges(Texture2D atlas, Rect[] rects)
        {
            foreach (var r in rects)
            {
                var xMin = Mathf.RoundToInt(Mathf.Lerp(0, atlas.width, r.x));
                var xMax = Mathf.RoundToInt(Mathf.Lerp(0, atlas.width, r.x + r.width));
                var yMin = Mathf.RoundToInt(Mathf.Lerp(0, atlas.height, r.y));
                var yMax = Mathf.RoundToInt(Mathf.Lerp(0, atlas.height, r.y + r.height));

                //bottom left
                if (xMin > 0 && yMin > 0)
                    atlas.SetPixel(xMin - 1, yMin - 1, atlas.GetPixel(xMin, yMin));

                //top left
                if (xMin > 0 && yMax < atlas.height)
                    atlas.SetPixel(xMin - 1, yMax, atlas.GetPixel(xMin, yMax - 1));

                //bottom right
                if (xMax < atlas.width && yMin > 0)
                    atlas.SetPixel(xMax, yMin - 1, atlas.GetPixel(xMax - 1, yMin));

                //top right
                if (xMax < atlas.width && yMax < atlas.height)
                    atlas.SetPixel(xMax, yMax, atlas.GetPixel(xMax - 1, yMax - 1));

                //left edge
                if (xMin > 0)
                {
                    var colors = atlas.GetPixels(xMin, yMin, 1, yMax - yMin);
                    atlas.SetPixels(xMin - 1, yMin, 1, yMax - yMin, colors);
                }

                //right edge
                if (xMax < atlas.width)
                {
                    var colors = atlas.GetPixels(xMax - 1, yMin, 1, yMax - yMin);
                    atlas.SetPixels(xMax, yMin, 1, yMax - yMin, colors);
                }

                //bottom edge
                if (yMin > 0)
                {
                    var colors = atlas.GetPixels(xMin, yMin, xMax - xMin, 1);
                    atlas.SetPixels(xMin, yMin - 1, xMax - xMin, 1, colors);
                }

                //top edge
                if (yMax < atlas.height)
                {
                    var colors = atlas.GetPixels(xMin, yMax - 1, xMax - xMin, 1);
                    atlas.SetPixels(xMin, yMax, xMax - xMin, 1, colors);
                }
            }
        }
    }
}

#endif
