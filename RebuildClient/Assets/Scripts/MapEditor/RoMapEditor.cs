using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.MapEditor
{
    public enum EditMode
    {
        Height,
        Texture,
        Startup,
    }

    public enum SelectionMode
    {
        None,
        TopRect,
        SideRect,
    }


    [ExecuteInEditMode]
    public class RoMapEditor : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField]
        private RoMapData mapData;

        public RoMapChunk[] Chunks;
        public int ChunkWidth;
        public int ChunkHeight;

        public Material MapMaterial;
        public Material ShadowMaterial;

        public EditMode CurrentMode;
        public bool DragSeparated;
        public float TileSize = 2f;
        public float HeightSnap = 2f;

        public bool PaintEmptyTileColorsBlack = false;
        
        public const int ChunkSizeInTiles = 16;

        public bool IsEditorStartupMode;

        public IMapEditor CurrentBrush;

        public LightProbeGroup ProbeGroup;

        [NonSerialized] private Tool lastTool;
        [NonSerialized] private bool hasToolStored;
        [NonSerialized] private bool inEditMode;

        public RectInt SelectedRegion;
        public bool HasSelection;
        private SelectionMode selectionMode;

        [NonSerialized] public Vector2Int HoveredTile;
        [NonSerialized] public bool CursorVisible;

        private bool drawCursor;
        public int CursorSize;
        private Texture2D cursorTexture;

        private RectInt cursorArea;

        public RoMapData MapData => mapData;

        public void Awake()
        {
	        if (Application.isPlaying && Camera.main == null && !mapData.IsWalkTable)
	        {
		        NetworkManager.SpawnMap = name;
		        SceneManager.LoadScene("MainScene");
	        }
        }

        public void Initialize(RoMapData data)
        {
            mapData = data;
            if (mapData.IsWalkTable)
                TileSize = 1f;
            RebuildMesh();
            MakeStatic();
        }

        public void AdjustRendererInChildren(bool enabled)
        {
            var childRenderers = gameObject.GetComponentsInChildren<Renderer>();
            foreach (var r in childRenderers)
            {
                r.enabled = enabled;
            }
        }

        public void EnterEditMode()
        {
            if(MapData.IsWalkTable)
                AdjustRendererInChildren(true);
            if (hasToolStored)
                return;
            lastTool = Tools.current;
            Tools.current = Tool.None;
            hasToolStored = true;
            inEditMode = true;

            //Debug.Log(name + " - Enter Edit Mode");
        }

        public void LeaveEditMode()
        {
            if (MapData.IsWalkTable)
                AdjustRendererInChildren(false);
            if (hasToolStored)
                Tools.current = lastTool;
            hasToolStored = false;
            inEditMode = false;
            IsEditorStartupMode = false;
            //Debug.Log(name + " - Leave Edit Mode");
        }

        public void Reload()
        {
            RebuildMeshInArea(MapData.Rect);
            if(gameObject.isStatic)
                RebuildUVDataInArea(MapData.Rect);
            //Initialize(mapData);
        }

        public void RebuildMesh()
        {
            RebuildMeshInArea(MapData.Rect);
            RebuildUVDataInArea(MapData.Rect);
        }

        public void MakeStatic()
        {
            gameObject.isStatic = true;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                child.gameObject.isStatic = true;
                foreach (Transform t in child.transform)
                {
                    if(!MapData.IsWalkTable)
                        t.gameObject.isStatic = true;
                    else
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
                }
            }

            RebuildDirtyChunkData();
            RebuildProbes();
        }

        public void RemoveStatic()
        {
            gameObject.isStatic = false;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                child.gameObject.isStatic = false;
                foreach (Transform t in child.transform)
                    t.gameObject.isStatic = false;
            }
        }

        public void UpdateAtlasTexture()
        {
            PrepareMaterial();

            for (var x = 0; x < ChunkWidth; x++)
            {
                for (var y = 0; y < ChunkHeight; y++)
                {
                    var chunk = Chunks[x + y * ChunkWidth];
                    if (chunk == null)
                        continue;

                    //chunk.RebuildUVDataOnly();
                    chunk.RebuildMeshData(TileSize, PaintEmptyTileColorsBlack);
                    if(gameObject.isStatic)
                        chunk.RebuildUvs();
                }
            }
        }

        private void RebuildProbes()
        {
            if (MapData.IsWalkTable)
                return;

            if (ProbeGroup == null)
            {
                var go = new GameObject();
                ProbeGroup = go.AddComponent<LightProbeGroup>();
            }

            ProbeGroup.gameObject.name = "LightProbes";

            var layerMask = ~(1 << LayerMask.NameToLayer("DynamicObject"));

            var probePositions = new List<Vector3>();

            for (var x = 0; x < MapData.Width; x += 3)
            {
                for (var y = 0; y < MapData.Height; y += 3)
                {
                    
                    var cell = MapData.Cell(x, y);

                    var castBottom = new Vector3(x * TileSize, cell.AverageHeights, y * TileSize);
                    var topCast = new Vector3(x * TileSize, cell.AverageHeights * RoMapData.YScale + 50f, y * TileSize);

                    var ray = new Ray(topCast, Vector3.down);

                    if (Physics.Raycast(ray, out var hitInfo, 100f, layerMask))
                    {
                        probePositions.Add(hitInfo.point);
                    }
                    else
						probePositions.Add(castBottom);

                    if(x % 30 == 0 && y % 30 == 0)
						probePositions.Add(new Vector3(x * TileSize, cell.AverageHeights * RoMapData.YScale + 50f, y * TileSize));
                }
            }

            ProbeGroup.probePositions = probePositions.ToArray();
        }

        private void PrepareMaterial()
        {
            if (MapMaterial == null)
            {
                if(MapData.Atlas == null)
                    MapData.RebuildAtlas();

                if(!MapData.IsWalkTable)
                    MapMaterial = new Material(Shader.Find("Custom/MapShaderWithVertexColor"));
                else
                    MapMaterial = new Material(Shader.Find("Unlit/WalkableShader"));
            }

            if (ShadowMaterial == null)
            {
                ShadowMaterial = new Material(Shader.Find("Custom/MapShaderUnlit"));
                ShadowMaterial.color = Color.black;
            }

            if (MapData.IsWalkTable)
            {
                MapMaterial.SetFloat("_Glossiness", 0f);
                MapMaterial.mainTexture = MapData.Atlas;
                MapMaterial.color = Color.white;
                MapMaterial.name = MapData.Atlas.name;
                MapMaterial.doubleSidedGI = false;
                MapMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                MapMaterial.enableInstancing = true;
            }
            else
            {
                MapMaterial.SetFloat("_Glossiness", 0f);
                MapMaterial.mainTexture = MapData.Atlas;
                MapMaterial.color = Color.white;
                MapMaterial.name = MapData.Atlas.name;
                MapMaterial.doubleSidedGI = true;
                MapMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                MapMaterial.enableInstancing = true;
            }

            ShadowMaterial.mainTexture = MapData.Atlas;
            ShadowMaterial.name = MapData.Atlas.name + " shadow";
        }

        public void RebuildDirtyChunkData()
        {
            foreach (var c in Chunks)
            {
                if(c.NeedsUVUpdate)
                    c.RebuildUvs();
            }
        }

        public void RebuildUVDataInArea(RectInt r)
        {
            if (mapData.IsWalkTable)
                return; //no need for this

            r.ClampToBounds(new RectInt(0, 0, MapData.Width, MapData.Height));

            var chunkXMin = r.xMin / ChunkSizeInTiles;
            var chunkXMax = (r.xMax - 1) / ChunkSizeInTiles;
            var chunkYMin = r.yMin / ChunkSizeInTiles;
            var chunkYMax = (r.yMax - 1) / ChunkSizeInTiles;

            for (var x = chunkXMin; x <= chunkXMax; x++)
            {
                for (var y = chunkYMin; y <= chunkYMax; y++)
                {
                    var chunk = Chunks[x + y * ChunkWidth];
                    if(chunk != null)
                        chunk.RebuildUvs();
                }
            }
        }
        
        public void RebuildMeshInArea(RectInt r)
        {
            Profiler.BeginSample("RebuildMeshInArea");
            r.ClampToBounds(new RectInt(0, 0, MapData.Width, MapData.Height));

            var chunkXMin = r.xMin / ChunkSizeInTiles;
            var chunkXMax = (r.xMax-1) / ChunkSizeInTiles;
            var chunkYMin = r.yMin / ChunkSizeInTiles;
            var chunkYMax = (r.yMax-1) / ChunkSizeInTiles;

            PrepareMaterial();

            //Debug.Log($"Rebuilding mesh area {r} in chunks {chunkXMin},{chunkYMin} to {chunkXMax},{chunkYMax} (mesh size {MapData.Width},{MapData.Height})");

            if (Chunks == null)
            {
                ChunkWidth = MapData.Width / ChunkSizeInTiles;
                ChunkHeight = MapData.Height / ChunkSizeInTiles;

                Chunks = new RoMapChunk[ChunkWidth * ChunkHeight];
            }

            for (var x = chunkXMin; x <= chunkXMax; x++)
            {
                for (var y = chunkYMin; y <= chunkYMax; y++)
                {
                    var chunk = Chunks[x + y * ChunkWidth];
                    if (chunk == null)
                    {
                        var go = new GameObject($"Chunk{x}-{y}");
                        chunk = go.AddComponent<RoMapChunk>();
                        chunk.hideFlags = HideFlags.NotEditable;
                        Chunks[x + y * ChunkWidth] = chunk;
                        chunk.Initialize(MapData, MapMaterial, ShadowMaterial,
                            new RectInt(x * ChunkSizeInTiles, y * ChunkSizeInTiles, ChunkSizeInTiles, ChunkSizeInTiles),
                            TileSize, PaintEmptyTileColorsBlack);
                    }

                    chunk.transform.parent = gameObject.transform;
                    chunk.transform.localPosition = new Vector3(x * TileSize * ChunkSizeInTiles, 0, y * TileSize * ChunkSizeInTiles);
                    //chunk.gameObject.hideFlags = HideFlags.HideInHierarchy;

                    chunk.UpdateMaterial(MapMaterial, ShadowMaterial);
                    chunk.RebuildMeshData(TileSize, PaintEmptyTileColorsBlack);
                }
            }

            EditorUtility.SetDirty(gameObject);
            Profiler.EndSample();
        }

        public void ChangeEditMode(EditMode newMode)
        {
            CurrentMode = newMode;
            HasSelection = false;
        }

        public void UpdateSelectionMode(SelectionMode mode)
        {
            if (selectionMode != mode)
                HasSelection = false;

            selectionMode = mode;
        }


        private int GetLayerForMap()
        {
            var layer = LayerMask.NameToLayer("Ground");
            if (MapData.IsWalkTable)
                layer = LayerMask.NameToLayer("WalkMap");

            return layer;
        }

        public void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;

        }

        public void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public void Update()
        {
            if (Selection.activeGameObject != gameObject && hasToolStored)
                LeaveEditMode();

            Shader.SetGlobalColor("_FakeAmbient", Color.white);
        }

        private Material highlightMaterial;

        private void PrepareDrawMaterial()
        {
            if (highlightMaterial != null)
                return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            highlightMaterial = new Material(shader);
            highlightMaterial.hideFlags = HideFlags.HideAndDontSave;

            highlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            highlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            highlightMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            highlightMaterial.SetInt("_ZWrite", 0);
        }

        private void HighlightTopFaces(RectInt region)
        {
            var cells = MapData.GetCellData();

            PrepareDrawMaterial();

            GL.PushMatrix();
            GL.MultMatrix(Handles.matrix);
            highlightMaterial.SetPass(0);

            GL.Begin(GL.TRIANGLES);

            GL.Color(new Color(1f, 1f, 1f, 0.3f));

            for (var x = region.xMin; x < region.xMax; x++)
            {
                for (var y = region.yMin; y < region.yMax; y++)
                {
                    var cell = cells[x + y * MapData.Width];

                    var offset = Vector3.zero;
                    if(MapData.IsWalkTable)
                        offset += new Vector3(0f, 0.05f, 0f);

                    var v1 = new Vector3((x + 0) * TileSize, cell.Heights[0] * RoMapData.YScale, (y + 1) * TileSize) + offset;
                    var v2 = new Vector3((x + 1) * TileSize, cell.Heights[1] * RoMapData.YScale, (y + 1) * TileSize) + offset;
                    var v3 = new Vector3((x + 0) * TileSize, cell.Heights[2] * RoMapData.YScale, (y + 0) * TileSize) + offset;
                    var v4 = new Vector3((x + 1) * TileSize, cell.Heights[3] * RoMapData.YScale, (y + 0) * TileSize) + offset;

                    GL.Vertex(v1 + transform.position);
                    GL.Vertex(v2 + transform.position);
                    GL.Vertex(v4 + transform.position);
                    GL.Vertex(v4 + transform.position);
                    GL.Vertex(v3 + transform.position);
                    GL.Vertex(v1 + transform.position);
                }
            }

            GL.End();

            GL.PopMatrix();
        }
        
        public (Vector2Int, Direction) GetTileAndDirForDirection(Direction dir)
        {
            if (dir == Direction.SouthWest)
                return (new Vector2Int(SelectedRegion.xMin, SelectedRegion.yMin), dir);
            if (dir == Direction.NorthWest)
                return (new Vector2Int(SelectedRegion.xMin, SelectedRegion.yMax - 1), dir);
            if (dir == Direction.SouthEast)
                return (new Vector2Int(SelectedRegion.xMax - 1, SelectedRegion.yMin), dir);
            if (dir == Direction.NorthEast)
                return (new Vector2Int(SelectedRegion.xMax - 1, SelectedRegion.yMax - 1), dir);

            var xOdd = SelectedRegion.width % 2 == 1;
            var yOdd = SelectedRegion.height % 2 == 1;
            var x = Mathf.FloorToInt(SelectedRegion.center.x - (xOdd ? 0f : 0.5f));
            var y = Mathf.FloorToInt(SelectedRegion.center.y - (yOdd ? 0f : 0.5f));

            if (dir == Direction.West)
                return (new Vector2Int(SelectedRegion.xMin, y), yOdd ? Direction.West : Direction.NorthWest);
            if (dir == Direction.East)
                return (new Vector2Int(SelectedRegion.xMax - 1, y), yOdd ? Direction.East : Direction.NorthEast);
            if (dir == Direction.South)
                return (new Vector2Int(x, SelectedRegion.yMin), xOdd ? Direction.South : Direction.SouthEast);
            if (dir == Direction.North)
                return (new Vector2Int(x, SelectedRegion.yMax - 1), xOdd ? Direction.North : Direction.NorthEast);

            if(xOdd && !yOdd)
                return (new Vector2Int(x, y), Direction.North);

            if (!xOdd && yOdd)
                return (new Vector2Int(x, y), Direction.East);

            if (xOdd && yOdd)
                return (new Vector2Int(x, y), Direction.None);
            
            return (new Vector2Int(x, y), Direction.NorthEast);

        }

        public Vector3 GetPositionForTileEdgeOrCorner(int x, int y, Direction dir)
        {
            var tile = MapData.Cell(x, y);

            if (dir == Direction.None)
            {
                var pos = new Vector3((x + 0) * TileSize, tile.Heights[0] * RoMapData.YScale, (y + 0) * TileSize);
                pos += new Vector3((x + 1) * TileSize, tile.Heights[1] * RoMapData.YScale, (y + 0) * TileSize);
                pos += new Vector3((x + 0) * TileSize, tile.Heights[2] * RoMapData.YScale, (y + 1) * TileSize);
                pos += new Vector3((x + 1) * TileSize, tile.Heights[3] * RoMapData.YScale, (y + 1) * TileSize);
                return pos / 4 + transform.position;
            }

            if (dir == Direction.SouthWest)
                return new Vector3((x + 0) * TileSize, tile.Heights[2] * RoMapData.YScale, (y + 0) * TileSize) + transform.position;
            if (dir == Direction.NorthWest)
                return new Vector3((x + 0) * TileSize, tile.Heights[0] * RoMapData.YScale, (y + 1) * TileSize) + transform.position;
            if (dir == Direction.SouthEast)
                return new Vector3((x + 1) * TileSize, tile.Heights[3] * RoMapData.YScale, (y + 0) * TileSize) + transform.position;
            if (dir == Direction.NorthEast)
                return new Vector3((x + 1) * TileSize, tile.Heights[1] * RoMapData.YScale, (y + 1) * TileSize) + transform.position;

            if (dir == Direction.West)
            {
                var pos = new Vector3((x + 0) * TileSize, tile.Heights[0] * RoMapData.YScale, (y + 0) * TileSize);
                pos += new Vector3((x + 0) * TileSize, tile.Heights[2] * RoMapData.YScale, (y + 1) * TileSize);
                return pos / 2 + transform.position;
            }
            if (dir == Direction.East)
            {
                var pos = new Vector3((x + 1) * TileSize, tile.Heights[1] * RoMapData.YScale, (y + 0) * TileSize);
                pos += new Vector3((x + 1) * TileSize, tile.Heights[3] * RoMapData.YScale, (y + 1) * TileSize);
                return pos / 2 + transform.position;
            }
            if (dir == Direction.North)
            {
                var pos = new Vector3((x + 0) * TileSize, tile.Heights[0] * RoMapData.YScale, (y + 1) * TileSize);
                pos += new Vector3((x + 1) * TileSize, tile.Heights[1] * RoMapData.YScale, (y + 1) * TileSize);
                return pos / 2 + transform.position;
            }
            if (dir == Direction.South)
            {
                var pos = new Vector3((x + 0) * TileSize, tile.Heights[2] * RoMapData.YScale, (y + 0) * TileSize);
                pos += new Vector3((x + 1) * TileSize, tile.Heights[3] * RoMapData.YScale, (y + 0) * TileSize);
                return pos / 2 + transform.position;
            }

            return Vector3.zero; //shouldn't happen...
        }
        
        public Vector3 GetTileCenterPosition(int x, int y)
        {
            var data = MapData.GetCellData();
            var pos = Vector3.zero;
            var tile = data[x + y * MapData.Width];
            pos += new Vector3((x + 0) * TileSize, tile.Heights.x * RoMapData.YScale, (y + 0) * TileSize);
            pos += new Vector3((x + 1) * TileSize, tile.Heights.y * RoMapData.YScale, (y + 0) * TileSize);
            pos += new Vector3((x + 0) * TileSize, tile.Heights.z * RoMapData.YScale, (y + 1) * TileSize);
            pos += new Vector3((x + 1) * TileSize, tile.Heights.w * RoMapData.YScale, (y + 1) * TileSize);

            return pos / 4 + transform.position;
        }

        public bool GetClosestTileTopToPoint(Vector3 point, out Vector2Int tile)
        {
            tile = new Vector2Int();

            var x = Mathf.FloorToInt((point.x - transform.position.x) / TileSize);
            var y = Mathf.FloorToInt((point.z - transform.position.z) / TileSize);

            if (x < 0 || x >= (ChunkWidth * ChunkSizeInTiles) || y < 0 || y >= (ChunkHeight * ChunkSizeInTiles))
                return false;

            tile = new Vector2Int(x, y);
            return true;
        }

        public void UpdateSelection(Vector3 hitPosition)
        {
            if (CurrentMode == EditMode.Height)
            {
                if (!GetClosestTileTopToPoint(hitPosition, out var tile))
                    return;

                if (Event.current.shift)
                    SelectedRegion = RectHelper.ExpandRectToIncludePoint(SelectedRegion, tile.x, tile.y);
                else
                    SelectedRegion = new RectInt(tile.x, tile.y, 1, 1);

                HasSelection = true;
                //CenterDragHandle();
                CurrentBrush?.OnSelectionChange();
            }
        }
        
        private bool RaycastToChunks(out RaycastHit rayHit)
        {
            var worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var hasHit = false;
            var distance = 9999f;

            rayHit = new RaycastHit();

            for (var i = 0; i < Chunks.Length; i++)
            {
                if (Chunks[i].GetMeshIntersection(worldRay, out var hit))
                {
                    if (hit.distance < distance)
                    {
                        rayHit = hit;
                        hasHit = true;
                        distance = hit.distance;
                    }
                }
            }

            return hasHit;
        }

        private void UpdateCursor()
        {
            //Debug.Log("MOUSEMOVE");
            if (Event.current.type == EventType.MouseMove)
            {
                
                var hasHit = RaycastToChunks(out var hit);

                if (hasHit)
                {
                    //Debug.Log("The hit");

                    if (!GetClosestTileTopToPoint(hit.point, out var tile))
                    {
                        //Debug.Log("But not in range?");
                        CursorVisible = false;
                        return;
                    }

                    HoveredTile = tile;

                    var size = Mathf.Clamp(CursorSize - 1, 0, 20);
                    //Debug.Log(size);
                    cursorArea = new RectInt(tile.x - size / 2, tile.y - size / 2, 1 + size, 1 + size);
                    cursorArea.ClampToBounds(MapData.Rect);
                    
                    CursorVisible = true;
                }
            }

            if (Event.current.type == EventType.Repaint && CursorVisible)
            {
                HighlightTopFaces(cursorArea);
            }
        }

        private bool PerformSelectAction()
        {
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                var hasHit = RaycastToChunks(out var hit);

                if (hasHit)
                {
                    UpdateSelection(hit.point);
                    EditorUtility.SetDirty(gameObject);
                    return true;
                }
                else
                {
                    HasSelection = false;
                    EditorUtility.SetDirty(gameObject);
                    return true;
                }

            }

            return false;
        }

        public void DebugStartupCheck()
        {
	        if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
	        {
		        if (!RaycastToChunks(out var hit))
			        return;
                
		        var walkProvider = GameObject.FindObjectOfType<RoWalkDataProvider>();
		        if (walkProvider == null)
			        return;

		        walkProvider.GetMapPositionForWorldPosition(hit.point, out var mapPos);
                if (!walkProvider.IsCellWalkable(mapPos))
                    return;

                PlayerPrefs.SetInt("DebugStartX", mapPos.x);
                PlayerPrefs.SetInt("DebugStartY", mapPos.y);

                CurrentMode = EditMode.Height;

                EditorApplication.ExecuteMenuItem("Edit/Play");

                IsEditorStartupMode = false;
            }

        }

        public void OnSceneGUI(SceneView sceneView)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (!inEditMode)
	            return;

            //override f command to focus on cursor
            if (Event.current.type == EventType.ExecuteCommand)
            {
                if (Event.current.commandName == "FrameSelected")
                {
                    Event.current.commandName = "";
                    Event.current.Use();
                    
                    if (CursorVisible)
                    {
                        var curPos = GetTileCenterPosition(Mathf.FloorToInt(cursorArea.center.x), Mathf.FloorToInt(cursorArea.center.y));
                        sceneView.Frame(new Bounds(curPos, new Vector3(10, 10, 10)), false);
                    }
                }
            }

            //Debug.Log(controlId);
            HandleUtility.AddDefaultControl(controlId);

            CurrentBrush?.OnSceneGUI();

            UpdateCursor();

            if (Event.current.type == EventType.Repaint && selectionMode == SelectionMode.TopRect && HasSelection)
                HighlightTopFaces(SelectedRegion);

            if (IsEditorStartupMode)
            {
	            DebugStartupCheck();
	            return;
            }

            if (PerformSelectAction())
                return;
        }
#endif
    }
}
