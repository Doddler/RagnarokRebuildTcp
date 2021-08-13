using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor
{
    class RoMapChangeTracker
    {
        private readonly List<MapSubsetData> changeData;

        private int count = 0;
        private const int MaxChanges = 20;

        public RoMapChangeTracker()
        {
            changeData = new List<MapSubsetData>();
        }

        public void Register(RectInt area, Vector2Int mapSize, Cell[] cells)
        {
            //var bytes = SerializationUtility.SerializeValue(cells, DataFormat.Binary);
            if (count > MaxChanges)
            {
                changeData.RemoveAt(0);
                count--;
            }

            //Debug.Log($"Storing undo into position {count} covering area {area}");

            area.ClampToBounds(new RectInt(0, 0, mapSize.x, mapSize.y));

            var data = new MapSubsetData();
            data.Store(cells, mapSize, area);
            changeData.Add(data);
            //Debug.Log($"Adding {data} to changeData, new count is: {changeData.Count}");
            count++;
        }

        public bool Undo(Cell[] cells, Vector2Int mapSize, out RectInt area)
        {
            if (count == 0)
            {
#if UNITY_EDITOR
                EditorApplication.Beep();
#endif
                cells = null;
                area = new RectInt();
                return false;
            }

            var data = changeData[count - 1];
            
            if(data == null)
                throw new Exception($"WHOA! Trying to pull data that doesn't exist from position {count-1}.");
            
            data.Restore(cells, mapSize);
            area = data.Area;

            changeData.RemoveAt(count-1);
            
            count--;
            
            return true;
        }

        //public bool Redo(out Cell[] cells, out RectInt area)
        //{
        //    if (count == total)
        //    {
        //        Debug.Log($"Can't redo, exhausted available data. Currently at position {count} of {total}.");
        //        cells = null;
        //        area = new RectInt();
        //        return false;
        //    }

        //    Debug.Log($"Redo: {count} {total} {storedData.Count} {affectedAreas.Count}");
            
        //    cells = SerializationUtility.DeserializeValue<Cell[]>(storedData[count], DataFormat.Binary);
        //    area = affectedAreas[count];
        //    count++;

        //    Debug.Log($"Returning redo info for area {count-1} {affectedAreas[count-1]}");

        //    return true;
        //}
    }
}
