using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Assets.Scripts.MapEditor
{
    [Serializable]
    public class Cell
    {
        public Vector4 Heights;
        public Tile Top;
        public Tile Front;
        public Tile Right;
        public CellType Type;

        public float AverageHeights => (Heights[0] + Heights[1] + Heights[2] + Heights[3]) / 4f;

        public Cell Clone()
        {
            return new Cell() {Heights = Heights, Top = Top.Clone(), Front = Front.Clone(), Right = Right.Clone()};
        }

        public void Serialize(BinaryWriter bw)
        {
            bw.Write(Heights);
            bw.Write(Top != null);
            Top?.Serialize(bw);
            bw.Write(Front != null);
            Front?.Serialize(bw);
            bw.Write(Right != null);
            Right?.Serialize(bw);
            bw.Write((byte)Type);
        }

        public void Deserialize(BinaryReader br)
        {
            Heights = br.ReadVector4();
            Top = null;
            Front = null;
            Right = null;
            if (br.ReadBoolean())
            {
                Top = new Tile();
                Top.Deserialize(br);
            }
            if (br.ReadBoolean())
            {
                Front = new Tile();
                Front.Deserialize(br);
            }
            if (br.ReadBoolean())
            {
                Right = new Tile();
                Right.Deserialize(br);
            }

            Type = (CellType)br.ReadByte();
        }

        public override string ToString()
        {
            return $"Cell Heights: {Heights}|Top:{Top.Enabled} {Top.Texture} {!Top.IsUnlit}|Front:{Front.Enabled} {Front.Texture} {!Front.IsUnlit}|Right:{Right.Enabled} {Right.Texture} {!Right.IsUnlit}";
        }
    }

    [Serializable]
    public class Tile
    {
        public bool Enabled;
        public bool IsUnlit;
        public string Texture;
        public Vector2[] UVs;
        public Color Color;

        public Tile Clone()
        {
            return new Tile() {Enabled = Enabled, Texture = Texture, UVs = (Vector2[])UVs.Clone(), Color = Color, IsUnlit = IsUnlit};
        }

        public Color GetColor()
        {
            //if(IsUnlit)
            //    return Color.black;
            return Color;
        }

        public void Serialize(BinaryWriter bw)
        {
            bw.Write(Enabled);
            if (Enabled)
            {
                bw.Write(IsUnlit);
                bw.Write(Texture);
                for (var i = 0; i < 4; i++)
                    bw.Write(UVs[i]);
                bw.Write(Color);
            }
        }

        public void Deserialize(BinaryReader br)
        {
            Enabled = br.ReadBoolean();
            UVs = new Vector2[4];

            if (!Enabled)
                return;

            IsUnlit = br.ReadBoolean();
            Texture = br.ReadString();
            for (var i = 0; i < 4; i++)
                UVs[i] = br.ReadVector2();
            Color = br.ReadColor();
        }
    }

    [Serializable]
    public class MapWater
    {
        public float Level;
        public int Type;
        public float WaveHeight;
        public float WaveSpeed;
        public float WavePitch;
        public int AnimSpeed;
        public Texture2D[] Images;

    }

    public class MapDataContainer : ScriptableObject
    {
        public Cell[] CellData;

        public static MapDataContainer Deserialize(MapDataContainer container, BinaryReader br)
        {
            var size = br.ReadInt32();
            container.CellData = new Cell[size];

            //Debug.LogWarning("Deserializing size " + size);

            for (var i = 0; i < size; i++)
            {
                container.CellData[i] = new Cell();
                container.CellData[i].Deserialize(br);
            }

            return container;
        }

        public void Serialize(BinaryWriter bw)
        {
            bw.Write(CellData.Length);

            //Debug.LogWarning("Serializing size " + CellData.Length);

            for (var i = 0; i < CellData.Length; i++)
                CellData[i].Serialize(bw);
        }
    }

    [CreateAssetMenu(menuName = "Custom Assets/Map Data")]
    public class RoMapData : ScriptableObject, ISerializationCallbackReceiver
    {
        public int Width;
        public int Height;

        public Vector2Int InitialSize;

        public List<Texture2D> Textures;
        public Texture2D Atlas; 
        public Rect[] AtlasRects;

        public bool IsWalkTable;
        public RoMapData WalkData;
        public RagnarokWalkData WalkCellData;

        public MapWater Water;

        private Dictionary<string, int> TextureLookup = new Dictionary<string, int>();
        private RoMapChangeTracker ChangeTracker = new RoMapChangeTracker();

        public static float YScale = 0.20f;

#if UNITY_EDITOR

        public RectInt Rect => new RectInt(0, 0, Width, Height);
        public Vector2Int Size => new Vector2Int(Width, Height);
        public Cell Cell(int x, int y) => GetCell(x, y);
        public Cell Cell(Vector2Int pos) => GetCell(pos.x, pos.y);

        [NonSerialized]
        private RoMapSharedMeshData sharedMeshData;
        public RoMapSharedMeshData SharedMeshData => sharedMeshData ??= new RoMapSharedMeshData(this);

        private MapSubsetData copyData;
        
        [NonSerialized]
        public bool HasNeighborChanges = false;


        private Cell GetCell(int x, int y)
        {
            if (container == null)
                LoadCellData();
            return container.CellData[x + y * Width];
        }

        public void SetCellTexture(string texture, int x, int y)
        {
            container.CellData[x + y * Width].Top.Texture = texture;
        }
        
        [NonSerialized]
        private MapDataContainer container;

        private bool hasChanges;
        
        
        private void LoadCellData()
        {
            var fullPath = AssetDatabase.GetAssetPath(this);
            var path = Path.GetDirectoryName(fullPath);
            var dataPath = Path.Combine(path, "mapinfo", name + ".bytes").Replace("\\", "/");

            if (!File.Exists(dataPath))
                throw new Exception($"Could not find cell data for {name} at path: {dataPath}");

            var bytes = CLZF2.Decompress(File.ReadAllBytes(dataPath));

            if (container == null)
                container = ScriptableObject.CreateInstance<MapDataContainer>();

            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                MapDataContainer.Deserialize(container, br);
            }

            //container.CellData = SerializationUtility.DeserializeValue<Cell[]>(bytes, DataFormat.Binary);
        }

        public void SaveCellDataToFile(Cell[] cellData, string path)
        {
            if (container == null)
            {
                container = ScriptableObject.CreateInstance<MapDataContainer>();
                container.CellData = cellData;
            }
            else
                container.CellData = cellData;

            Debug.Log("Saving cell data to : " + path);

            using (var ms = new MemoryStream(2000000))
            using (var bw = new BinaryWriter(ms))
            {
                container.Serialize(bw);

                var bytes = ms.ToArray();
                var b2 = CLZF2.Compress(bytes);

                var basePath = Path.GetDirectoryName(path);
                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                File.WriteAllBytes(path, b2);
            }
        }

        public void SaveCellData(Cell[] cellData)
        {
            var fullPath = AssetDatabase.GetAssetPath(this);
            var path = Path.GetDirectoryName(fullPath);

            var outPath = Path.Combine(path, "mapinfo", name + ".bytes").Replace("\\", "/");

            SaveCellDataToFile(cellData, outPath);
        }

        private Vector2[] GetUVsForTexture(string texture, Vector2[] uvs)
        {
            var outuv = new Vector2[uvs.Length];

            if (TextureLookup.Count == 0)
                RefreshTextureLookup();

            if (string.IsNullOrEmpty(texture))
                texture = "transparent";

            if (!TextureLookup.TryGetValue(texture, out var texId))
            {
                return GetUntexturedUVs();
            }

            //Debug.Log($"Get texture rect for {texture}");

            for (var i = 0; i < uvs.Length; i++)
            {
                outuv[i] = VectorHelper.RemapUV(uvs[i], AtlasRects[texId]);
            }

            return outuv;
        }

        public Vector2[] GetUntexturedUVs()
        {
            var uvs = GetEmptyUVs().ToArray();

            if (TextureLookup.Count == 0)
                RefreshTextureLookup();

            if (TextureLookup.ContainsKey("BACKSIDE"))
                return GetUVsForTexture("BACKSIDE", uvs);

            if (TextureLookup.ContainsKey("BLACK"))
                return GetUVsForTexture("BLACK", uvs);

            return uvs;
        }

        public Vector2[] TranslateTileTextureUVs(Tile tile)
        {
            //if (string.IsNullOrWhiteSpace(tile.Texture))
            //    return GetUntexturedUVs();

            if (!tile.Enabled)
                return GetUVsForTexture(null, tile.UVs);

            return GetUVsForTexture(tile.Texture, tile.UVs);
        }

        public void RefreshTextureLookup()
        {
            TextureLookup = new Dictionary<string, int>();
            Textures = Textures.OrderBy(t => t.name).ToList();

            for (var i = 0; i < Textures.Count; i++)
            {
                var tex = Textures[i];
                if(!TextureLookup.ContainsKey(tex.name))
					TextureLookup.Add(tex.name, i);
            }
        }
        
        public void RequireTransparentTexture()
        {
            var curPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
            var transPath = "Assets/textures/transparent.png";
            if (File.Exists(transPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(transPath);
                Textures.Add(tex);
                RefreshTextureLookup();
                return;
            }

            var t = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var colors = new Color[t.width * t.height];
            for (var i = 0; i < colors.Length; i++)
                colors[i] = new Color(0f, 0f, 0f, 0f);
            t.SetPixels(colors);
            File.WriteAllBytes(transPath, t.EncodeToPNG());
            AssetDatabase.Refresh();

            var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>(transPath);
            Textures.Add(t2);
            RefreshTextureLookup();
            return;
        }

        public void RebuildAtlas()
        {
            RefreshTextureLookup();

            if (!TextureLookup.ContainsKey("transparent"))
                RequireTransparentTexture();

            TextureImportHelper.SetTexturesReadable(Textures);

            var supertexture = new Texture2D(2, 2, TextureFormat.RGBA32, 2, false);
            supertexture.name = $"{name}_atlas";
            AtlasRects = supertexture.PackTextures(Textures.ToArray(), 4, 4096, false);


            var copyTexture = new Texture2D(supertexture.width, supertexture.height, TextureFormat.RGBA32, false);
            var pixels = supertexture.GetPixels(0, 0, supertexture.width, supertexture.height, 0);
            //Debug.Log(pixels.Length);
            copyTexture.SetPixels(pixels);
            copyTexture.name = supertexture.name;


            supertexture = copyTexture;
            supertexture.wrapMode = TextureWrapMode.Clamp;

            TextureImportHelper.PatchAtlasEdges(supertexture, AtlasRects);

            if (Atlas != null)
                AssetDatabase.RemoveObjectFromAsset(Atlas);

            var path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
            path = Path.Combine(path, "atlas").Replace("\\", "/");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            //File.WriteAllBytes(@"../atlas.png", Atlas.EncodeToPNG());

            supertexture = TextureImportHelper.SaveAndUpdateTexture(supertexture, Path.Combine(path, supertexture.name + ".png"));

			//Debug.Log(path);
   //         AssetDatabase.CreateAsset(supertexture, Path.Combine(path, supertexture.name + ".asset").Replace("\\", "/"));

            Atlas = supertexture;

            

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        public void AddTextures(List<Texture2D> textures)
        {
            foreach (var t in textures)
            {
                if (Textures.Any(tex => tex.name == t.name))
                    continue;

                Textures.Add(t);
            }

            RebuildAtlas();
            RefreshTextureLookup();
        }

        public void RemoveTexture(Texture2D texture)
        {
            Textures.Remove(texture);

            RebuildAtlas();
            RefreshTextureLookup();
        }


        public Cell[] GetCellData()
        {
            if (container != null)
                return this.container.CellData;

            LoadCellData();

            return container.CellData;
        }


        public void CreateNew(int width, int height)
        {
            Width = width;
            Height = height;
            var cellData = new Cell[Width * Height];

            Debug.Log("Create new map");

            Textures = new List<Texture2D>();
            TextureLookup = new Dictionary<string, int>();

            var defTexture = "transparent";

            var assets = AssetDatabase.FindAssets("BLACK");
            foreach (var a in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(a);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    Textures.Add(tex);
                    defTexture = tex.name;
                    break;
                }
            }

            RequireTransparentTexture();
            
            for (var i = 0; i < Width * Height; i++)
            {
                var cell = new Cell
                {
                    Heights = new Vector4(32, 32, 32, 32),
                    Top = NewTile(),
                    Front = NewTile(),
                    Right = NewTile()
                };

                cell.Top.Enabled = true;
                cell.Top.Texture = defTexture;

                cellData[i] = cell;
            }

            SaveCellData(cellData);
            RebuildAtlas();
        }

        public void Copy(RectInt area)
        {
            copyData = new MapSubsetData();
            copyData.Store(GetCellData(), Size, area);
        }

        public bool Paste(Vector2Int position, out RectInt affectedArea)
        {
            affectedArea = new RectInt();
            if (copyData == null)
                return false;
            
            RegisterUndo(copyData.GetOffsetArea(position, Size));
            affectedArea = copyData.RestoreIntoArea(GetCellData(), Size, position);

            return true;
        }

        private List<Vector2> GetEmptyUVs()
        {
            var uvs = new List<Vector2>();
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            return uvs;
        }

        private Tile NewTile()
        {
            return new Tile() { Texture = null, UVs = GetEmptyUVs().ToArray(), Color = Color.gray };
        }

        private bool InBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }
        
        public void UpdateWall(Cell neighbor, bool right, bool joined)
        {
            if (right)
            {
                //Debug.Log($"{neighbor} right wall is {joined}");
                if (!joined && !neighbor.Right.Enabled)
                {
                    HasNeighborChanges = true;
                    neighbor.Right.Enabled = true;
                }

                if (joined && neighbor.Right.Enabled)
                {
                    HasNeighborChanges = true;
                    neighbor.Right.Enabled = false;
                }
            }

            if (!right)
            {
                //Debug.Log($"{neighbor} front wall is {joined}");
                if (!joined && !neighbor.Front.Enabled)
                {
                    HasNeighborChanges = true;
                    neighbor.Front.Enabled = true;
                }

                if (joined && neighbor.Front.Enabled)
                {
                    HasNeighborChanges = true;
                    neighbor.Front.Enabled = false;
                }
            }
        }

        public void UpdateNeighbors(Cell primary, Cell neighbor, Vector2Int relation)
        {
            //Debug.Log("UpdateNeightbors: " + relation);

            if (Mathf.Abs(relation.x) + Mathf.Abs(relation.y) == 2)
                return; //these cells are diagonal neighbors, we have nothing to modify here

            var flipped = Flip(primary.Heights, relation);
            var nHeight = neighbor.Heights;

            if (relation.x == -1)
                UpdateWall(neighbor, true, MathHelper.CloseEnough(flipped[1], nHeight[1]) && MathHelper.CloseEnough(flipped[3], nHeight[3]));
            if (relation.x == 1)
                UpdateWall(primary, true, MathHelper.CloseEnough(flipped[0], nHeight[0]) && MathHelper.CloseEnough(flipped[2], nHeight[2]));
            if (relation.y == -1)
                UpdateWall(primary, false, MathHelper.CloseEnough(flipped[0], nHeight[0]) && MathHelper.CloseEnough(flipped[1], nHeight[1]));
            if (relation.y == 1)
                UpdateWall(neighbor, false, MathHelper.CloseEnough(flipped[2], nHeight[2]) && MathHelper.CloseEnough(flipped[3], nHeight[3]));

        }

        public bool IsNeighborVertexConnected(Vector2Int originPosition, Vector2Int neighborPosition, Vector2Int relation, int vertId)
        {
            var origin = Cell(originPosition);
            var neighbor = Cell(neighborPosition);

            var mask = GetConnectedNeighborHeightMask(origin.Heights, neighbor.Heights, relation);
            var flipped = Flip(mask, relation);


            //Debug.Log(flipped);
            return flipped[vertId] > 0;
        }

        private Vector4 GetConnectedNeighborHeightMask(Vector4 origin, Vector4 neighbor, Vector2Int relation)
        {
            var flipped = Flip(origin, relation);

            if (relation.x == -1 && relation.y == -1)
                return MathHelper.CloseEnough(flipped[1], neighbor[1]) ? new Vector4(0, 1, 0, 0) : Vector4.zero;
            if (relation.x == 1 && relation.y == -1)
                return MathHelper.CloseEnough(flipped[0], neighbor[0]) ? new Vector4(1, 0, 0, 0) : Vector4.zero;
            if (relation.x == -1 && relation.y == 1)
                return MathHelper.CloseEnough(flipped[3], neighbor[3]) ? new Vector4(0, 0, 0, 1) : Vector4.zero;
            if (relation.x == 1 && relation.y == 1)
                return MathHelper.CloseEnough(flipped[2], neighbor[2]) ? new Vector4(0, 0, 1, 0) : Vector4.zero;
            if (relation.x == -1)
                return new Vector4(0, MathHelper.CloseEnough(flipped[1], neighbor[1]) ? 1 : 0, 0, MathHelper.CloseEnough(flipped[3], neighbor[3]) ? 1 : 0);
            if (relation.x == 1)
                return new Vector4(MathHelper.CloseEnough(flipped[0], neighbor[0]) ? 1 : 0, 0, MathHelper.CloseEnough(flipped[2], neighbor[2]) ? 1 : 0, 0);
            if (relation.y == -1)
                return new Vector4(MathHelper.CloseEnough(flipped[0], neighbor[0]) ? 1 : 0, MathHelper.CloseEnough(flipped[1], neighbor[1]) ? 1 : 0, 0, 0);
            if (relation.y == 1)
                return new Vector4(0, 0, MathHelper.CloseEnough(flipped[2], neighbor[2]) ? 1 : 0, MathHelper.CloseEnough(flipped[3], neighbor[3]) ? 1 : 0);
            return Vector4.zero;
        }

        private Vector4 Flip(Vector4 vector, Vector2Int relation)
        {
            if (Mathf.Abs(relation.x) == 1)
                vector = new Vector4(vector[1], vector[0], vector[3], vector[2]);

            if (Mathf.Abs(relation.y) == 1)
                vector = new Vector4(vector[2], vector[3], vector[0], vector[1]);

            return vector;
        }

        private Vector4 Mask(Vector4 vector, Vector4 mask)
        {
            return new Vector4(vector.x * mask.x, vector.y * mask.y, vector.z * mask.z, vector.w * mask.w);
        }

        private void PropagateHeightChangeToNeighbor(Cell primary, Cell neighbor, Vector2Int relation, Vector4 initial)
        {
            var diff = primary.Heights - initial;
            var mask = GetConnectedNeighborHeightMask(initial, neighbor.Heights, relation);

            //Debug.Log($"{relation} mask: {mask}. Heights will be modified by: " + Mask(Flip(diff, relation), mask));

            neighbor.Heights += Mask(Flip(diff, relation), mask);
        }

        public void RegisterUndo(RectInt area)
        {
            Profiler.BeginSample("Register Undo");
            ChangeTracker.Register(area, new Vector2Int(Width, Height),  GetCellData());
            Profiler.EndSample();
        }

        public bool UndoChange(out RectInt affectedRegion)
        {
            var success = ChangeTracker.Undo(GetCellData(), new Vector2Int(Width, Height), out affectedRegion);
            if (success)
                EditorUtility.SetDirty(this);

            return success;
        }

        //public bool RedoChange(out RectInt affectedRegion)
        //{
        //    var success = ChangeTracker.Redo(out var cells, out affectedRegion);
        //    if(success)
        //    {
        //        container.CellData = cells;
        //        EditorUtility.SetDirty(this);
        //    }

        //    return success;
        //}


        private void UpdateWalkCellData(RectInt area)
        {
            area = area.ExpandRect(1);
            area.ClampToBounds(Rect);

            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    var wc = WalkCellData.Cells[x + y * Width];
                    var cell = Cell(x, y);
                    wc.Heights = cell.Heights;
                    wc.Type = RagnarokWalkData.ColorToCellMask(cell.Top.Texture);
                    WalkCellData.Cells[x + y * Width] = wc;
                }
            }

            EditorUtility.SetDirty(WalkCellData);
        }


        public void ModifyHeightsRect(RectInt area, bool detachMode, Action<Vector2, Cell> modifyAction)
        {
            var cells = GetCellData();
            HasNeighborChanges = false;

            for (var x1 = area.xMin; x1 < area.xMax; x1++)
            {
                for (var y1 = area.yMin; y1 < area.yMax; y1++)
                {
                    var cell = cells[x1 + y1 * Width];
                    var heights = cell.Heights;

                    modifyAction(new Vector2Int(x1, y1), cell);

                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            if (x == 0 && y == 0)
                                continue;

                            var relation = new Vector2Int(x, y);
                            var neighbor = new Vector2Int(x1 + x, y1 + y);

                            if (!InBounds(neighbor.x, neighbor.y))
                            {
                                //Debug.Log($"Neighbor {relation} out of bounds");
                                continue;
                            }

                            if (area.Contains(neighbor))
                            {
                                //Debug.Log($"{relation} is already being modified");
                                continue;
                            }

                            var neighborCell = cells[neighbor.x + neighbor.y * Width];

                            if (!detachMode)
                                PropagateHeightChangeToNeighbor(cell, neighborCell, relation, heights);

                            UpdateNeighbors(cell, neighborCell, relation);
                        }

                    }
                }
            }

            if (WalkCellData != null)
                UpdateWalkCellData(area);

            hasChanges = true;
            EditorUtility.SetDirty(this);
        }

        public void ModifyHeights(List<Vector2Int> cellPositions, bool detachMode, Action<Vector2, Cell> modifyAction)
        {
            Profiler.BeginSample("ModifyHeights");
            var cells = GetCellData();
            
            foreach (var pos in cellPositions)
            {
                var cell = cells[pos.x + pos.y * Width];
                var heights = cell.Heights;

                modifyAction(pos, cell);

                for (var x = -1; x <= 1; x++)
                {
                    for (var y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;

                        var relation = new Vector2Int(x, y);
                        var neighbor = new Vector2Int(pos.x + x, pos.y + y);

                        if (!InBounds(neighbor.x, neighbor.y))
                        {
                            //Debug.Log($"Neighbor {relation} out of bounds");
                            continue;
                        }

                        if (cellPositions.Any(p => p == neighbor))
                        {
                            //Debug.Log($"{relation} is already being modified");
                            continue;
                        }

                        var neighborCell = cells[neighbor.x + neighbor.y * Width];

                        Profiler.BeginSample("PropagateAndUpdateNeighbors");
                        if (!detachMode)
                            PropagateHeightChangeToNeighbor(cell, neighborCell, relation, heights);

                        UpdateNeighbors(cell, neighborCell, relation);
                        Profiler.EndSample();
                    }

                }
            }

            hasChanges = true;
            EditorUtility.SetDirty(this);
            Profiler.EndSample();
        }

        public List<Vector2Int> GatherCellsInArea(RectInt area)
        {
            Profiler.BeginSample("GatherCellsInArea");
            var cells = new List<Vector2Int>();

            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
            Profiler.EndSample();
            return cells;
        }
        
        public void MoveCells(RectInt area, bool detachedMode, Action<Vector2, Cell> modifyAction)
        {
            Profiler.BeginSample("Move Cells");
            //var pos = GatherCellsInArea(area);
            ModifyHeightsRect(area, detachedMode, modifyAction);

            hasChanges = true;
            EditorUtility.SetDirty(this);
            Profiler.EndSample();
        }

        public void OnBeforeSerialize()
        {
            if (hasChanges)
            {
                hasChanges = false;
                SaveCellData(container.CellData);
            }
        }

        public void OnAfterDeserialize()
        {
            //Debug.Log("OnDeSerialize");
        }

        public void OnUndo()
        {

        }

#else

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize() { }

#endif
    }
}
