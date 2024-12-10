using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    class RagnarokWalkableDataImporter
    {
        private FileStream fs;
        private BinaryReader br;

        private int version;

        private List<Texture2D> GetTextures()
        {
            var textures = new List<Texture2D>();
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/red.png"));
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/blue.png"));
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/green.png"));
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/yellow.png"));
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/orange.png"));
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/purple.png"));
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/aqua.png"));
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/black.png"));
            return textures;
        }

        private float GetPointHeight(Vector4 heights, Vector2 pos)
        {
            var top = heights[0] * (1 - pos.x) + heights[1] * pos.x;
            var bottom = heights[2] * (1 - pos.x) + heights[3] * pos.x;
            var left = heights[0] * (1 - pos.y) + heights[2] * pos.y;
            var right = heights[1] * (1 - pos.y) + heights[3] * pos.y;

            var xVal = left * (1 - pos.x) + right * pos.x;
            var yVal = top * (1 - pos.y) + bottom * pos.y;

            return (xVal + yVal) / 2f;
        }

        public RoMapData SplitWalkData(RoMapData src, int splitCount, string name)
        {
            if (splitCount < 2)
                throw new Exception("SplitWalkData must be split in at least 2 parts.");

            var map = ScriptableObject.CreateInstance<RoMapData>();
            map.Width = src.Width * splitCount;
            map.Height = src.Height * splitCount;
            map.InitialSize = src.InitialSize * splitCount;
            map.IsWalkTable = true;
            map.Textures = src.Textures;

            var cells = new Cell[map.Width * map.Height];
            var walkCells = new RoWalkCell[map.Width * map.Height];

            for (var y = 0; y < src.Height; y++)
            {
                for (var x = 0; x < src.Width; x++)
                {
                    var cell = src.Cell(x, y);
                    var heights = cell.Heights;

                    for (var y2 = 0; y2 < splitCount; y2++)
                    {
                        for (var x2 = 0; x2 < splitCount; x2++)
                        {
                            var xPos = x2 / (float)splitCount;
                            var xAdv = (x2 + 1) / (float)splitCount;
                            var yPos = y2 / (float)splitCount;
                            var yAdv = (y2 + 1) / (float)splitCount;

                            var tl = GetPointHeight(heights, new Vector2(xPos, yPos));
                            var tr = GetPointHeight(heights, new Vector2(xAdv, yPos));
                            var bl = GetPointHeight(heights, new Vector2(xPos, yAdv));
                            var br = GetPointHeight(heights, new Vector2(xAdv, yAdv));

                            var c = new Cell();
                            c.Heights = new Vector4(tl, tr, bl, br);
                            c.Type = cell.Type;
                            c.Top = cell.Top;
                            c.Right = cell.Right;
                            c.Front = cell.Front;

                            var newX = x * splitCount + x2;
                            var newY = y * splitCount + (splitCount-y2-1);

                            cells[newX + newY * map.Width] = c;
                            walkCells[newX + newY * map.Width] = new RoWalkCell() { Heights = c.Heights, Type = c.Type };
                        }
                    }
                }
            }


            if (!Directory.Exists("Assets/maps/altitude/mapinfo"))
                Directory.CreateDirectory("Assets/maps/altitude/mapinfo");

            map.SaveCellDataToFile(cells, Path.Combine("Assets/maps/altitude/mapinfo", name + "_walk.bytes").Replace("\\", "/"));

            if (!Directory.Exists("Assets/maps/altitude/walkdata"))
                Directory.CreateDirectory("Assets/maps/altitude/walkdata");

            var walkData = ScriptableObject.CreateInstance<RagnarokWalkData>();
            walkData.Width = map.Width;
            walkData.Height = map.Height;
            walkData.Cells = walkCells;

            AssetDatabase.CreateAsset(walkData, Path.Combine("Assets/maps/altitude/walkdata", name + "_walkdata.asset").Replace("\\", "/"));

            map.WalkCellData = walkData;

            Debug.Log($"Done splitting map walk data for {name}");

            return map;
        }

        public RoMapData LoadWalkData(string path, float waterHeight)
        {
            var filename = path;
            var basename = Path.GetFileNameWithoutExtension(filename);
            var basedir = Path.GetDirectoryName(path);

            fs = new FileStream(filename, FileMode.Open);
            br = new BinaryReader(fs);

            var header = new string(br.ReadChars(4));
            if (header != "GRAT")
                throw new Exception("Not altitude");

            var majorVersion = br.ReadByte();
            var minorVersion = br.ReadByte();
            version = majorVersion * 10 + minorVersion;

            var map = ScriptableObject.CreateInstance<RoMapData>();

            map.Width = br.ReadInt32();
            map.Height = br.ReadInt32();
            map.InitialSize = new Vector2Int(map.Width, map.Height);

            map.IsWalkTable = true;
            map.Textures = GetTextures();

            var realWidth = (int)Math.Ceiling((map.Width) / 16f) * 16;
            var realHeight = (int)Math.Ceiling((map.Height) / 16f) * 16;

            var cells = new Cell[realWidth * realHeight];

            var walkCells = new RoWalkCell[realWidth * realHeight];

            for (var y = 0; y < realHeight; y++)
            {
                for (var x = 0; x < realWidth; x++)
                {
                    if (x >= map.Width || y >= map.Height)
                    {
                        var c2 = new Cell();
                        c2.Type = CellType.None;
                        c2.Heights = Vector4.zero;
                        c2.Top = new Tile() { Enabled = true, Texture = "red", UVs = VectorHelper.DefaultQuadUVs(), Color = Color.white, IsUnlit = false };
                        c2.Right = new Tile() { Enabled = false, UVs = VectorHelper.DefaultQuadUVs() };
                        c2.Front = new Tile() { Enabled = false, UVs = VectorHelper.DefaultQuadUVs() };
                        cells[x + y * realWidth] = c2;
                        continue;
                    }
                    
                    var v1 = br.ReadSingle();
                    var v2 = br.ReadSingle();
                    var v3 = br.ReadSingle();
                    var v4 = br.ReadSingle();

                    var c = new Cell();
                    c.Heights = new Vector4(v3, v4, v1, v2) * -1;
                    var type = br.ReadInt32();
                    var color = "";

                    
                    var avg = (v1 + v2 + v3 + v4) / 4f;
                    var isInWater = avg > -waterHeight * 5f;

                    if (x + 1 == map.Width || y + 1 == map.Height)
                        type = 1;

                    switch (type)
                    {
                        case 0:
                            c.Type = CellType.Walkable | CellType.Snipable;
                            color = "green";
                            break;
                        case 1:
                            c.Type = CellType.None;
                            color = "red";
                            break;
                        case 2:
                            c.Type = CellType.Walkable | CellType.Snipable;
                            color = "orange";
                            break;
                        case 3:
                            c.Type = CellType.Walkable | CellType.Snipable | CellType.Water;
                            color = "blue";
                            break;
                        case 4:
                            c.Type = CellType.Walkable | CellType.Snipable;
                            color = "purple";
                            break;
                        case 5:
                            c.Type = CellType.Snipable;
                            color = "yellow";
                            break;
                        case 6:
                            c.Type = CellType.Walkable | CellType.Snipable;
                            color = "purple";
                            break;
                        default:
                            throw new Exception("Unknown cell type " + type);
                    }

                    if (isInWater && ((c.Type & CellType.Walkable) > 0 || (c.Type & CellType.Snipable) > 0))
                    {
                        c.Type |= CellType.Water;
                        if ((c.Type & CellType.Walkable) > 0)
                            color = "blue";
                        Debug.Log($"Water cell {x},{y} type {c.Type}");
                    }

                    c.Top = new Tile() { Enabled = true, Texture = color, UVs = VectorHelper.DefaultQuadUVs(), Color = Color.white, IsUnlit = false };
                    c.Right = new Tile() { Enabled = false, UVs = VectorHelper.DefaultQuadUVs() };
                    c.Front = new Tile() { Enabled = false, UVs = VectorHelper.DefaultQuadUVs() };

                    cells[x + y * realWidth] = c;
                    walkCells[x + y * realWidth] = new RoWalkCell() { Heights = c.Heights, Type = c.Type };
                }
            }

            map.Width = realWidth;
            map.Height = realHeight;

            if (!Directory.Exists("Assets/maps/altitude/mapinfo"))
                Directory.CreateDirectory("Assets/maps/altitude/mapinfo");

            map.SaveCellDataToFile(cells, Path.Combine("Assets/maps/altitude/mapinfo", basename + "_walk.bytes").Replace("\\", "/"));

            if (!Directory.Exists("Assets/maps/altitude/walkdata"))
                Directory.CreateDirectory("Assets/maps/altitude/walkdata");

            var walkData = ScriptableObject.CreateInstance<RagnarokWalkData>();
            walkData.Width = realWidth;
            walkData.Height = realHeight;
            walkData.Cells = walkCells;

            AssetDatabase.CreateAsset(walkData, Path.Combine("Assets/maps/altitude/walkdata", basename + "_walkdata.asset").Replace("\\", "/"));

            map.WalkCellData = walkData;

            Debug.Log($"Done loading data, read {fs.Position}/{fs.Length}");

            fs.Close();

            return map;
        }
    }
}