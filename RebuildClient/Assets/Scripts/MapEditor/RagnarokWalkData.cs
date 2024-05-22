using System;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.MapEditor
{

    [Flags]
    public enum CellType
    {
        None = 0,
        Walkable = 1,
        Water = 2,
        Snipable = 4
    }

    [Serializable]
    public struct RoWalkCell
    {
        public Vector4 Heights;
        public CellType Type;
        
        public float AverageHeights => (Heights[0] + Heights[1] + Heights[2] + Heights[3]) / 4f;
    }

    public class RagnarokWalkData : ScriptableObject
    {
        public int Width;
        public int Height;

        public RoWalkCell[] Cells;

        public RoWalkCell Cell(int x, int y) => Cells[x + y * Width];
        public RoWalkCell Cell(Vector2Int pos) => Cells[pos.x + pos.y * Width];
        public bool CellWalkable(int x, int y) => (Cell(x, y).Type & CellType.Walkable) == CellType.Walkable;
        

        public static CellType ColorToCellMask(string color)
        {
            switch (color)
            {
                case "green":
                    return CellType.Walkable | CellType.Snipable;
                case "red":
                    return CellType.None;
                case "blue":
                    return CellType.Walkable | CellType.Snipable | CellType.Water;
                case "yellow":
                    return CellType.Snipable;
            }
            Debug.LogWarning("Could not determine cell type from cell data: " + color);
            return CellType.None;
        }
        
        public void UpdateWalkCellData()
        {
            Debug.LogWarning(name + " WE SERIALIZIN'");
        }
#if UNITY_EDITOR
		public void ExportToFile(string path)
        {
            var outDir = Path.GetDirectoryName(path);
            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);
            
			using (var fs = new FileStream(path, FileMode.Create))
			using (var bw = new BinaryWriter(fs))
			{

				bw.Write(Width);
				bw.Write(Height);

				for (var i = 0; i < Width * Height; i++)
				{
					bw.Write((byte) Cells[i].Type);
				}

				fs.Flush(true);
				fs.Close();
			}
        }
#endif
	}
}
