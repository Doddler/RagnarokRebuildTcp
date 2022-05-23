using System.Collections;
using Assets.Scripts.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.MapEditor
{
    public class RoMapWaterChunk : MonoBehaviour
    {
#if UNITY_EDITOR

        public RoMapData MapData;
        public MapWater Water;

        public MeshRenderer MeshRenderer;
        public MeshFilter MeshFilter;
        public Material Material;

        public Mesh Mesh;

        public int WaterTileCount;

        public RectInt ChunkBounds;

        

        public void Initialize(RoMapData mapData, MapWater water, Material material, RectInt bounds)
        {
            ChunkBounds = bounds;
            MapData = mapData;
            Water = water;

            MeshRenderer = gameObject.GetOrAddComponent<MeshRenderer>();
            MeshFilter = gameObject.GetOrAddComponent<MeshFilter>();
            //Material = new Material(material);
            Material = material;

            MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            MeshRenderer.receiveShadows = false;
            MeshRenderer.material = Material;
            

            GameObjectUtility.SetStaticEditorFlags(gameObject, (StaticEditorFlags)0);
            
            gameObject.layer = LayerMask.NameToLayer("Water");
        }

        public void RebuildWaterMesh()
        {
            var m = new MeshBuilder();

            var cellData = MapData.GetCellData();
            var tileCount = 0;

            for (var x = ChunkBounds.xMin; x < ChunkBounds.xMax; x++)
            {
                for (var y = ChunkBounds.yMin; y < ChunkBounds.yMax; y++)
                {
                    var pos = x + y * MapData.Width;
                    if (pos >= cellData.Length)
                        Debug.LogError($"{x} {y} {ChunkBounds} {ChunkBounds.yMax}");
                    var cell = cellData[pos];

                    var max = Mathf.Min(cell.Heights.x, cell.Heights.y, cell.Heights.z, cell.Heights.w) * RoMapData.YScale;
                    if (-Water.Level + (Water.WaveHeight / 5f) - 0.01f < max)
                        continue;

                    var x1 = x - ChunkBounds.xMin;
                    var y1 = y - ChunkBounds.yMin;

                    var w1 = new Vector3((x1 + 0) * 2, -Water.Level, (y1 + 1) * 2);
                    var w2 = new Vector3((x1 + 1) * 2, -Water.Level, (y1 + 1) * 2);
                    var w3 = new Vector3((x1 + 0) * 2, -Water.Level, (y1 + 0) * 2);
                    var w4 = new Vector3((x1 + 1) * 2, -Water.Level, (y1 + 0) * 2);


                    var normal = Vector3.up;

                    var uv1 = new Vector2((x + 0) * 0.25f * 2, (y + 0) * 0.25f * 2);
                    var uv2 = new Vector2((x + 1) * 0.25f * 2, (y + 0) * 0.25f * 2);
                    var uv3 = new Vector2((x + 0) * 0.25f * 2, (y + 1) * 0.25f * 2);
                    var uv4 = new Vector2((x + 1) * 0.25f * 2, (y + 1) * 0.25f * 2);

                    m.StartTriangle();
                    m.AddVertices(new[] { w1, w2, w3, w4 });
                    m.AddNormals(new[] { normal, normal, normal, normal });
                    m.AddUVs(new[] { uv1, uv2, uv3, uv4 });
                    m.AddTriangles(new[] { 0, 1, 3, 3, 2, 0 });

                    tileCount++;
                }
            }

            WaterTileCount = tileCount;

            if (tileCount == 0)
            {

                MeshFilter.sharedMesh = null;
                return;
            }


            Mesh = m.Build($"Water-{ChunkBounds.xMin}-{ChunkBounds.yMin}");
            MeshFilter.sharedMesh = Mesh;
        }
#endif
    }

}