using Assets.Scripts.MapEditor;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Utility
{
    public class GroundHighlighter : MonoBehaviour
    {
        private MeshFilter mf;
        private MeshRenderer mr;
        private Mesh mesh;
        private Material mat;
        private MeshBuilder mb;
        private RoWalkDataProvider walkData;

        private ServerControllable target;
        private Vector2Int lastPosition;
        private Vector3[] verts;
        private Vector3[] normals;
        private int[] tris;
        private Vector2[] uvs;
        private Color32[] colors;
        private Color color;
        private int size;
        private float time;

        public float MaxTime;

        public static GroundHighlighter Create(ServerControllable targetObject, string texture, Color color, int size = 1)
        {
            var go = new GameObject($"GroundHighlighter {targetObject.name}");
            var highlighter = go.AddComponent<GroundHighlighter>();
            highlighter.size = size;
            highlighter.color = color;
            highlighter.Init(targetObject, texture);
            
            return highlighter;
        }

        public void DisableRenderer()
        {
            mr.enabled = false;
        }

        private void Init(ServerControllable targetObject, string texture)
        {
            target = targetObject;
            mf = gameObject.AddComponent<MeshFilter>();
            mr = gameObject.AddComponent<MeshRenderer>();
            //walkData = CameraFollower.Instance.WalkProvider;

            mat = new Material(Shader.Find("Ragnarok/AdditiveShaderPulse"));
            mat.mainTexture = Resources.Load<Texture2D>(texture);
            mat.color = new Color(0f, 0f, 0f, 0f);
            mat.name = "Highlighter Mat";
            mat.doubleSidedGI = false;
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.enableInstancing = false;
            mat.renderQueue = 2995;

            mr.material = mat;

            MaxTime = float.MaxValue;

            //UpdateMesh(targetObject.Position);
        }

        private void UpdateMesh(Vector2Int pos)
        {
            transform.localPosition = new Vector3(pos.x + 0.5f, 0.08f, pos.y + 0.5f);

            if (walkData == null)
                walkData = CameraFollower.Instance.WalkProvider;
            if (walkData == null)
                return;
            
            var dist = size - 1;
            
            if (mb == null)
            {
                mb = new MeshBuilder();
                verts = new Vector3[4];
                tris = new int[6];
                uvs = new[]
                {
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(0, 0),
                    new Vector2(1, 0)
                };
                normals = new[]
                {
                    Vector3.up,
                    Vector3.up,
                    Vector3.up,
                    Vector3.up,
                };
                colors = new[]
                {
                    (Color32)color,
                    (Color32)color,
                    (Color32)color,
                    (Color32)color,
                };
            }
            else
                mb.Clear();

            var count = 0;
            var step = 1 / (1 + dist * 2f);
            
            for (var x = -dist; x <= dist; x++)
            {
                for (var y = -dist; y <= dist; y++)
                {
                    var cell = walkData.WalkData.Cell(pos + new Vector2Int(x, y));
                    if ((cell.Type & CellType.Walkable) == 0)
                        continue;
                    
                    tris[0] = count;
                    tris[1] = count + 1;
                    tris[2] = count + 2;
                    tris[3] = count + 1;
                    tris[4] = count + 3;
                    tris[5] = count + 2;


                    var posX = (x + dist) * step;
                    var posY = (y + dist) * step;


                    uvs[0] = new Vector2(posX, posY + step);
                    uvs[1] = new Vector2(posX + step, posY + step);
                    uvs[2] = new Vector2(posX, posY);
                    uvs[3] = new Vector2(posX + step, posY);
                    
                    //mb.AddTriangles(tris);
                    //mb.AddUVs(uvs);

                    var offset = new Vector3(x, 0.025f, y);

                    verts[0] = new Vector3(-0.5f, cell.Heights[0] / 5f, +0.5f) + offset;
                    verts[1] = new Vector3(+0.5f, cell.Heights[1] / 5f, +0.5f) + offset;
                    verts[2] = new Vector3(-0.5f, cell.Heights[2] / 5f, -0.5f) + offset;
                    verts[3] = new Vector3(+0.5f, cell.Heights[3] / 5f, -0.5f) + offset;

                    mb.AddQuad(verts, normals, uvs, colors);
                    count++;
                }
            }

            mf.sharedMesh = mb.Build("highlighter");
            mr.material = mat;

            lastPosition = pos;
        }

        public void LateUpdate()
        {
            if (target == null || target.gameObject == null)
            {
                Destroy(gameObject);
                return;
            }

            mr.enabled = true;

            if (time < 0.33f)
                mat.color = new Color(1, 1, 1, time * 3);
            else if (time > MaxTime - 0.33f)
                mat.color = new Color(1, 1, 1, 1f - (time - (MaxTime - 0.33f)) * 3);
            else
                mat.color = Color.white;

            time += Time.deltaTime;

            if (target.transform.localPosition.ToTilePosition() == lastPosition)
                return;

            UpdateMesh(target.transform.position.ToTilePosition());
        }
    }
}