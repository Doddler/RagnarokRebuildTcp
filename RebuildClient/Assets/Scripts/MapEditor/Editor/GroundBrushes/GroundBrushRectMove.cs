namespace Assets.Scripts.MapEditor.Editor.GroundBrushes
{
    //[MapBrush("Vertical Move", 0)]
    //class GroundBrushRectMove : MapBasicBrush
    //{
    //    private Vector3 dragHandlePosition;
    //    private Vector3 dragHandleStart;

    //    public void Flatten()
    //    {
    //        Data.Flatten(Editor.SelectedRegion, Editor.HeightSnap * RoMapData.YScale, Editor.DragSeparated);
    //        Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1), true, false);
    //        CenterDragHandle();
    //        RepaintScene();
    //    }

    //    public override void OnEnable()
    //    {
    //        Editor.UpdateSelectionMode(SelectionMode.TopRect);
    //        CenterDragHandle();
    //        UpdateDragHandle();
    //    }

    //    public override void EditorUI()
    //    {
    //        if (Editor.CursorVisible)
    //        {
    //            var cell = Data.Cell(Editor.HoveredTile);
    //            EditorStyles.label.wordWrap = true;
    //            EditorGUILayout.LabelField($"Hover Tile: ({Editor.HoveredTile}) {cell}");
    //        }
    //    }

    //    public override void OnSceneGUI(SceneView sceneView)
    //    {
    //        UpdateDragHandle();
    //    }

    //    public override void OnSelectionChange()
    //    {
    //        if (!Editor.HasSelection)
    //            return;

    //        CenterDragHandle();
    //    }

    //    public void CenterDragHandle()
    //    {
    //        var (tile, realDir) = Editor.GetTileAndDirForDirection(Direction.Center);

    //        //Debug.Log($"Dir: {dir} {Editor.SelectedRegion} x: {x} y: {y} Tile: {tile} RealDir: {realDir}");

    //        dragHandleStart = Editor.GetPositionForTileEdgeOrCorner(tile.x, tile.y, realDir);


    //        //dragHandleStart = Editor.GetTileCenterPosition(Mathf.FloorToInt(Editor.SelectedRegion.center.x), Mathf.FloorToInt(Editor.SelectedRegion.center.y));
    //        dragHandlePosition = dragHandleStart;
    //    }

    //    private void UpdateDragHandle()
    //    {
    //        if (!Editor.HasSelection || Mathf.Approximately(Editor.HeightSnap, 0))
    //            return;

    //        var moved = VerticalDragHandle(dragHandleStart, dragHandlePosition);

    //        if (Mathf.Approximately(moved, 0f))
    //            return;

    //        var dist = moved / RoMapData.YScale;

    //        Data.MoveCells(Editor.SelectedRegion, Editor.DragSeparated, (v, c) =>
    //        {
    //            c.Heights += new Vector4(dist, dist, dist, dist);
    //        });
    //        Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1), true, false);

    //        dragHandleStart.y += moved;
    //        dragHandlePosition.y += moved;
    //    }
    //}
}
