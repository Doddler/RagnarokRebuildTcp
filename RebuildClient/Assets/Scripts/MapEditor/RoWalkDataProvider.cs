using Assets.Scripts.Utility;
using RebuildSharedData.Config;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Assets.Scripts.MapEditor
{
    public class RoWalkDataProvider : MonoBehaviour
    {
        public RagnarokWalkData WalkData;
        private Texture2D gridIcon;
        private Texture2D gridIconYellow;

        private MeshFilter mf;
        private MeshRenderer mr;
        private Mesh mesh;
        private Material mat;

        private Vector3[] vertices;
        private Vector2[] uvs;
        private int[] triangles;

        private Vector2Int cursorTarget;

        private static RoWalkDataProvider instance;

        public static RoWalkDataProvider Instance
        {
            get
            {
                if (instance != null)
                    return instance;
                instance = GameObject.FindObjectOfType<RoWalkDataProvider>();
                return instance;
            }
        }

        public void Awake()
        {
            mat = new Material(Shader.Find("Unlit/WalkableShader"));
            mat.SetFloat("_Glossiness", 0f);
            mat.mainTexture = gridIcon;
            mat.color = Color.white;
            mat.name = "Cursor Mat";
            mat.doubleSidedGI = false;
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.enableInstancing = false;

            mf = gameObject.AddComponent<MeshFilter>();
            mr = gameObject.AddComponent<MeshRenderer>();
            mr.material = mat;
            mat.renderQueue = 3000 + 4;

            var loader = Addressables.LoadAssetAsync<Texture2D>("gridicon");
            loader.Completed += ah =>
            {
                gridIcon = ah.Result;
                mat.mainTexture = gridIcon;
            };


            var loaderYellow = Addressables.LoadAssetAsync<Texture2D>("gridiconYellow");
            loaderYellow.Completed += ah =>
            {
                gridIconYellow = ah.Result;
                //mat.mainTexture = gridIcon;
            };

        }

        public void DisableRenderer()
        {
            mr.enabled = false;
        }

        public void UpdateCursorPosition(Vector3 playerPosition, Vector3 targetPosition, bool hasValidPath)
        {
            var exists = GetClosestTileTopToPoint(targetPosition, out var target);
            if (!exists)
            {
                //Debug.Log("Not in grid");
                DisableRenderer();
                return;
            }

            if (hasValidPath)
                mat.mainTexture = gridIcon;
            else
                mat.mainTexture = gridIconYellow;

            if (target == cursorTarget)
            {
                //Debug.Log("Position not changed");
                return;
            }

            var cell = WalkData.Cell(target);
            if (!cell.Type.HasFlag(CellType.Walkable))
            {
                //Debug.Log("Cell not walkable: " + cell.Type);
                DisableRenderer();
                return;
            }

            if (vertices == null)
            {
                mesh = new Mesh();
                vertices = new Vector3[4];
                uvs = new Vector2[4];
                triangles = new[] { 0, 1, 2, 1, 3, 2 }; ;
            }
            else
                mesh.Clear();

            //var verts = new Vector3[4];
            //var uvs = new Vector2[4];
            //var tris = new [] {0, 1, 2, 1, 3, 2};

            var offset = new Vector3(0f, 0.015f, 0f);

            vertices[0] = new Vector3(target.x, cell.Heights[0] / 5f, target.y + 1) + offset;
            vertices[1] = new Vector3(target.x + 1, cell.Heights[1] / 5f, target.y + 1) + offset;
            vertices[2] = new Vector3(target.x, cell.Heights[2] / 5f, target.y) + offset;
            vertices[3] = new Vector3(target.x + 1, cell.Heights[3] / 5f, target.y) + offset;

            uvs[0] = new Vector2(0, 1);
            uvs[1] = new Vector2(1, 1);
            uvs[2] = new Vector2(0, 0);
            uvs[3] = new Vector2(1, 0);

            //var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            mf.sharedMesh = mesh;
            mr.enabled = true;
        }

        public bool IsCellWalkable(Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= (WalkData.Width) || cell.y < 0 || cell.y >= (WalkData.Height))
                return false;

            return WalkData.CellWalkable(cell.x, cell.y);
        }

        public Vector2Int ClampAreaToMap(Vector2Int v)
        {
            return new Vector2Int(Mathf.Clamp(v.x, 0, WalkData.Width), Mathf.Clamp(v.y, 0, WalkData.Height));
        }

        public bool IsPositionNearWater(Vector3 position, int area)
        {
            var hasTop = GetClosestTileTopToPoint(position, out var tile);
            if (!hasTop)
                return true; //better safe than sorry

            var waterAnimator = RoWaterAnimator.Instance;
            if (waterAnimator == null || waterAnimator.Water == null)
                return false;

            var water = waterAnimator.Water;

            for (var x = tile.x - area; x < tile.x + area; x++)
            {
                for (var y = tile.y - area; y < tile.y + area; y++)
                {
                    if (x < 0 || x >= WalkData.Width || y < 0 || y >= WalkData.Height)
                        continue;

                    var cell = WalkData.Cell(new Vector2Int(x, y));

                    var max = Mathf.Min(cell.Heights.x, cell.Heights.y, cell.Heights.z, cell.Heights.w) * RoMapData.YScale;
                    if (-water.Level + (water.WaveHeight / 5f) - 0.01f < max)
                        continue;

                    //Debug.Log($"{gameObject.name}: Scan for water around map position {tile} range {area} returned water.");

                    return true;
                }
            }

            //Debug.Log($"{gameObject.name}: Scan for water around map position {tile} range {area} returned no water.");

            return false;
        }

        public float GetHeightForPosition(Vector3 position)
        {
            var hasTop = GetClosestTileTopToPoint(position, out var tile);
            if (!hasTop)
                return position.y;

            var realX = position.x - tile.x;
            var realY = position.z - tile.y;

            var h = WalkData.Cell(tile).Heights;

            var v1 = h[0] * (1 - realX) * (realY);
            var v2 = h[1] * (realX) * (realY);
            var v3 = h[2] * (1 - realX) * (1 - realY);
            var v4 = h[3] * (realX) * (1 - realY);

            //Debug.Log($"Tile {tile} ({realX},{realY} heights {h} contributions {v1} {v2} {v3} {v4}");

            return (v1 + v2 + v3 + v4) * 0.2f + 0.05f;
        }

        public Vector3 GetWorldPositionForTile(Vector2Int tile)
        {
            var realPos = new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);
            return new Vector3(realPos.x, GetHeightForPosition(realPos), realPos.z);
        }

        public Vector2Int GetTilePositionForPoint(Vector3 point)
        {
            var position = transform.position;
            var x = Mathf.FloorToInt((point.x - position.x));
            var y = Mathf.FloorToInt((point.z - position.z));

            return new Vector2Int(x, y);
        }

        public bool GetClosestTileTopToPoint(Vector3 point, out Vector2Int tile)
        {
            tile = new Vector2Int();

            var position = transform.position;
            var x = Mathf.FloorToInt((point.x - position.x));
            var y = Mathf.FloorToInt((point.z - position.z));

            //Debug.Log(WalkData.Width + " " + WalkData.Height + " " + x + " " + y);

            if (x < 0 || x >= (WalkData.Width) || y < 0 || y >= (WalkData.Height))
                return false;

            tile = new Vector2Int(x, y);
            return true;
        }

        private Vector2Int GetClosestInRangePoint(Vector2Int position, Vector2Int player, int maxDistance)
        {
            var diff = (position - player);
            if (diff.x > maxDistance && diff.y > maxDistance)
                return player + new Vector2Int(maxDistance, maxDistance);
            if (diff.x < -maxDistance && diff.y > maxDistance)
                return player + new Vector2Int(-maxDistance, maxDistance);
            if (diff.x > maxDistance && diff.y < -maxDistance)
                return player + new Vector2Int(maxDistance, -maxDistance);
            if (diff.x < -maxDistance && diff.y < -maxDistance)
                return player + new Vector2Int(-maxDistance, -maxDistance);
            if (diff.x > maxDistance)
                return new Vector2Int(player.x + maxDistance, position.y);
            if (diff.x < -maxDistance)
                return new Vector2Int(player.x - maxDistance, position.y);
            if (diff.y > maxDistance)
                return new Vector2Int(position.x, player.y + maxDistance);
            if (diff.y < -maxDistance)
                return new Vector2Int(position.x, player.y - maxDistance);

            //shouldn't happen
            return player;
        }

        public bool GetNextWalkableTileForClick(Vector2Int start, Vector2Int dest, out Vector2Int modifiedPosition)
        {
            modifiedPosition = start;

            //we'll assume we can't walk on start, since this will only get called if the normal check fails

            //Debug.Log($"Finding walkable {start} to {dest}");
            if (start == dest)
            {
                modifiedPosition = dest;
                return true;
            }

            var next = dest;

            if ((start - dest).SquareDistance() >= SharedConfig.MaxPathLength)
            {
                next = GetClosestInRangePoint(dest, start, SharedConfig.MaxPathLength - 1);

                if ((WalkData.Cell(next).Type & CellType.Walkable) != 0)
                {
                    //Debug.Log("Found closer path!?");
                    modifiedPosition = next;
                    return true;
                }
            }

            //step them in a very nieve fashion towards the target cell until we hit a wall
            while (next != start)
            {
                if (start.x < next.x)
                    next.x--;
                if (start.x > next.x)
                    next.x++;
                if (start.y < next.y)
                    next.y--;
                if (start.y > next.y)
                    next.y++;

                if ((WalkData.Cell(next).Type & CellType.Walkable) != 0)
                {
                    if (next == dest) return false; //it's not valid if we don't move
                    
                    modifiedPosition = next;
                    return true;
                }
            }

            Debug.Log("Failed :(");

            return false;
        }

        public bool GetMapPositionForWorldPosition(Vector3 position, out Vector2Int outPosition)
        {
            outPosition = Vector2Int.zero;

            if (GetClosestTileTopToPoint(position, out outPosition))
            {
                return true;
            }

            return false;
        }

        public bool GetPositionForClick(Vector3 position, out Vector3 modifiedPosition)
        {
            modifiedPosition = position;

            if (GetClosestTileTopToPoint(position, out var t))
            {
                var cell = WalkData.Cell(t);
                if ((cell.Type & CellType.Walkable) == 0)
                    return false;
                modifiedPosition = new Vector3(t.x + 0.5f, position.y, t.y + 0.5f);
                return true;
            }

            return false;
        }
    }
}
