using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using B83.Image.BMP;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace Assets.Scripts
{

    public class TextureImportHelper
    {
        private static readonly Dictionary<string, bool> FileSystemCaseSensitivityCache =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public static void SetTexturesReadable(List<Texture2D> textures)
        {
            var hasChanges = false;
            foreach (var t in textures)
            {
                if (!t.isReadable)
                {
                    var texturePath = AssetDatabase.GetAssetPath(t);
                    var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (textureImporter != null)
                    {
                        textureImporter.isReadable = true;

                        AssetDatabase.ImportAsset(texturePath);
                        hasChanges = true;
                    }
                }
            }

            if(hasChanges)
                AssetDatabase.Refresh();
        }

        public static Texture2D SaveAndUpdateTexture(Texture2D texture, string outputPath, Action<TextureImporter> callback = null, bool forceUpdate = true)
        {
            outputPath = outputPath.Replace("\\", "/");
            var dir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

	        var bytes = texture.EncodeToPNG();
	        File.WriteAllBytes(outputPath, bytes);

            if(forceUpdate)
	            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
	        AssetDatabase.Refresh();

	        TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(outputPath);
            importer.textureType = TextureImporterType.Default;
	        importer.npotScale = TextureImporterNPOTScale.None;
	        importer.textureCompression = TextureImporterCompression.CompressedHQ;
	        importer.crunchedCompression = false;
	        importer.compressionQuality = 50;
            importer.wrapMode = TextureWrapMode.Clamp;
	        importer.isReadable = false;
	        importer.mipmapEnabled = false;
	        importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            if (callback != null)
                callback(importer);

	        importer.SaveAndReimport();
            
	        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);

            return texture;
        }
        
        private static string NormalizeTextureRelativePath(string textureName)
        {
            return textureName
                .TrimStart('\\', '/')
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string ResolvePathCaseInsensitive(string candidatePath)
        {
            candidatePath = candidatePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            if (File.Exists(candidatePath) || Directory.Exists(candidatePath))
                return candidatePath;

            if (!IsFileSystemCaseSensitive(candidatePath))
                return candidatePath;

            var root = Path.GetPathRoot(candidatePath);
            var current = string.IsNullOrEmpty(root) ? Directory.GetCurrentDirectory() : root;
            var remainder = string.IsNullOrEmpty(root) ? candidatePath : candidatePath.Substring(root.Length);

            foreach (var pathPart in remainder.Split(Path.DirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(pathPart))
                    continue;

                if (!Directory.Exists(current))
                    return candidatePath;

                var exact = Path.Combine(current, pathPart);
                if (File.Exists(exact) || Directory.Exists(exact))
                {
                    current = exact;
                    continue;
                }

                string matchingEntry = null;
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    if (string.Equals(Path.GetFileName(entry), pathPart, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingEntry = entry;
                        break;
                    }
                }

                if (matchingEntry == null)
                    return candidatePath;

                current = matchingEntry;
            }

            return current;
        }

        private static bool IsFileSystemCaseSensitive(string candidatePath)
        {
            var probeDirectory = GetCaseSensitivityProbeDirectory(candidatePath);
            if (string.IsNullOrWhiteSpace(probeDirectory))
                return true;

            var cacheKey = Path.GetFullPath(probeDirectory);

            bool cachedIsCaseSensitive;
            if (FileSystemCaseSensitivityCache.TryGetValue(cacheKey, out cachedIsCaseSensitive))
                return cachedIsCaseSensitive;

            var probeName = ".case-sensitivity-probe-" + Guid.NewGuid().ToString("N");
            var lowerProbePath = Path.Combine(probeDirectory, probeName.ToLowerInvariant());
            var upperProbePath = Path.Combine(probeDirectory, probeName.ToUpperInvariant());

            try
            {
                File.WriteAllText(lowerProbePath, string.Empty);
                var isCaseSensitive = !File.Exists(upperProbePath);
                FileSystemCaseSensitivityCache[cacheKey] = isCaseSensitive;
                return isCaseSensitive;
            }
            catch
            {
                return true;
            }
            finally
            {
                try
                {
                    if (File.Exists(lowerProbePath))
                        File.Delete(lowerProbePath);

                    if (File.Exists(upperProbePath))
                        File.Delete(upperProbePath);
                }
                catch
                {
                    // Best-effort cleanup only.
                }
            }
        }

        private static string GetCaseSensitivityProbeDirectory(string candidatePath)
        {
            var current = Directory.Exists(candidatePath)
                ? candidatePath
                : Path.GetDirectoryName(candidatePath);

            while (!string.IsNullOrWhiteSpace(current))
            {
                if (Directory.Exists(current))
                    return current;

                current = Path.GetDirectoryName(current);
            }

            return Directory.Exists(Application.temporaryCachePath)
                ? Application.temporaryCachePath
                : null;
        }

        private static string ResolveTexturePath(string textureName, string importPath)
        {
            var normalizedTextureName = NormalizeTextureRelativePath(textureName);

            var candidates = new[]
            {
                Path.Combine(importPath, "texture", normalizedTextureName),
                Path.Combine(importPath, normalizedTextureName),
                importPath
            };

            foreach (var candidate in candidates)
            {
                var resolved = ResolvePathCaseInsensitive(candidate);
                if (File.Exists(resolved))
                    return resolved;
            }

            return candidates[0];
        }

        private static string ToProjectRelativeAssetPath(string path)
        {
            path = path.Replace('\\', '/');

            var assetsIndex = path.IndexOf("Assets/", StringComparison.Ordinal);
            path = assetsIndex >= 0 ? path.Substring(assetsIndex) : path;

            if (path.Equals("Assets/Maps", StringComparison.OrdinalIgnoreCase))
                return "Assets/Maps";

            if (path.StartsWith("Assets/Maps/", StringComparison.OrdinalIgnoreCase))
                path = "Assets/Maps/" + path.Substring("Assets/Maps/".Length);

            if (path.StartsWith("Assets/Maps/Texture/", StringComparison.OrdinalIgnoreCase))
                path = "Assets/Maps/Texture/" + path.Substring("Assets/Maps/Texture/".Length);

            return path;
        }

        public static Texture2D GetOrImportTextureToProject(string textureName, string importPath, string outputPath, bool keyOnBlack = false)
        {

            var texturePath = ResolveTexturePath(textureName, importPath);

            //Debug.Log(texturePath);

            if (File.Exists(texturePath))
            {
                var textureBaseName = Path.GetFileNameWithoutExtension(NormalizeTextureRelativePath(textureName));
                var textureOutputDirectory = Path.Combine(outputPath, DirectoryHelper.GetRelativeDirectory(importPath, Path.GetDirectoryName(texturePath)));
                var pngPath = ToProjectRelativeAssetPath(Path.Combine(textureOutputDirectory, textureBaseName + ".png"));
                
                if (!File.Exists(pngPath))
                {
                    var texture = LoadTexture(texturePath, keyOnBlack);

                    try
                    {
                        texture.name = Path.GetFileNameWithoutExtension(texturePath);

                        PathHelper.CreateDirectoryIfNotExists(Path.GetDirectoryName(pngPath));

                        File.WriteAllBytes(pngPath, texture.EncodeToPNG());

                        //Debug.Log("Png file does not exist: " + pngPath);
                    }
                    finally
                    {
                        if (texture != null)
                            UnityEngine.Object.DestroyImmediate(texture);
                    }
                }

                var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                if (importedTexture == null)
                {
                    AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                }

                if (importedTexture == null)
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                }

                if (importedTexture == null)
                {
                    Debug.LogWarning($"Imported texture {textureName} from {texturePath} to {pngPath}, but Unity could not load it as Texture2D. Falling back to readable source texture for atlas packing.");
                    var fallbackTexture = LoadTexture(texturePath, keyOnBlack);
                    fallbackTexture.name = Path.GetFileNameWithoutExtension(texturePath);
                    return fallbackTexture;
                }

                return importedTexture;
            }

            throw new Exception($"Could not find texture {textureName} in the import path {importPath}");
        }

        public static Texture2D FixPinkColor(Texture2D tex)
        {
            var colors = tex.GetPixels32();
            var width = tex.width;
            var height = tex.height;
            var colorsOut = new Color32[colors.Length];
            
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
                        Debug.LogWarning($"For some reason looking out of bounds on color table on texture {tex} w{width} h{height} position {x} {y} ({x + y * width}");
                    var color = colors[x + y * width];
                    var posterized = new Color32((byte)(color.r & 0xF0), (byte)(color.g & 0xF0), (byte)(color.b & 0xF0), color.a);
                    //Debug.Log(color);
                    if (posterized.r < 254 || posterized.g > 4 || posterized.b < 254)
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

                        colorsOut[x + y * width] = new Color32(r2, g2, b2, 0);
                    }
                    else
                        colorsOut[x + y * width] = new Color32(0, 0, 0, 0);
                }
            }


            var texOut = new Texture2D(width, height, TextureFormat.RGBA32, true);
            texOut.SetPixels32(colorsOut);
            texOut.Apply(true, true);
            return texOut;
        }

        public static Texture2D LoadTexture(string path, bool keyOnBlack = false)
        {
            var keyColor = new Color32(255, 0, 255, 0);
            var posterizedMask = new Color32((byte)(keyColor.r & 0xF0), (byte)(keyColor.g & 0xF0), (byte)(keyColor.b & 0xF0), keyColor.a);
            
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
                    //var count = 0;
                    //var r = 0;
                    //var g = 0;
                    //var b = 0;

                    if (x + y * width >= colors.Length)
                        Debug.LogWarning($"For some reason looking out of bounds on color table on texture {path} w{width} h{height} position {x} {y} ({x + y * width}");
                    var color = colors[x + y * width];
                    
                    var posterized = new Color32((byte)(color.r & 0xF0), (byte)(color.g & 0xF0), (byte)(color.b & 0xF0), color.a);

                    img.imageData[x + y * width] = color;
                    
                    if (posterized.r == posterizedMask.r && posterized.g == posterizedMask.g && posterized.b == posterizedMask.b)
                        img.imageData[x + y * width] = new Color32(0, 0, 0, 0);
                    
                    if(keyOnBlack && color.r == 0 && color.g == 0 && color.b == 0)
                        img.imageData[x + y * width] = new Color32(0, 0, 0, 0);
                    
                    //
                    // //
                    // //Debug.Log(color);
                    // if (posterized.r != keyColor.r || posterized.g != keyColor.g || posterized.b != keyColor.b)
                    // {
                    //     if(!keyOnBlack || color.r > 0 || color.g > 0 || color.b > 0)
                    //         continue;
                    // }

                    //Debug.Log("OHWOW: " + color);
                    //
                    // for (var y2 = -1; y2 <= 1; y2++)
                    // {
                    //     for (var x2 = -1; x2 <= 1; x2++)
                    //     {
                    //         if (y + y2 < 0 || y + y2 >= height)
                    //             continue;
                    //         if (x + x2 < 0 || x + x2 >= width)
                    //             continue;
                    //
                    //         var color2 = colors[x + x2 + (y + y2) * width];
                    //
                    //         var isKeyed = color2.r == keyColor.r && color2.g == keyColor.g && color2.b == keyColor.b;
                    //         if (keyOnBlack)
                    //             isKeyed = color2.r == 0 && color2.g == 0 && color2.b == 0;
                    //
                    //         if (isKeyed)
                    //             continue;
                    //         
                    //         count++;
                    //
                    //         r += color2.r;
                    //         g += color2.g;
                    //         b += color2.b;
                    //     }
                    // }
                    //
                    // if (count > 0)
                    // {
                    //     var r2 = (byte)Mathf.Clamp(r / count, 0, 255);
                    //     var g2 = (byte)Mathf.Clamp(g / count, 0, 255);
                    //     var b2 = (byte)Mathf.Clamp(b / count, 0, 255);
                    //
                    //     //Debug.Log($"{x},{y} - change {color} to {r2},{g2},{b2}");
                    //
                    //     img.imageData[x + y * width] = new Color32(r2, g2, b2, 0);
                    // }
                    // else
                        // img.imageData[x + y * width] = new Color32(0, 0, 0, 0);
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
        
        //code graciously from https://github.com/hanbim520/Unity2017AutoCreateSpriteAtlas
        public static void CreateAtlas(string atlasName, string sptDesDir)
        {
            string yaml = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!612988286 &1
SpriteAtlasAsset:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: 
  serializedVersion: 2
  m_MasterAtlas: {fileID: 0}
  m_ImporterData:
    packables: []
  m_IsVariant: 0
";
            AssetDatabase.Refresh();

            if (!Directory.Exists(sptDesDir ))
            {
                Directory.CreateDirectory(sptDesDir );
                AssetDatabase.Refresh();
            }
            string filePath = sptDesDir + "/" + atlasName;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                AssetDatabase.Refresh();
            }
            FileStream fs = new FileStream(filePath, FileMode.CreateNew);
            byte[] bytes = new UTF8Encoding().GetBytes(yaml);
            fs.Write(bytes, 0, bytes.Length);
            fs.Close();
            AssetDatabase.Refresh();
        }
    }
}

#endif
