using System;
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

        public static GroundHighlighter Create(ServerControllable targetObject, string texture)
        {
            var go = new GameObject($"GroundHighlighter {targetObject.name}");
            var highlighter = go.AddComponent<GroundHighlighter>();
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
            
            mat = new Material(Shader.Find("Unlit/WalkableShader"));
            mat.SetFloat("_Glossiness", 0f);
            mat.mainTexture = Resources.Load<Texture2D>(texture);
            mat.color = Color.white;
            mat.name = "Cursor Mat";
            mat.doubleSidedGI = false;
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.enableInstancing = false;
            
            //UpdateMesh(targetObject.Position);
        }
        
        
        private void UpdateMesh(Vector2Int pos)
        {
            transform.localPosition = new Vector3(pos.x + 0.5f, 0, pos.y + 0.5f);

            if(walkData == null)
                walkData = CameraFollower.Instance.WalkProvider;
            if (walkData == null)
                return;
            
            var cell = walkData.WalkData.Cell(pos);

            if (mb == null)
            {
                mb = new MeshBuilder();
                mb.AddTriangles(new[] { 0, 1, 2, 1, 3, 2 });
                mb.AddUVs(new[]
                {
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(0, 0),
                    new Vector2(1, 0)
                });
                verts = new Vector3[4];
            }
            else
                mb.ClearVertex();
            
            var offset = new Vector3(0f, 0.025f, 0f);
            
            verts[0] = new Vector3(-0.5f, cell.Heights[0] / 5f, +0.5f) + offset;
            verts[1] = new Vector3(+0.5f, cell.Heights[1] / 5f, +0.5f) + offset;
            verts[2] = new Vector3(-0.5f, cell.Heights[2] / 5f, -0.5f) + offset;
            verts[3] = new Vector3(+0.5f, cell.Heights[3] / 5f, -0.5f) + offset;
            
            mb.AddVertices(verts);

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

            mr.enabled = CameraFollower.Instance.DebugVisualization;

            if (target.transform.localPosition.ToTilePosition() == lastPosition)
                return;
            
            UpdateMesh(target.transform.position.ToTilePosition());
            
        }
    }
}