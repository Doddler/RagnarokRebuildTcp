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


        public void FollowEntity(GameObject target, bool destroyWithEntity = true)
        {
            FollowTarget = target;
            destroyOnTargetLost = destroyWithEntity;
        }

        public void DelayUpdate(float time)
        {
            activeDelay = time;
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
