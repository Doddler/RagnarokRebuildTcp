using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Scripts.Objects
{
    public class SpriteEffect : MonoBehaviour
    {
        public RoSpriteData SpriteData;
        public Material OverrideMaterial;
        public AudioClip AudioClip;

        public Color SpriteColor = Color.white;

        public bool IsLoop;
        public bool UseZTest;
        public bool RandomStart;
        public bool DestroyOnFinish;
        public float Duration;
        public bool DestroyAtEndOfDuration;

        private bool isInit;
        private bool hasStartedAudio;
        
        private CullingGroup cullingGroup;
        private BoundingSphere boundingSphere;
        private BoundingSphere[] boundingSpheres;

        private GameObject spriteObject;

        [FormerlySerializedAs("Sprite")] public RoSpriteAnimator SpriteAnimator;
        

        public void Initialize(bool useBillboard = true)
        {
            if (useBillboard)
            {
                var billboard = gameObject.AddComponent<BillboardObject>();
                billboard.Style = BillboardStyle.Character;
            }

            var child = new GameObject("Sprite");
            child.layer = LayerMask.NameToLayer("Characters");
            child.transform.SetParent(gameObject.transform, false);
            child.transform.localPosition = Vector3.zero;
            //
            // var sr = child.AddComponent<RoSpriteRendererStandard>();
            // sr.SecondPassForWater = false;
            // sr.UpdateAngleWithCamera = false;
            //
            SpriteAnimator = child.AddComponent<RoSpriteAnimator>();
            SpriteAnimator.Type = SpriteType.Npc;
            // sprite.State = SpriteState.Dead;
            // sprite.SpriteRenderer = sr;
            SpriteAnimator.LockAngle = true;
            SpriteAnimator.RaycastForShadow = false;
            SpriteAnimator.State = SpriteState.Idle;
            SpriteAnimator.BaseColor = SpriteColor;
            SpriteAnimator.OnSpriteDataLoadNoCollider(SpriteData);
            SpriteAnimator.ChangeActionExact(0);

            if (DestroyOnFinish)
            {
                SpriteAnimator.DisableLoop = true;
                SpriteAnimator.OnFinishAnimation = FinishPlaying;
            }
            
            
            if(OverrideMaterial != null)
                SpriteAnimator.SpriteRenderer.SetOverrideMaterial(OverrideMaterial);
            
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

            if (DestroyAtEndOfDuration)
            {
                Duration -= Time.deltaTime;
                if (Duration < 0)
                    FinishPlaying();
                return;
            }

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
            
            if (IsLoop && cullingGroup != null)
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