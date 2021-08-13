using System;
using RebuildData.Shared.Enum;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Comparers;
using UnityEngine.Profiling;

namespace Assets.Scripts.MapEditor.Editor.GroundBrushes
{
    [MapBrush("Box Height Tool", 0)]
    class GroundBrushHeightTool : MapBasicBrush
    {
        private Vector3[] dragHandlePosition;
        private Vector3[] dragHandleStart;

        private bool useCornerHandles = true;
        private bool useEdgeHandles = false;
        private bool useCenterHandle = true;

        private float setHeightAmount = 0;

        private bool skipRegisterUndo = false;

        
        public override void OnEnable()
        {
            Editor.UpdateSelectionMode(SelectionMode.TopRect);
            dragHandlePosition = new Vector3[9];
            dragHandleStart = new Vector3[9];

            LoadSettings();
            
            CenterDragHandle();
            UpdateDragHandle();
        }

        public override void OnDisable()
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            useCornerHandles = EditorPrefs.GetBool("GroundBrushHeightTool_useCornerHandles", true);
            useEdgeHandles = EditorPrefs.GetBool("GroundBrushHeightTool_useEdgeHandles", true);
            useCenterHandle = EditorPrefs.GetBool("GroundBrushHeightTool_useCenterHandle", true);
            setHeightAmount = EditorPrefs.GetFloat("GroundBrushHeightTool_setHeightAmount", 10f);
        }

        private void SaveSettings()
        {
            EditorPrefs.SetBool("GroundBrushHeightTool_useCornerHandles", useCornerHandles);
            EditorPrefs.SetBool("GroundBrushHeightTool_useEdgeHandles", useEdgeHandles);
            EditorPrefs.SetBool("GroundBrushHeightTool_useCenterHandle", useCenterHandle);
            EditorPrefs.SetFloat("GroundBrushHeightTool_setHeightAmount", setHeightAmount);
        }

        public override void Repaint()
        {
            if (!Editor.HasSelection)
                return;

            CenterDragHandle();
        }

        public override void OnSelectionChange()
        {
            if (!Editor.HasSelection)
                return;

            CenterDragHandle();
        }
        
        public void CenterDragHandle()
        {
            if (!Editor.HasSelection)
                return;

            for (var i = 0; i < 8; i++)
            {
                var dir = (Direction) i;
                var x = Mathf.FloorToInt(Editor.SelectedRegion.center.x);
                var y = Mathf.FloorToInt(Editor.SelectedRegion.center.y);

                var (tile, realDir) = Editor.GetTileAndDirForDirection(dir);

                //Debug.Log($"Dir: {dir} {Editor.SelectedRegion} x: {x} y: {y} Tile: {tile} RealDir: {realDir}");

                dragHandleStart[i] = Editor.GetPositionForTileEdgeOrCorner(tile.x, tile.y, realDir);
                dragHandlePosition[i] = dragHandleStart[i];
            }

            //center point
            {
                var (tile, realDir) = Editor.GetTileAndDirForDirection(Direction.None);
                dragHandleStart[8] = Editor.GetPositionForTileEdgeOrCorner(tile.x, tile.y, realDir);
                dragHandlePosition[8] = dragHandleStart[8];
            }

            RepaintScene();
        }

        public override void EditorUI()
        {
            var corners = EditorGUILayout.Toggle("Show Corner Handles", useCornerHandles);
            var edges = EditorGUILayout.Toggle("Show Edge Handles", useEdgeHandles);
            var center = EditorGUILayout.Toggle("Show Center Handle", useCenterHandle);

            if (corners != useCornerHandles || edges != useEdgeHandles || center != useCenterHandle)
            {
                useCornerHandles = corners;
                useEdgeHandles = edges;
                useCenterHandle = center;

                SaveSettings();
                RepaintScene();
            }

            setHeightAmount = EditorGUILayout.FloatField("Height", setHeightAmount);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Add"))
                AddHeightToSelection(setHeightAmount);
            if(GUILayout.Button("Subtract"))
                AddHeightToSelection(-setHeightAmount);
            GUILayout.EndHorizontal();
        }

        public override void OnSceneGUI()
        {
            if(Event.current.type == EventType.ValidateCommand && Event.current.commandName == "UndoRedoPerformed")
                CenterDragHandle();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha1)
            {
                useCornerHandles = !useCornerHandles;
                SaveSettings();
                Repaint();
                Event.current.Use();
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha2)
            {
                useEdgeHandles = !useEdgeHandles;
                SaveSettings();
                Repaint();
                Event.current.Use();
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha3)
            {
                useCenterHandle = !useCenterHandle;
                SaveSettings();
                Repaint();
                Event.current.Use();
            }

            UpdateDragHandle();
        }

        public void AddHeightToSelection(float height)
        {
            var sw = new System.Diagnostics.Stopwatch();
            Profiler.BeginSample("AddHeightToSelection");
            sw.Start();

            Data.RegisterUndo(Editor.SelectedRegion.ExpandRect(1));

            var undo = sw.ElapsedMilliseconds;

            Data.ModifyHeightsRect(Editor.SelectedRegion, Editor.DragSeparated, (v, c) =>
            {
                c.Heights += new Vector4(height, height, height, height);
            });

            var mid = sw.ElapsedMilliseconds - undo;

            Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1));
            //Editor.RebuildUVDataInArea(Editor.SelectedRegion.ExpandRect(1));

            var end = sw.ElapsedMilliseconds - mid - undo;
            sw.Stop();
            Profiler.EndSample();

            Debug.Log($"Move performed in {end}ms. Registering undo took {undo}ms, Moving cells took {mid}ms, rebuilding mesh took {end}ms.");

            CenterDragHandle();
            RepaintScene();
        }

        private Vector2 GetEdgePointForDirection(Direction d)
        {
            var sel = Editor.SelectedRegion;
            switch (d)
            {
                case Direction.West: return new Vector2(sel.xMin * Editor.TileSize, sel.center.y * Editor.TileSize);
                case Direction.East: return new Vector2(sel.xMax * Editor.TileSize, sel.center.y * Editor.TileSize);
                case Direction.South: return new Vector2(sel.center.x * Editor.TileSize, sel.yMin * Editor.TileSize);
                case Direction.North: return new Vector2(sel.center.x * Editor.TileSize, sel.yMax * Editor.TileSize);
            }

            return Vector2.zero;
        }

        private Vector2 GetCornerPointForDirection(Direction d)
        {
            var sel = Editor.SelectedRegion;
            switch (d)
            {
                case Direction.SouthWest: return new Vector2((sel.xMin + 0) * Editor.TileSize, (sel.yMin + 0) * Editor.TileSize);
                case Direction.NorthWest: return new Vector2((sel.xMin + 0) * Editor.TileSize, (sel.yMax - 1 + 1) * Editor.TileSize);
                case Direction.SouthEast: return new Vector2((sel.xMax - 1 + 1) * Editor.TileSize, (sel.yMin + 0) * Editor.TileSize);
                case Direction.NorthEast: return new Vector2((sel.xMax - 1 + 1) * Editor.TileSize, (sel.yMax - 1 + 1) * Editor.TileSize);
            }

            return Vector2.zero; //should never happen
        }
        
        private void UpdateDragHandle()
        {
            if (!Editor.HasSelection || Mathf.Approximately(Editor.HeightSnap, 0))
                return;

            Profiler.BeginSample("UpdateDragHandle");

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                skipRegisterUndo = false; //once we let go of the mouse button we can track a new event

            for (var i = 0; i <= 8; i++)
            {
                if (i == 8) //center handle case
                {
                    if (!useCenterHandle)
                        continue;

                    var moved = VerticalDragHandle(dragHandleStart[8], dragHandlePosition[8]);

                    if (Mathf.Approximately(moved, 0f))
                        continue;

                    var dist = moved / RoMapData.YScale;

                    if(!skipRegisterUndo)
                        Data.RegisterUndo(Editor.SelectedRegion.ExpandRect(1));
                    skipRegisterUndo = true;
                    
                    Data.MoveCells(Editor.SelectedRegion, Editor.DragSeparated, (v, c) =>
                    {
                        c.Heights += new Vector4(dist, dist, dist, dist);
                    });
                    Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1));

                    CenterDragHandle();
                    RepaintScene();

                    continue;
                }

                var isCorner = i % 2 == 1;
                if (isCorner && useCornerHandles)
                {
                    var moved = VerticalDragHandle(dragHandleStart[i], dragHandlePosition[i]);
                    if (Mathf.Approximately(moved, 0))
                        continue;

                    var amountToMove = moved / RoMapData.YScale;

                    var dir = (Direction) i;
                    var movedPoint = GetCornerPointForDirection(dir);
                    var oppositePoint = GetCornerPointForDirection(dir.FlipDirection());
                    
                    if (!skipRegisterUndo)
                        Data.RegisterUndo(Editor.SelectedRegion.ExpandRect(1));
                    skipRegisterUndo = true;

                    Data.MoveCells(Editor.SelectedRegion, Editor.DragSeparated, (v, c) =>
                    {
                        var t = new Vector2[4];
                        t[0] = new Vector2((v.x + 0) * Editor.TileSize, (v.y + 1) * Editor.TileSize);
                        t[1] = new Vector2((v.x + 1) * Editor.TileSize, (v.y + 1) * Editor.TileSize);
                        t[2] = new Vector2((v.x + 0) * Editor.TileSize, (v.y + 0) * Editor.TileSize);
                        t[3] = new Vector2((v.x + 1) * Editor.TileSize, (v.y + 0) * Editor.TileSize);

                        for (var j = 0; j < 4; j++)
                        {
                            var xWeight = t[j].x.Remap(oppositePoint.x, movedPoint.x, 0, 1);
                            var yWeight = t[j].y.Remap(oppositePoint.y, movedPoint.y, 0, 1);

                            c.Heights[j] += amountToMove * (xWeight * yWeight);
                        }
                    });

                    Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1));

                    CenterDragHandle();

                    RepaintScene();
                }

                if (!isCorner && useEdgeHandles)
                {
                    var moved = VerticalDragHandle(dragHandleStart[i], dragHandlePosition[i]);

                    if (Mathf.Approximately(moved, 0))
                        continue;

                    var amountToMove = moved / RoMapData.YScale;
                    var dir = (Direction)i;
                    var edge = GetEdgePointForDirection(dir);
                    var oppositeEdge = GetEdgePointForDirection(dir.FlipDirection());

                    var useXWeight = dir == Direction.West || dir == Direction.East;

                    if (!skipRegisterUndo)
                        Data.RegisterUndo(Editor.SelectedRegion.ExpandRect(1));
                    skipRegisterUndo = true;

                    Data.MoveCells(Editor.SelectedRegion, Editor.DragSeparated, (v, c) =>
                    {
                        var t = new Vector2[4];
                        t[0] = new Vector2((v.x + 0) * Editor.TileSize, (v.y + 1) * Editor.TileSize);
                        t[1] = new Vector2((v.x + 1) * Editor.TileSize, (v.y + 1) * Editor.TileSize);
                        t[2] = new Vector2((v.x + 0) * Editor.TileSize, (v.y + 0) * Editor.TileSize);
                        t[3] = new Vector2((v.x + 1) * Editor.TileSize, (v.y + 0) * Editor.TileSize);

                        for (var j = 0; j < 4; j++)
                        {
                            float weight;
                            if(useXWeight)
                                weight = t[j].x.Remap(oppositeEdge.x, edge.x, 0, 1);
                            else
                                weight = t[j].y.Remap(oppositeEdge.y, edge.y, 0, 1);

                            //Debug.Log($"i:{i} j:{j} dir:{dir} amnt:{amountToMove} edge:{edge} oppositeEdge:{oppositeEdge} useX:{useXWeight} weight:{weight}");

                            c.Heights[j] += amountToMove * weight;
                        }
                    });
                    
                    Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1));

                    CenterDragHandle();

                    RepaintScene();
                }
            }

            Profiler.EndSample();
        }
    }
}
