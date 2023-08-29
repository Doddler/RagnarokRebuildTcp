using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using B83.Image.BMP;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Debug = UnityEngine.Debug;

namespace Assets.Scripts.MapEditor.Editor
{
    class RagnarokMapLoader
    {
        private FileStream fs;
        private BinaryReader br;

        private string name;
        private int version;
        private int width;
        private int height;

        private int realWidth;
        private int realHeight;

        private float zoom;

        private string[] textures;

        private Tile[] tiles;
        private Cell[] cells;

        private Texture2D[] textureData;

        private Texture2D lightmapAtlas;
        private Dictionary<int, Vector2Int> lightmapPositions;
        private Texture2D lightmapMapTexture;
        private int[] lightIds;


        private void ReadTextures(string basePath, string outPath)
        {
            var count = br.ReadInt32();
            var length = br.ReadInt32();

            textures = new string[count];
            textureData = new Texture2D[count];

            for (var i = 0; i < count; i++)
            {
                var bytes = br.ReadBytes(length);
                var tex = Encoding.GetEncoding(949).GetString(bytes);
                var nulladdr = tex.IndexOf('\0');
                textures[i] = tex.Substring(0, nulladdr);

                var texout = TextureImportHelper.GetOrImportTextureToProject(textures[i], basePath, outPath);

                //var texPath = Path.Combine(basePath, "texture", textures[i]);

                ////Debug.Log(texPath);

                //if (File.Exists(texPath))
                //{
                //    var bpath = Path.GetDirectoryName(textures[i]);
                //    var fname = Path.GetFileNameWithoutExtension(textures[i]);
                //    var texOutPath = Path.Combine(outPath, "texture", bpath);
                //    var pngPath = Path.Combine(texOutPath, fname + ".png");

                //    if (!File.Exists(pngPath))
                //    {
                //        var tex2D = LoadTexture(texPath);

                //        tex2D.name = textures[i];

                //        PathHelper.CreateDirectoryIfNotExists(texOutPath);

                //        File.WriteAllBytes(pngPath, tex2D.EncodeToPNG());

                //        AssetDatabase.Refresh();
                //    }

                //    var texout = AssetDatabase.LoadAssetAtPath(pngPath, typeof(Texture2D)) as Texture2D;

                //    //Debug.Log("Our texture is currently: " + tex2D);

                textures[i] = Path.GetFileNameWithoutExtension(texout.name);
                textureData[i] = texout;
                //}
                //Debug.Log(textures[i]);
            }
        }

        private Color Decode(Span<byte> bytes, int idx)
        {
            //var baseIdx = pos 

            return Color.white;
        }

        private void ReadLightmaps()
        {
            var count = br.ReadInt32();
            var perCellX = br.ReadInt32();
            var perCellY = br.ReadInt32();
            var sizeCell = br.ReadInt32();
            var perCell = perCellX * perCellY * sizeCell;


            var width = Mathf.RoundToInt(Mathf.Sqrt(count));
            var height = Mathf.CeilToInt(Mathf.Sqrt(count));
            var w2 = (int)Mathf.Pow(2, Mathf.CeilToInt(Mathf.Log(width * 8) / Mathf.Log(2)));
            var h2 = (int)Mathf.Pow(2, Mathf.CeilToInt(Mathf.Log(height * 8) / Mathf.Log(2)));

            var bOut = new byte[w2 * h2 * 4];
            lightmapPositions = new Dictionary<int, Vector2Int>();

            Debug.Log($"perCellX:{perCellX} perCellY:{perCellY} sizeCell:{sizeCell} perCell:{perCell} width:{width} height:{height} w2:{w2} h2:{h2} count:{count} perCell:{perCell} bOut.Length:{bOut.Length}");
            
            for (var i = 0; i < count; i++)
            {
                var intensity = br.ReadBytes(perCell);
                var specular = br.ReadBytes(perCell * 3);

                var pos = i * perCell;
                var x = (i % width) * 8;
                var y = (i / width) * 8;

                lightmapPositions.Add(i, new Vector2Int(x, y));

                for (var x2 = 0; x2 < 8; x2++)
                {
                    for (var y2 = 0; y2 < 8; y2++)
                    {
                        var idx = ((x + x2) + (y + y2) * w2) * 4;

                        //Debug.Log("idx:" + idx + " of " + bOut.Length);
                        //Debug.Log("pos:" + (pos + perCell + (x2 + y2 * 8) * 3 + 0) + " of " + bytes.Length);
                        //Debug.Log($"i:{i} pos:{pos} x:{x} y:{y} x2:{x2} y2:{y2}");

                        //rgba
                        bOut[idx + 0] = (byte)(specular[(x2 + y2 * 8) * 3 + 0] );
                        bOut[idx + 1] = (byte)(specular[(x2 + y2 * 8) * 3 + 1] );
                        bOut[idx + 2] = (byte)(specular[(x2 + y2 * 8) * 3 + 2] );
                        bOut[idx + 3] = (byte)(intensity[(x2 + y2 * 8)] );
                        //bOut[idx + 3] = (byte)(bytes[pos + (x2 + y2 * 8)]);
                    }
                }
            }
            
            var texture = new Texture2D(w2, h2, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(bOut);
            texture.Apply();

            lightmapAtlas = texture;

            //File.WriteAllBytes($"F:\\Temp\\lightmap\\lmsrc.png", texture.EncodeToPNG());

            //var data = br.ReadBytes(count * perCell * 4);
        }
        
        private void ParseTiles()
        {
            var count = br.ReadInt32();
            tiles = new Tile[count];
            lightIds = new int[count];
            
            // var firstTile = "";
            // var firstUVMin = new Vector2(0, 0);
            // var firstUVMax = new Vector2(0, 0);

            var unlitCount = 0;

            // var lightIdList = new List<int>();

            for (var i = 0; i < count; i++)
            {
                var tile = new Tile();
                var us = br.ReadVector4();
                var vs = br.ReadVector4();

                tile.UVs = new Vector2[4];

                tile.UVs[2] = new Vector2(us[0], 1 - vs[0]);
                tile.UVs[3] = new Vector2(us[1], 1 - vs[1]);
                tile.UVs[0] = new Vector2(us[2], 1 - vs[2]);
                tile.UVs[1] = new Vector2(us[3], 1 - vs[3]);

                var texId = br.ReadInt16();
                var lightId = br.ReadInt16();
                
                tile.Texture = textures[texId];
                tile.Color = br.ReadByteColor();
                lightIds[i] = lightId;

                //tile.IsUnlit = lightId <= 0;

                if (tile.Texture.ToLower() == "black" || tile.Texture.ToLower() == "backside")
                {
                    tile.IsUnlit = true;
                    // if(firstTile != tile.Texture)
                    //     unlitCount++;
                }

                var uvMin = new Vector2(1f, 1f);
                var uvMax = new Vector2(0f, 0f);

                for (var j = 0; j < 4; j++)
                {
                    uvMin = Vector2.Min(uvMin, tile.UVs[j]);
                    uvMax = Vector2.Max(uvMax, tile.UVs[j]);
                }
                //
                // if (i == 0)
                // {
                //     firstTile = tile.Texture;
                //     firstUVMin = uvMin;
                //     firstUVMax = uvMax;
                //     Debug.Log($"Using {firstTile} {uvMin} {uvMax} as unlit tile for map.");
                // }

                //what the everliving nonsense it this shit put it in a file or something god
                //
                if ((tile.Texture == "시계탑던전03" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f)) //clock tower basement
                    //clocktower topside
                    || (tile.Texture == "gp-lostdun_g03" && uvMin == new Vector2(0.75f, 0f) && uvMax == new Vector2(1f, 0.25f))
                    //geffen tower
                    || (tile.Texture == "수도원언덕1" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f))
                    || (tile.Texture == "수도원언덕1" && uvMin == new Vector2(0.25f, 0.75f) && uvMax == new Vector2(0.5f, 1f))
                    || (tile.Texture == "수도원언덕1" && uvMin == new Vector2(0.5f, 0.75f) && uvMax == new Vector2(0.75f, 1f))
                    || (tile.Texture == "수도원언덕1" && uvMin == new Vector2(0.75f, 0.75f) && uvMax == new Vector2(1f, 1f))
                    || (tile.Texture == "수도원언덕3" && uvMin == new Vector2(0f, 0f) && uvMax == new Vector2(0.25f, 0.25f))
                    || (tile.Texture == "수도원언덕3" && uvMin == new Vector2(0f, 0.5f) && uvMax == new Vector2(0.25f, 0.75f))
                    || (tile.Texture == "수도원언덕3" && uvMin == new Vector2(0f, 0.25f) && uvMax == new Vector2(0.25f, 0.5f))
                    || (tile.Texture == "수도원언덕3" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f))
                    || (tile.Texture == "수도원언덕3" && uvMin == new Vector2(0.25f, 0.75f) && uvMax == new Vector2(0.5f, 1f))
                    || (tile.Texture == "수도원언덕3" && uvMin == new Vector2(0.5f, 0.75f) && uvMax == new Vector2(0.75f, 1f))
                    || (tile.Texture == "수도원언덕3" && uvMin == new Vector2(0.75f, 0.75f) && uvMax == new Vector2(1f, 1f))
                    //prontera church
                    || (tile.Texture == "sgid2-side2" && uvMin == new Vector2(0f, 0f) && uvMax == new Vector2(1f, 1f))

                    //guild castles
                    || (tile.Texture == "sgid3-side1" && uvMin == new Vector2(0f, 0f) && uvMax == new Vector2(1f, 1f))
                    || (tile.Texture == "sage_f002" && uvMin == new Vector2(0.8333333f, 0.8333333f) && uvMax == new Vector2(1f, 1f))

                    //comodo beach dungeon
                    || (tile.Texture == "hot-3" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f))
                    || (tile.Texture == "hot-3" && uvMin == new Vector2(0f, 0f) && uvMax == new Vector2(0.25f, 0.25f))
                    || (tile.Texture == "hot-3" && uvMin == new Vector2(0.25f, 0.5f) && uvMax == new Vector2(0.5f, 0.75f))
                    || (tile.Texture == "hot-3" && uvMin == new Vector2(0.5f, 0.75f) && uvMax == new Vector2(0.75f, 1f))
                    || (tile.Texture == "hot-3" && uvMin == new Vector2(0.75f, 0.75f) && uvMax == new Vector2(1f, 1f))

                    || (tile.Texture == "hot-2" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f))


                    || (tile.Texture == "hot-8" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f))
                    || (tile.Texture == "hot-8" && uvMin == new Vector2(0.25f, 0.75f) && uvMax == new Vector2(0.5f, 1f))
                    || (tile.Texture == "hot-8" && uvMin == new Vector2(0.75f, 0.75f) && uvMax == new Vector2(1f, 1f))
                    || (tile.Texture == "hot-8" && uvMin == new Vector2(0.5f, 0.75f) && uvMax == new Vector2(0.75f, 1f))


                    || (tile.Texture == "hot-9" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f))
                    || (tile.Texture == "hot-9" && uvMin == new Vector2(0.25f, 0.75f) && uvMax == new Vector2(0.5f, 1f))
                    || (tile.Texture == "hot-9" && uvMin == new Vector2(0.5f, 0.75f) && uvMax == new Vector2(0.75f, 1f))
                    || (tile.Texture == "hot-9" && uvMin == new Vector2(0.75f, 0.75f) && uvMax == new Vector2(1f, 1f))

                    || (tile.Texture == "hot-10" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f))
                    || (tile.Texture == "hot-10" && uvMin == new Vector2(0.25f, 0.75f) && uvMax == new Vector2(0.5f, 1f))
                    || (tile.Texture == "hot-10" && uvMin == new Vector2(0.5f, 0.75f) && uvMax == new Vector2(0.75f, 1f))
                    || (tile.Texture == "hot-10" && uvMin == new Vector2(0.75f, 0.75f) && uvMax == new Vector2(1f, 1f))

                    || (tile.Texture == "hot-1" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f))
                    || (tile.Texture == "hot-1" && uvMin == new Vector2(0.25f, 0.75f) && uvMax == new Vector2(0.5f, 1f))
                    || (tile.Texture == "hot-1" && uvMin == new Vector2(0.5f, 0.75f) && uvMax == new Vector2(0.75f, 1f))
                    || (tile.Texture == "hot-1" && uvMin == new Vector2(0.75f, 0.75f) && uvMax == new Vector2(1f, 1f))


                    ////izlude
                    //|| (tile.Texture == "iz-03" && uvMin == new Vector2(0f, 0.5f) && uvMax == new Vector2(0.25f, 0.75f))
                    //|| (tile.Texture == "iz-03" && uvMin == new Vector2(0f, 0.75f) && uvMax == new Vector2(0.25f, 1f))
                    //|| (tile.Texture == "iz-03" && uvMin == new Vector2(0.25f, 0.75f) && uvMax == new Vector2(0.5f, 1f))
                    //|| (tile.Texture == "iz-03" && uvMin == new Vector2(0.5f, 0.75f) && uvMax == new Vector2(0.75f, 1f))
                   )
                {
                    tile.IsUnlit = true;
                    unlitCount++;
                }
                //
                // if (tile.Texture == firstTile && uvMin == firstUVMin && uvMax == firstUVMax)
                // {
                //     tile.IsUnlit = true;
                //     unlitCount++;
                // }

                //if (i == 0 || lightId == 0)
                //{
                //    tile.IsUnlit = true;
                //    Debug.Log($"First tile: {tile.Texture} {tile.UVs[0]} {tile.UVs[1]} {tile.UVs[2]} {tile.UVs[3]} {lightId}");
                //}

                tiles[i] = tile;
            }

            Debug.Log($"Painted {unlitCount} tiles unlit.");
        }

        private Vector2[] RightSwapUVs(Vector2[] uvs)
        {
            var ret = new Vector2[4];

            ret[1] = uvs[0];
            ret[3] = uvs[1];
            ret[0] = uvs[2];
            ret[2] = uvs[3];

            return ret;
        }

        private Vector2[] GetEmptyUVs()
        {
            var uvs = new List<Vector2>();
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            return uvs.ToArray();
        }

        private Tile GetDisabledTile()
        {
            return new Tile() { Enabled = false, Color = Color.gray, Texture = null, UVs = GetEmptyUVs() };
        }

        private void ParseSurfaces()
        {
            realWidth = (int)Math.Ceiling((width) / 16f) * 16;
            realHeight = (int)Math.Ceiling((height) / 16f) * 16;

            cells = new Cell[realWidth * realHeight];

            for (var i = 0; i < cells.Length; i++)
                cells[i] = new Cell() { Top = GetDisabledTile(), Front = GetDisabledTile(), Right = GetDisabledTile(), Heights = Vector4.zero };

            lightmapMapTexture = new Texture2D(width*8, height*8, TextureFormat.RGBA32, false);

            var pixels = lightmapMapTexture.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);
            lightmapMapTexture.SetPixels32(pixels);
            lightmapMapTexture.Apply();
            

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var v1 = br.ReadSingle();
                    var v2 = br.ReadSingle();
                    var v3 = br.ReadSingle();
                    var v4 = br.ReadSingle();

                    var cell = cells[x + y * realWidth];
                    cell.Heights = new Vector4(v3, v4, v1, v2) * -1;
                    var top = br.ReadInt32();
                    var front = br.ReadInt32();
                    var right = br.ReadInt32();

                    cell.Top = GetDisabledTile();
                    //cell.Front = GetDisabledTile();
                    cell.Right = GetDisabledTile();
                    
                    if (top != -1)
                    {
                        if (top >= tiles.Length)
                            Debug.LogError(
                                $"Cell is requesting top tile ID {top} but the list only goes to {tiles.Length}");
                        cell.Top = new Tile() { Enabled = true, Texture = tiles[top].Texture, UVs = tiles[top].UVs, Color = tiles[top].Color, IsUnlit = tiles[top].IsUnlit};
                        
                        var id = lightIds[top];

                        if (lightIds[top] >= 0)
                        {
                            var pos = lightmapPositions[id];
                            Graphics.CopyTexture(lightmapAtlas, 0, 0, pos.x, pos.y, 8, 8, lightmapMapTexture, 0, 0, x * 8, y * 8);
                        }

                    }

                    if (y < realHeight - 1)
                    {
                        if(x + (y + 1) * realWidth >= cells.Length)
                            Debug.LogError($"Whoa, looking out of bounds! X is {x}, Y is {y}, realwidth {realWidth} realheight {realHeight}");
                        var cell2 = cells[x + (y + 1) * realWidth]; //special case because our front isn't their front...
                        if (front != -1)
                            cell2.Front = new Tile()
                            {
                                Enabled = true,
                                Texture = tiles[front].Texture,
                                UVs = tiles[front].UVs,
                                Color = tiles[front].Color,
                                IsUnlit = tiles[front].IsUnlit
                            };
                        else
                            cell2.Front = GetDisabledTile();
                    }

                    if (right != -1)
                        cell.Right = new Tile() { Enabled = true, Texture = tiles[right].Texture, UVs = RightSwapUVs(tiles[right].UVs), Color = tiles[right].Color, IsUnlit = tiles[right].IsUnlit};

                    //Debug.Log($"Load tile {cell.Heights} {top} {front} {right}");

                    if (x + y * realWidth > realWidth * realHeight - 1)
                        Debug.Log($"{x} {y} {height} {width} {realHeight} {realWidth}");

                    cells[x + y * realWidth] = cell;
                }
            }

            //lightmapMapTexture.Apply();
            //File.WriteAllBytes($"F:\\Temp\\lightmap\\{name}.png", lightmapMapTexture.EncodeToPNG());
        }


        public RoMapData ImportMap(string path, string outPath)
        {
            var filename = path;
            var basename = Path.GetFileNameWithoutExtension(filename);
            var basedir = Path.GetDirectoryName(path);

            name = basename;

            fs = new FileStream(filename, FileMode.Open);
            br = new BinaryReader(fs);

            var header = new string(br.ReadChars(4));
            if (header != "GRGN")
                throw new Exception("Not map");

            var majorVersion = br.ReadByte();
            var minorVersion = br.ReadByte();
            version = majorVersion * 10 + minorVersion;

            Debug.Log("Map version: " + version);

            width = br.ReadInt32();
            height = br.ReadInt32();
            zoom = br.ReadSingle();

            ReadTextures(basedir, outPath);
            ReadLightmaps();

            ParseTiles();
            ParseSurfaces();


            Debug.Log($"Finished {fs.Position} of {fs.Length}");

            fs.Close();
            fs.Dispose();


            var mapData = ScriptableObject.CreateInstance<RoMapData>();
            mapData.InitialSize = new Vector2Int(width, height);
            mapData.name = basename;
            mapData.Width = realWidth;
            mapData.Height = realHeight;
            mapData.Textures = textureData.ToList();

            mapData.Water = new MapWater();

            mapData.SaveCellDataToFile(cells, Path.Combine(outPath, "mapinfo", basename + ".bytes").Replace("\\", "/"));

            //ctx.AddObjectToAsset(basename, mapData);
            //ctx.SetMainObject(mapData);

            return mapData;
        }
    }
}
