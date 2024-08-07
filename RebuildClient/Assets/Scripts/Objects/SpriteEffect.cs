﻿using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class SpriteEffect : MonoBehaviour
    {
        public RoSpriteData SpriteData;
        public Material OverrideMaterial;
        public AudioClip AudioClip;

        public bool IsLoop;
        public bool UseZTest;
        public bool RandomStart;
        public bool DestroyOnFinish;

        private bool isInit;
        private bool hasStartedAudio;

        private CullingGroup cullingGroup;
        private BoundingSphere boundingSphere;
        private BoundingSphere[] boundingSpheres;

        private GameObject spriteObject;

        protected RoSpriteAnimator Sprite;
        

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
            Sprite = child.AddComponent<RoSpriteAnimator>();
            Sprite.Type = SpriteType.Npc;
            // sprite.State = SpriteState.Dead;
            // sprite.SpriteRenderer = sr;
            Sprite.LockAngle = true;
            Sprite.RaycastForShadow = false;
            Sprite.State = SpriteState.Idle;
            Sprite.OnSpriteDataLoadNoCollider(SpriteData);
            Sprite.ChangeActionExact(0);

            if (DestroyOnFinish)
            {
                Sprite.DisableLoop = true;
                Sprite.OnFinishAnimation = FinishPlaying;
            }
            
            
            if(OverrideMaterial != null)
                Sprite.SpriteRenderer.SetOverrideMaterial(OverrideMaterial);
            
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

        private void FinishPlaying()
        {
            Destroy(gameObject);
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

        public virtual void UpdateSpriteEffect()
        {
            
        }

        public void Update()
        {
            if (!isInit)
                return;


            if (AudioClip != null && !hasStartedAudio)
            {
                Debug.LogWarning(transform.position + " " + transform.localPosition);
                AudioManager.Instance.OneShotSoundEffect(-1, AudioClip, transform.localPosition);
                hasStartedAudio = true;
            }

#if UNITY_EDITOR
            if (CameraFollower.Instance.CinemachineMode)
            {
                spriteObject.SetActive(true);
                UpdateSpriteEffect();
                return;
            }
#endif
            
            if (IsLoop)
                spriteObject.SetActive(cullingGroup.IsVisible(0));
            
            UpdateSpriteEffect();
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