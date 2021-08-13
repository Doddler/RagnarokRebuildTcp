using System;
using UnityEngine;

namespace Assets.Scripts.MapEditor
{
    public class RoMapSharedMeshData
    {
#if UNITY_EDITOR
        private readonly RoMapData data;

        private readonly Vector3[][] vertexData;
        private readonly Vector3[][] normalData;
        private readonly Color[][] colorData;

        private readonly Vector3[] cellNormals;
        private readonly Color[] cellColors;

        private bool paintEmptyTilesBlack;


        public RoMapSharedMeshData(RoMapData data)
        {
            this.data = data;

            vertexData = new Vector3[data.Width * data.Height][];
            normalData = new Vector3[data.Width * data.Height][];
            colorData = new Color[data.Width * data.Height][];

            cellNormals = new Vector3[data.Width * data.Height];
            cellColors = new Color[data.Width * data.Height];
        }

        public Vector3[] GetTileVertices(Vector2Int point, Vector3 origin)
        {
            var vertOut = new Vector3[4];
            for (var i = 0; i < 4; i++)
                vertOut[i] = vertexData[point.x + point.y * data.Width][i] - origin;
            return vertOut;
        }

        public Vector3[] GetTileNormals(Vector2Int point)
        {
            return normalData[point.x + point.y * data.Width];
        }

        public Color[] GetTileColors(Vector2Int point)
        {
            return colorData[point.x + point.y * data.Width];
        }

        private int[] GetNeighborsForVertex(int vertexId)
        {
            int[] neighborCheck;
            switch (vertexId)
            {
                case 0: neighborCheck = new[] { 0, 1, 3 }; break;
                case 1: neighborCheck = new[] { 1, 2, 4 }; break;
                case 2: neighborCheck = new[] { 3, 5, 6 }; break;
                case 3: neighborCheck = new[] { 4, 6, 7 }; break;
                default:
                    throw new Exception("Invalid vertex id");
            }

            return neighborCheck;
        }

        private Vector3 AverageNormal(bool[] hasNeighbor, Vector2Int[] neighborDirections, Vector3 initialNormal, Vector2Int homePos, int vertexId)
        {
            var neighborCheck = GetNeighborsForVertex(vertexId);

            var normal = initialNormal;
            var count = 1;

            for (var i = 0; i < neighborCheck.Length; i++)
            {
                var id = neighborCheck[i];

                if (!hasNeighbor[id])
                    continue;

                var neighbor = homePos + neighborDirections[id];
                if (data.IsNeighborVertexConnected(homePos, neighbor, neighborDirections[id], vertexId))
                {
                    normal += cellNormals[neighbor.x + neighbor.y * data.Width];
                    count++;
                }
            }

            return (normal / count).normalized;
        }

        private Color AverageColors(bool[] hasNeighbor, Vector2Int[] neighborDirections, Vector2Int homePos,
            int vertexId)
        {
            var neighborCheck = GetNeighborsForVertex(vertexId);

            var color = cellColors[homePos.x + homePos.y * data.Width];

            if (paintEmptyTilesBlack)
            {
                var cellTex = data.Cell(homePos.x, homePos.y).Top.Texture;
                if (cellTex == "BACKSIDE" || cellTex == null)
                    color = Color.black;
            }

            var count = 1;

            for (var i = 0; i < neighborCheck.Length; i++)
            {
                var id = neighborCheck[i];

                if (!hasNeighbor[id])
                    continue;

                var neighbor = homePos + neighborDirections[id];

                if (data.IsNeighborVertexConnected(homePos, neighbor, neighborDirections[id], vertexId))
                {
                    if (paintEmptyTilesBlack)
                    {
                        var neighborTex = data.Cell(neighbor.x, neighbor.y).Top.Texture;
                        if (neighborTex == "BACKSIDE" || neighborTex == "BLACK" || neighborTex == null)
                            color += Color.black;
                        else
                            color += cellColors[neighbor.x + neighbor.y * data.Width];
                    }
                    else
                        color += cellColors[neighbor.x + neighbor.y * data.Width];

                    count++;
                }

            }

            return color / count;
        }

        private (bool[] hasNeighbros, Vector2Int[] neighbors) GetValidNeighbors(Vector2Int homePos)
        {
            var hasNeighbor = new bool[8];
            var neighbors = new Vector2Int[8];

            var count = 0;

            for (var y = 1; y >= -1; y--)
            {
                for (var x = -1; x <= 1; x++)
                {

                    if (x == 0 && y == 0)
                        continue;

                    var dir = new Vector2Int(x, y);

                    if (!data.Rect.Contains(homePos + dir))
                    {
                        hasNeighbor[count] = false;
                    }
                    else
                    {
                        neighbors[count] = dir;
                        hasNeighbor[count] = true;
                    }

                    count++;
                }
            }

            return (hasNeighbor, neighbors);
        }

        private void AverageCellConnectedNormals(Vector2Int homePos)
        {
            var (hasNeighbor, neighbors) = GetValidNeighbors(homePos);

            var vertsOut = new Vector3[4];
            var normal = cellNormals[homePos.x + homePos.y * data.Width];

            //Build the normals
            vertsOut[0] = AverageNormal(hasNeighbor, neighbors, normal, homePos, 0);
            vertsOut[1] = AverageNormal(hasNeighbor, neighbors, normal, homePos, 1);
            vertsOut[2] = AverageNormal(hasNeighbor, neighbors, normal, homePos, 2);
            vertsOut[3] = AverageNormal(hasNeighbor, neighbors, normal, homePos, 3);

            normalData[homePos.x + homePos.y * data.Width] = vertsOut;

            //Build the colors

            var colorsOut = new Color[4];
            //colorsOut[0] = AverageColors(hasNeighbor, neighbors, homePos, 0);
            //colorsOut[1] = AverageColors(hasNeighbor, neighbors, homePos, 1);
            //colorsOut[2] = AverageColors(hasNeighbor, neighbors, homePos, 2);
            //colorsOut[3] = AverageColors(hasNeighbor, neighbors, homePos, 3);

            //colorsOut[0] = data.Cell(homePos).Top.Color;
            //colorsOut[1] = data.Cell(homePos).Top.Color;
            //colorsOut[2] = data.Cell(homePos).Top.Color;
            //colorsOut[3] = data.Cell(homePos).Top.Color;

            colorsOut[0] = hasNeighbor[1] ? data.Cell(homePos + neighbors[1]).Top.Color : Color.white;
            colorsOut[1] = hasNeighbor[2] ? data.Cell(homePos + neighbors[2]).Top.Color : Color.white;
            colorsOut[2] = data.Cell(homePos).Top.Color;
            colorsOut[3] = hasNeighbor[4] ? data.Cell(homePos + neighbors[4]).Top.Color : Color.white;

            for (var i = 0; i < 4; i++)
                colorsOut[i].a = 1f;

            colorData[homePos.x + homePos.y * data.Width] = colorsOut;
        }

        public void SmoothNormals(RectInt area)
        {
            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    AverageCellConnectedNormals(new Vector2Int(x, y));
                }
            }
        }

        public Color[] FourBlackColors()
        {
            var colors = new Color[4];
            for (var i = 0; i < 4; i++)
                colors[i] = Color.black;
            return colors;
        }

        public Color[] GetAveragedRightColors(Vector2Int pos)
        {
            var cell = data.Cell(pos);

            var (hasNeighbor, neighbors) = GetValidNeighbors(pos);

            var colors = new Color[4];

            colors[0] = hasNeighbor[2] ? data.Cell(pos + neighbors[2]).Top.Color : Color.white;
            colors[1] = hasNeighbor[2] ? data.Cell(pos + neighbors[2]).Top.Color : Color.white;
            colors[2] = hasNeighbor[4] ? data.Cell(pos + neighbors[4]).Top.Color : Color.white;
            colors[3] = hasNeighbor[4] ? data.Cell(pos + neighbors[4]).Top.Color : Color.white;

            for (var i = 0; i < 4; i++)
                colors[i].a = 1f;

            return colors;


            //if (paintEmptyTilesBlack)
            //{
            //    if (cell.Right.Texture == null || cell.Right.Texture == "BACKSIDE")
            //        return FourBlackColors();
            //}

            //var color = cell.Right.Color;
            //var colorUp = color;
            //var colorDown = color;

            //var upPos = new Vector2Int(pos.x, pos.y + 1);
            //var downPos = new Vector2Int(pos.x, pos.y - 1);

            //if (data.Rect.Contains(upPos))
            //{
            //    var up = data.Cell(upPos);
            //    if (up.Right != null && up.Right.Enabled)
            //        colorUp = up.Right.Color;
            //}

            //if (data.Rect.Contains(downPos))
            //{
            //    var down = data.Cell(downPos);
            //    if (down.Right != null && down.Right.Enabled)
            //        colorDown = down.Right.Color;
            //}

            //var colors = new Color[4];

            //colors[0] = (color + colorUp) / 2;
            //colors[1] = (color + colorUp) / 2;
            //colors[2] = (color + colorDown) / 2;
            //colors[3] = (color + colorDown) / 2;

            //return colors;
        }

        public Color[] GetAveragedFrontColors(Vector2Int pos)
        {
            var cell = data.Cell(pos);


            var (hasNeighbor, neighbors) = GetValidNeighbors(pos);

            var colors = new Color[4];

            colors[0] = cell.Top.Color;
            colors[1] = hasNeighbor[4] ? data.Cell(pos + neighbors[4]).Top.Color : Color.white;
            colors[2] = cell.Top.Color;
            colors[3] = hasNeighbor[4] ? data.Cell(pos + neighbors[4]).Top.Color : Color.white;

            for (var i = 0; i < 4; i++)
                colors[i].a = 1f;

            return colors;

            //if (paintEmptyTilesBlack)
            //{
            //    if (cell.Front.Texture == null || cell.Front.Texture == "BACKSIDE")
            //        return FourBlackColors();
            //}

            //var color = cell.Front.Color;
            //var colorLeft = color;
            //var colorRight = color;

            //var leftPos = new Vector2Int(pos.x - 1, pos.y);
            //var rightPos = new Vector2Int(pos.x + 1, pos.y);

            //if (data.Rect.Contains(leftPos))
            //{
            //    var left = data.Cell(leftPos);
            //    if (left.Front != null && left.Front.Enabled)
            //        colorLeft = left.Front.Color;
            //}

            //if (data.Rect.Contains(rightPos))
            //{
            //    var right = data.Cell(rightPos);
            //    if (right.Front != null && right.Front.Enabled)
            //        colorRight = right.Front.Color;
            //}

            //var colors = new Color[4];

            //colors[0] = (color + colorLeft) / 2;
            //colors[1] = (color + colorRight) / 2;
            //colors[2] = (color + colorLeft) / 2;
            //colors[3] = (color + colorRight) / 2;

            //return colors;
        }

        public void PrepareTopFaces(RectInt area, float tileSize)
        {
            var cellData = data.GetCellData();
            area.ClampToBounds(data.Rect);

            //build normal verts and normals
            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    var cell = data.Cell(x, y);

                    //var x1 = x - ChunkBounds.xMin;
                    //var y1 = y - ChunkBounds.yMin;

                    var tv = new Vector3[4];

                    if(cell == null)
                        Debug.LogError($"Could not find cell in bounds at {x},{y}");
                    
                    tv[0] = new Vector3((x + 0) * tileSize, cell.Heights[0] * RoMapData.YScale, (y + 1) * tileSize);
                    tv[1] = new Vector3((x + 1) * tileSize, cell.Heights[1] * RoMapData.YScale, (y + 1) * tileSize);
                    tv[2] = new Vector3((x + 0) * tileSize, cell.Heights[2] * RoMapData.YScale, (y + 0) * tileSize);
                    tv[3] = new Vector3((x + 1) * tileSize, cell.Heights[3] * RoMapData.YScale, (y + 0) * tileSize);

                    vertexData[x + y * data.Width] = tv;

                    var normal = VectorHelper.CalcQuadNormal(tv[0], tv[1], tv[2], tv[3]);

                    cellNormals[x + y * data.Width] = normal;

                    var normals = new Vector3[4];
                    for (var i = 0; i < 4; i++)
                        normals[i] = normal;

                    normalData[x + y * data.Width] = normals;

                    cellColors[x + y * data.Width] = cell.Top.Color;
                }
            }
        }

        public void RebuildArea(RectInt area, float tileSize, bool paintEmptyTileColorsBlack)
        {
            paintEmptyTilesBlack = paintEmptyTileColorsBlack;

            PrepareTopFaces(area.ExpandRect(1), tileSize);
            SmoothNormals(area);
        }
#endif
    }
}
