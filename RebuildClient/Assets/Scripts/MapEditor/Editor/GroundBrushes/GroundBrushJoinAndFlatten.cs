using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor.GroundBrushes
{
    [MapBrush("Join and Flatten", 2)]
    class GroundBrushJoinAndFlatten : MapBasicBrush
    {
        private float flattenHeight;

        public override void OnEnable()
        {
            Editor.UpdateSelectionMode(SelectionMode.TopRect);
        }

        public override void EditorUI()
        {
            EditorGUILayout.LabelField("Flatten Area");
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Lowest"))
                FlattenToLowest();
            if (GUILayout.Button("Average"))
                FlattenToAverage();
            if (GUILayout.Button("Ceiling"))
                FlattenToHighest();
            GUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Set Height");
            flattenHeight = EditorGUILayout.FloatField("Height", flattenHeight);
            if (GUILayout.Button("Flatten to Height"))
                FlattenToHeight();
            EditorGUILayout.LabelField("Join Edges");

            //GUILayout.BeginHorizontal();
            //EditorGUILayout.ObjectField(Data.Atlas, typeof(Texture2D), false);
            //GUILayout.Button("X", GUILayout.Width(30));
            //GUILayout.EndHorizontal();
        }
        
        public void FlattenToHeight()
        {
            Data.RegisterUndo(Editor.SelectedRegion.ExpandRect(1));
            Data.ModifyHeightsRect(Editor.SelectedRegion, Editor.DragSeparated, (_, c) => c.Heights = new Vector4(flattenHeight, flattenHeight, flattenHeight, flattenHeight));
            Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1));
            RepaintScene();
        }

        public void FlattenToLowest()
        {
            var cells = Data.GatherCellsInArea(Editor.SelectedRegion);
            var min = float.MaxValue;

            foreach (var c in cells)
            {
                var cell = Data.Cell(c);
                for (var i = 0; i < 4; i++)
                {
                    if (cell.Heights[i] < min)
                        min = cell.Heights[i];
                }

            }

            Data.RegisterUndo(Editor.SelectedRegion.ExpandRect(1));
            Data.ModifyHeightsRect(Editor.SelectedRegion, Editor.DragSeparated, (_, c) => c.Heights = new Vector4(min, min, min, min));
            Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1));
            RepaintScene();
        }
        
        public void FlattenToHighest()
        {
            var cells = Data.GatherCellsInArea(Editor.SelectedRegion);
            var max = float.MinValue;

            foreach (var c in cells)
            {
                var cell = Data.Cell(c);
                for (var i = 0; i < 4; i++)
                {
                    if (cell.Heights[i] > max)
                        max = cell.Heights[i];
                }

            }

            Data.RegisterUndo(Editor.SelectedRegion.ExpandRect(1));
            Data.ModifyHeightsRect(Editor.SelectedRegion, Editor.DragSeparated, (_, c) => c.Heights = new Vector4(max, max, max, max));
            Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1));
            RepaintScene();
        }

        private void FlattenToAverage()
        {
            var cells = Data.GatherCellsInArea(Editor.SelectedRegion);
            var sum = 0f;

            foreach (var c in cells)
            {
                var cell = Data.Cell(c);
                for (var i = 0; i < 4; i++)
                    sum += cell.Heights[i];

            }
            
            sum /= cells.Count * 4f;

            sum = MathHelper.SnapValue(sum, Editor.HeightSnap);

            Data.RegisterUndo(Editor.SelectedRegion.ExpandRect(1));
            Data.ModifyHeightsRect(Editor.SelectedRegion, Editor.DragSeparated, (_, c) => c.Heights = new Vector4(sum, sum, sum, sum));
            Editor.RebuildMeshInArea(Editor.SelectedRegion.ExpandRect(1));
            RepaintScene();
        }

    }
}
