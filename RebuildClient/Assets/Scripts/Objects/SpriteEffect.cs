using System;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityStandardAssets._2D;

namespace Assets.Scripts.Objects
{
    public class SpriteEffect : MonoBehaviour
    {
        public RoSpriteData SpriteData;
        public Material OverrideMaterial;

        public bool IsLoop;
        public bool UseZTest;
        public bool RandomStart;

        private bool isInit;

        private CullingGroup cullingGroup;
        private BoundingSphere boundingSphere;
        private BoundingSphere[] boundingSpheres;

        private GameObject spriteObject;
        
        

        public void Initialize()
        {
            var billboard = gameObject.AddComponent<BillboardObject>();
            billboard.Style = BillboardStyle.Character;
            
            var child = new GameObject("Sprite");
            child.layer = LayerMask.NameToLayer("Characters");
            child.transform.SetParent(gameObject.transform, false);
            child.transform.localPosition = Vector3.zero;
            //
            // var sr = child.AddComponent<RoSpriteRendererStandard>();
            // sr.SecondPassForWater = false;
            // sr.UpdateAngleWithCamera = false;
            //
            var sprite = child.AddComponent<RoSpriteAnimator>();
            sprite.Type = SpriteType.Npc;
            // sprite.State = SpriteState.Dead;
            // sprite.SpriteRenderer = sr;
            sprite.LockAngle = true;
            sprite.State = SpriteState.Idle;
            sprite.OnSpriteDataLoadNoCollider(SpriteData);
            sprite.ChangeActionExact(0);
            
            if(OverrideMaterial != null)
                sprite.SpriteRenderer.SetOverrideMaterial(OverrideMaterial);
            
            //sprite.LockAngle = true;
            

            spriteObject = child;
            
            if (IsLoop)
            {
                cullingGroup = new CullingGroup();
                cullingGroup.targetCamera = Camera.main;
                boundingSpheres = new BoundingSphere[1];
                boundingSphere = new BoundingSphere(transform.position, 20f);
                boundingSpheres[0] = boundingSphere;
                cullingGroup.SetBoundingSpheres(boundingSpheres);
                cullingGroup.SetBoundingSphereCount(1);
            }
        }
        
        private void Awake()
        {
            if (SpriteData != null)
                Initialize();

            isInit = true;
            //
            // AudioSource = GetComponent<AudioSource>();
            // if (AudioSource != null && AudioSource.clip != null)
            //     hasAudio = true;
        }

        public void Update()
        {
            if (!isInit)
                return;
            
            #if UNITY_EDITOR
            if (CameraFollower.Instance.CinemachineMode)
            {
                spriteObject.SetActive(true);
                return;
            }
#endif
            
            if (IsLoop)
                spriteObject.SetActive(cullingGroup.IsVisible(0));
        }
        
        void OnDestroy()
        {
            if (!isInit)
                return;
            
            if(cullingGroup != null)
                cullingGroup.Dispose();
        }
    }
}