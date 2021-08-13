using UnityEngine;

namespace Assets.Scripts.MapEditor
{
    public class MapSubsetData
    {
        private Cell[] cellData;
        private RectInt affectedArea;

        public RectInt Area => affectedArea;

        public void Store(Cell[] cells, Vector2Int mapSize, RectInt area)
        {
            affectedArea = area;
            cellData = new Cell[area.width * area.height];

            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    cellData[(x - area.xMin) + (y - area.yMin) * area.width] = cells[x + y * mapSize.x].Clone();
                }
            }
        }

        public RectInt GetOffsetArea(Vector2Int pos, Vector2Int mapSize)
        {
            var area = new RectInt(pos, affectedArea.size);
            area.ClampToBounds(new RectInt(0, 0, mapSize.x, mapSize.y));
            return area;
        }

        public RectInt RestoreIntoArea(Cell[] cells, Vector2Int mapSize, Vector2Int restorePosition)
        {
            var area = GetOffsetArea(restorePosition, mapSize);

            Debug.Log($"Paste into rect {area}");

            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    cells[x + y * mapSize.x] = cellData[(x - area.xMin) + (y - area.yMin) * affectedArea.width].Clone();
                }
            }

            return area;
        }

        public void Restore(Cell[] cells, Vector2Int mapSize)
        {
            var area = affectedArea;

            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    cells[x + y * mapSize.x] = cellData[(x - area.xMin) + (y - area.yMin) * area.width].Clone();
                }
            }
        }
    }
}