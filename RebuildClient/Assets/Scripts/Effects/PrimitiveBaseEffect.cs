using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    class PrimitiveBaseEffect : MonoBehaviour
    {
        public float Duration;
        public float CurrentPos;

        public GameObject FollowTarget;

        protected bool destroyOnTargetLost;

        protected Material material;

        public int Step;

        protected MeshBuilder mb;

        protected MeshRenderer mr;
        protected MeshFilter mf;

        public EffectPart[] Parts;

        protected Mesh mesh;

        protected float activeDelay = 0f;
        protected float pauseTime = 0f;

        public Action Updater;
        public Action Renderer;


        protected float frameTime;

        protected Vector3[] verts = new Vector3[4];
        protected Vector3[] normals = new Vector3[4];
        protected Color[] colors = new Color[4];
        protected Vector2[] uvs = new Vector2[4];


        public void FollowEntity(GameObject target, bool destroyWithEntity = true)
        {
            FollowTarget = target;
            destroyOnTargetLost = destroyWithEntity;
        }

        public void DelayUpdate(float time)
        {
            activeDelay = time;
        }


        protected void AddTexturedSliceQuad(Vector3 vert1, Vector3 vert2, Vector3 vert3, Vector3 vert4, int pos, int sliceCount, Color c, float scale = 1)
        {
            var uv1 = (pos - 1) / (float)sliceCount;
            var uv2 = pos / (float)sliceCount;

            AddTexturedQuad(vert1, vert2, vert3, vert4, new Vector2(uv1, 1), new Vector2(uv2, 1), new Vector2(uv1, 0), new Vector2(uv2, 0), c, scale);
        }

        
        protected void AddTexturedQuad(Vector3 vert1, Vector3 vert2, Vector3 vert3, Vector3 vert4, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4, Color c, float scale = 1)
    {
            //we get the uvs for a vertical slice for the position pos out of the number of parts used
            
            colors[0] = c;
            colors[1] = c;
            colors[2] = c;
            colors[3] = c;

            verts[0] = vert1 * scale;
            verts[1] = vert2 * scale;
            verts[2] = vert3 * scale;
            verts[3] = vert4 * scale;

            uvs[0] = uv1;
            uvs[1] = uv2;
            uvs[2] = uv3;
            uvs[3] = uv4;

            //completely unused really
            normals[0] = Vector3.up;
            normals[1] = Vector3.up;
            normals[2] = Vector3.up;
            normals[3] = Vector3.up;

            //Debug.Log(uv1 + " + " + uv2);

            mb.AddQuad(verts, normals, uvs, colors);
        }

        protected void OnDestroy()
        {
            if (Parts != null)
                EffectPool.ReturnParts(Parts);
            if (mesh != null)
                EffectPool.ReturnMesh(mesh);
            if (mb != null)
                EffectPool.ReturnMeshBuilder(mb);

            Parts = null;
            mesh = null;
            mb = null;
        }

        protected void Init(int partCount, Material mat)
        {
            mf = gameObject.AddComponent<MeshFilter>();
            mr = gameObject.AddComponent<MeshRenderer>();

            Parts = EffectPool.BorrowParts(partCount);
            mesh = EffectPool.BorrowMesh();
            mb = EffectPool.BorrowMeshBuilder();

            material = mat;
            mr.material = material;

            mf.sharedMesh = mesh;
        }


        public void Update()
        {
            activeDelay -= Time.deltaTime;
            if (activeDelay > 0f)
                return;

            frameTime = 1 / Time.deltaTime;
            //Debug.Log(frameTime);

            if (FollowTarget == null && destroyOnTargetLost)
            {
                Debug.Log(gameObject + " will destroy as it's follower is gone.");
                Destroy(gameObject);
                return;
            }

            if (FollowTarget != null)
                transform.localPosition = FollowTarget.transform.localPosition;

            pauseTime -= Time.deltaTime;
            if (pauseTime < 0)
            {
                CurrentPos += Time.deltaTime;
                Step = Mathf.RoundToInt(CurrentPos / (1 / 60f));
                if (Step > int.MaxValue / 2)
                    Step = 0; //safety in case they are on screen for a very, very long time
            }

            if (CurrentPos > Duration)
            {
                Debug.Log($"{gameObject} will destroy as it's completed it's duration: {CurrentPos} of {Duration}");

                Destroy(gameObject);
                return;
            }

            Updater();
            Renderer();
        }
    }
}
