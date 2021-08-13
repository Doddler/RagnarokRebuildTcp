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
using Debug = UnityEngine.Debug;

namespace Assets.Scripts.MapEditor.Editor
{
    class RagnarokMapLoader
    {
        private FileStream fs;
        private BinaryReader br;

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

        private void ReadLightmaps()
        {
            var count = br.ReadInt32();
            var perCellX = br.ReadInt32();
            var perCellY = br.ReadInt32();
            var sizeCell = br.ReadInt32();
            var perCell = perCellX * perCellY * sizeCell;

            var data = br.ReadBytes(count * perCell * 4);
        }

        private void ParseTiles()
        {
            var count = br.ReadInt32();
            tiles = new Tile[count];

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

                tiles[i] = tile;
            }
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
                        cell.Top = new Tile() { Enabled = true, Texture = tiles[top].Texture, UVs = tiles[top].UVs, Color = tiles[top].Color };
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
                                Color = tiles[front].Color
                            };
                        else
                            cell2.Front = GetDisabledTile();
                    }

                    if (right != -1)
                        cell.Right = new Tile() { Enabled = true, Texture = tiles[right].Texture, UVs = RightSwapUVs(tiles[right].UVs), Color = tiles[right].Color };

                    //Debug.Log($"Load tile {cell.Heights} {top} {front} {right}");

                    if (x + y * realWidth > realWidth * realHeight - 1)
                        Debug.Log($"{x} {y} {height} {width} {realHeight} {realWidth}");

                    cells[x + y * realWidth] = cell;
                }
            }
        }


        public RoMapData ImportMap(string path, string outPath)
        {
            var filename = path;
            var basename = Path.GetFileNameWithoutExtension(filename);
            var basedir = Path.GetDirectoryName(path);

            fs = new FileStream(filename, FileMode.Open);
            br = new BinaryReader(fs);

            var header = new string(br.ReadChars(4));
            if (header != "GRGN")
                throw new Exception("Not map");

            var majorVersion = br.ReadByte();
            var minorVersion = br.ReadByte();
            version = majorVersion * 10 + minorVersion;

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

            mapData.SaveCellDataToFile(cells, Path.Combine(outPath, "mapinfo", basename + ".bytes").Replace("\\", "/"));

            //ctx.AddObjectToAsset(basename, mapData);
            //ctx.SetMainObject(mapData);

            return mapData;
        }
    }
}
