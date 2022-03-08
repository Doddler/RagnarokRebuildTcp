using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Effects;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Sprites
{
    public class RoSpriteAnimator : MonoBehaviour
    {
        public RoSpriteData SpriteData;
        public Direction Direction;
        public SpriteType Type;
        public SpriteState State;
        public float AnimSpeed = 1;
        public float SpriteOffset = 0;
        public int SpriteOrder = 0;

        public Color Color;
        public float Alpha;

        private static bool nextUseSmoothRender = true;
        private static bool canUpdateRenderer = false;

        public List<RoSpriteAnimator> ChildrenSprites = new List<RoSpriteAnimator>();
        public List<SortingGroup> BattleZSwapGroups = new List<SortingGroup>();

        public RoSpriteAnimator Parent;
        public GameObject Shadow;
        public SortingGroup ShadowSortingGroup { get; set; }

        public AudioSource AudioSource;

        public bool IgnoreAnchor = false;
        public bool IsHead = false;
        public HeadFacing HeadFacing;

        public bool SetAsDirty;
        //these just exist so you can trigger them in the editor without getting a message from the server
        public bool Idle;
        public bool Attack;
        public bool Standby;
        public bool Walk;
        public bool Hit;
        public bool Sit;
        public bool Dead;

        private Dictionary<int, Mesh> meshCache;
        private Dictionary<int, Mesh> colliderCache;

        public ServerControllable Controllable;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public MeshCollider MeshCollider;
        public SortingGroup SortingGroup;

        private GameObject[] layers;
        private SpriteRenderer[] sprites;
        private bool isInitialized;
        private int layerCount;

        private string spriteName;

        private Light directionalLight;

        public float CurrentShade = 1f;
        public float TargetShade;
        public float ShadeLevel = 0.7f;

        public bool LockAngle;

        public SpriteMotion CurrentMotion;

        private RoAction currentAction;
        private int currentActionIndex;
        private int currentAngleIndex;

        private float currentFrameTime = 0;
        private int currentFrame = 0;
        private int maxFrame = 0;
        private bool isLooping;
        private bool isPaused;
        private bool isDirty;

        private Material mat;
        private Material mat2;
        private Material[] materialArray;

        public Color CurrentColor => mat.color;

        //private float deadResetTime = 0;

        private float rotate = 0;
        private bool doSpin = false;
        private float spinSpeed = 100f;

        private Shader shader;

        public bool IsInitialized => isInitialized;

        private Camera mainCamera;

        public bool IsAttackMotion => CurrentMotion == SpriteMotion.Attack1 || CurrentMotion == SpriteMotion.Attack2 ||
                                      CurrentMotion == SpriteMotion.Attack3;

        public void SetDirty()
        {
            isDirty = true;
        }

        public void DoSpin()
        {
            spinSpeed = Random.Range(240f, 640f);

            var megaSpin = Random.Range(0, 100);
            if (megaSpin == 10)
                spinSpeed = Random.Range(640f, 6400f);

            var dir = Random.Range(0, 2);
            if (dir == 1)
                spinSpeed *= -1;

            spinSpeed *= 2f;

            doSpin = true;
        }

        public Vector2 GetAnimationAnchor()
        {
            var frame = currentAction.Frames[currentFrame];
            if (frame.Pos.Length > 0)
                return frame.Pos[0].Position;
            if (IsHead && (State == SpriteState.Idle || State == SpriteState.Sit))
                return frame.Pos[currentFrame].Position;
            return Vector2.zero;
        }

        public void OnSpriteDataLoad(RoSpriteData spriteData)
        {
            if (spriteData == null)
                throw new Exception("AAAAAAA");
            //Debug.Log("Loaded sprite data for sprite " + spriteData.Name);
            SpriteData = spriteData;
            Initialize();

            if (currentAction == null)
                ChangeAction(0);
        }

        public void Initialize(bool makeCollider = true)
        {
            if (isInitialized)
                return;
            if (Parent != null && !Parent.IsInitialized)
                return;
            if (gameObject == null)
                return;
            if (SpriteData == null)
                return;

            Type = SpriteData.Type;

            var parent = gameObject.transform.parent;

            if (parent == null)
            {

                //var bb = gameObject.AddComponent<Billboard>();
            }
            else
            {
                Parent = parent.gameObject.GetComponent<RoSpriteAnimator>();
                if (Parent != null)
                    Controllable = Parent.Controllable;
            }

            MeshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer = gameObject.AddComponent<MeshRenderer>();

            MeshRenderer.sortingOrder = SpriteOrder;
            if (makeCollider)
                MeshCollider = gameObject.AddComponent<MeshCollider>();

            SortingGroup = gameObject.GetOrAddComponent<SortingGroup>();

            MeshRenderer.receiveShadows = false;
            MeshRenderer.lightProbeUsage = LightProbeUsage.Off;
            MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            if (shader == null)
                shader = ShaderCache.Instance?.SpriteShader;

            if (shader == null)
                Debug.LogError("Could not find shader Unlit/TestSpriteShader");

            mat = new Material(shader);
            mat.renderQueue -= 2;
            mat.EnableKeyword("WATER_BELOW");
            mat.mainTexture = SpriteData.Atlas;
            
            if (Mathf.Approximately(0, SpriteOffset))
                mat.SetFloat("_Offset", SpriteData.Size / 125f);
            else
                mat.SetFloat("_Offset", SpriteOffset);

            materialArray = new Material[2];
            
            materialArray[0] = mat;

            mat2 = new Material(shader);
            mat2.EnableKeyword("WATER_ABOVE");
            mat2.mainTexture = SpriteData.Atlas;

            if (Mathf.Approximately(0, SpriteOffset))
                mat2.SetFloat("_Offset", SpriteData.Size / 125f);
            else
                mat2.SetFloat("_Offset", SpriteOffset);

            materialArray[1] = mat2;

            MeshRenderer.sharedMaterials = materialArray;

            SpriteData.Atlas.filterMode = !nextUseSmoothRender ? FilterMode.Bilinear : FilterMode.Point;


            if (Parent != null)
                mat.SetFloat("_Offset", Parent.SpriteData.Size / 125f);

            if (AudioSource == null && Parent == null)
            {
                var channel = AudioManager.Instance.Mixer.FindMatchingGroups("Sounds")[0];
                AudioSource = gameObject.AddComponent<AudioSource>();
                AudioSource.spatialBlend = 0.7f;
                AudioSource.priority = 60;
                AudioSource.maxDistance = 40;
                AudioSource.rolloffMode = AudioRolloffMode.Linear;
                AudioSource.volume = 1f;
                AudioSource.dopplerLevel = 0;
                AudioSource.outputAudioMixerGroup = channel;
            }

            if (Type == SpriteType.Player && State == SpriteState.Idle)
                isPaused = true;

            if (ChildrenSprites.Count > 0)
            {
                foreach (var child in ChildrenSprites)
                    child.Initialize();
            }

            meshCache = SpriteMeshCache.GetMeshCacheForSprite(SpriteData.Name);
            colliderCache = SpriteMeshCache.GetColliderCacheForSprite(SpriteData.Name);
            spriteName = SpriteData.Name;

            Color = Color.white;
            Alpha = 1;

            isInitialized = true;

            ChangeMotion(CurrentMotion, true);

            if (parent == null)
            {
                UpdateShade();
                CurrentShade = TargetShade; //skip fade in at first spawn
            }

            isDirty = true;
        }
        
        private Mesh GetColliderForFrame()
        {
            var id = ((currentActionIndex + currentAngleIndex) << 8) + currentFrame;

            if (colliderCache.TryGetValue(id, out var mesh))
                return mesh;

            //Debug.Log("Building new mesh for " + name);

            var newMesh = SpriteMeshBuilder.BuildColliderMesh(SpriteData, currentActionIndex, currentAngleIndex, currentFrame);

            colliderCache.Add(id, newMesh);

            return newMesh;
        }

        public Mesh GetMeshForFrame()
        {
            var id = ((currentActionIndex + currentAngleIndex) << 8) + currentFrame;

            if (meshCache == null)
            {
                Debug.Log("Meshcache is not initialized! But how? isInitialized status is " + isInitialized);
            }

            if (meshCache.TryGetValue(id, out var mesh))
                return mesh;

            //Debug.Log("Building new mesh for " + name);

            var newMesh = SpriteMeshBuilder.BuildSpriteMesh(SpriteData, currentActionIndex, currentAngleIndex, currentFrame);

            meshCache.Add(id, newMesh);

            return newMesh;
        }


        public void OnDrawGizmos()
        {
            //if (Parent != null)
            // return; //don't draw gizmo if we're parented to someone else.
            //   var pos = RoAnimationHelper.FacingDirectionToVector(Direction);
            //   Gizmos.DrawLine(transform.position, transform.position + new Vector3(pos.x, 0, pos.y));
        }

        public float GetHitTiming()
        {
            var motionId = RoAnimationHelper.GetMotionIdForSprite(Type, SpriteMotion.Hit);
            var time = SpriteData.Actions[motionId].Delay;
            var frames = SpriteData.Actions[motionId].Frames.Length;
            return (time * frames) / 1000f;
        }

        public float GetDeathTiming()
        {
            var motionId = RoAnimationHelper.GetMotionIdForSprite(Type, SpriteMotion.Dead);
            var time = SpriteData.Actions[motionId].Delay;
            var frames = SpriteData.Actions[motionId].Frames.Length;
            return (time * frames) / 1000f;
        }

        public void UpdateSpriteFrame()
        {
            if (currentFrame >= currentAction.Frames.Length)
            {
                Debug.LogWarning($"Current frame is {currentFrame}, max frame is {maxFrame}, but actual frame max is {currentAction.Frames.Length}");
                return;
            }
            var frame = currentAction.Frames[currentFrame];

            if (frame.Sound > -1 && frame.Sound < SpriteData.Sounds.Length && !isPaused)
            {
                var sound = SpriteData.Sounds[frame.Sound];
                if (sound != null && AudioSource != null)
                {
                    AudioSource.clip = sound;
                    AudioSource.Play();
                }
            }

            var mesh = GetMeshForFrame();
            var cMesh = GetColliderForFrame();

            MeshFilter.sharedMesh = null;
            MeshFilter.sharedMesh = mesh;
            if (MeshCollider != null)
            {
                MeshCollider.sharedMesh = null;
                MeshCollider.sharedMesh = cMesh;
            }

            //Debug.Log("Updating sprite frame!");
        }

        public void ChangeAngle(int newAngleIndex)
        {
            currentAngleIndex = newAngleIndex;
            if (!isInitialized)
                return;
            currentAction = SpriteData.Actions[currentActionIndex + currentAngleIndex];
            maxFrame = currentAction.Frames.Length - 1;
            isDirty = true;
        }

        public void ChangeAction(int newActionIndex)
        {
            currentActionIndex = newActionIndex;
            if (!isInitialized)
                return;
            currentAction = SpriteData.Actions[currentActionIndex + currentAngleIndex];
            maxFrame = currentAction.Frames.Length - 1;
            currentFrameTime = currentAction.Delay / 1000f * AnimSpeed; //reset current frame time
            isDirty = true;
        }

        public void ChangeMotion(SpriteMotion nextMotion, bool forceUpdate = false)
        {
            //Debug.Log($"{name} state {State} change motion from {CurrentMotion} to {nextMotion}");
            
            if(CurrentMotion == SpriteMotion.Dead && !forceUpdate)
                Debug.LogWarning("Changing from dead to something else!");

            if (CurrentMotion == nextMotion && !forceUpdate)
                return;

            CurrentMotion = nextMotion;
            currentFrame = 0;

            if (!isInitialized)
                return;

            var action = RoAnimationHelper.GetMotionIdForSprite(Type, nextMotion);
            if (action < 0 || action > SpriteData.Actions.Length)
                action = 0;
            ChangeAction(action);
            isPaused = false;
            isDirty = true;

            if (Type == SpriteType.Player)
            {
                if (CurrentMotion == SpriteMotion.Idle || CurrentMotion == SpriteMotion.Sit)
                {
                    currentFrame = (int)HeadFacing;
                    UpdateSpriteFrame();
                    isPaused = true;
                }
                else
                    HeadFacing = HeadFacing.Center;
            }

            if (Shadow != null)
            {
                if (CurrentMotion == SpriteMotion.Sit || CurrentMotion == SpriteMotion.Dead)
                    Shadow.SetActive(false);
                else
                    Shadow.SetActive(true);
            }
        }

        public void SetHeadFacing(HeadFacing facing)
        {
            if (CurrentMotion != SpriteMotion.Sit && CurrentMotion != SpriteMotion.Idle)
                return;

            HeadFacing = facing;
            currentFrame = (int)HeadFacing;
            isDirty = true;
        }

        public void AdvanceFrame()
        {
            if (!isPaused)
                currentFrame++;
            if (currentFrame > maxFrame)
            {
                var nextMotion = RoAnimationHelper.GetMotionForState(State);
                if (nextMotion != CurrentMotion)
                    ChangeMotion(nextMotion);
                else
                {
                    if (State != SpriteState.Dead)
                        currentFrame = 0;
                    else
                    {
                        currentFrame = maxFrame;
                        isPaused = true;
                    }
                }
            }

            if (currentFrameTime < 0)
                currentFrameTime += (float)currentAction.Delay / 1000f * AnimSpeed;

            if (!isPaused)
                isDirty = true;
        }

        public void ChildSetFrameData(int actionIndex, int angleIndex, int newCurrentFrame)
        {
            currentActionIndex = actionIndex;
            currentAngleIndex = angleIndex;

            if (!isInitialized)
                return;

            currentAction = SpriteData.Actions[currentActionIndex + currentAngleIndex];
            currentFrame = newCurrentFrame;
            UpdateSpriteFrame();
            ChildUpdate();
        }

        public void ChildUpdate()
        {
            if (!IgnoreAnchor)
            {
                var parentAnchor = Parent.GetAnimationAnchor();
                var ourAnchor = GetAnimationAnchor();

                var diff = parentAnchor - ourAnchor;

                transform.localPosition = new Vector3(diff.x / 50f, -diff.y / 50f, 0f);
            }
        }

        private int GetFrameForHeadTurn(RoSpriteAnimator animator)
        {
            if (!animator.IsHead)
                return 0;

            if (HeadFacing == HeadFacing.Right)
                return 1;

            if (HeadFacing == HeadFacing.Left)
                return 2;

            return 0;
        }

        private void UpdateShade()
        {
            if (directionalLight == null)
            {
                directionalLight = GameObject.Find("DirectionalLight").GetComponent<Light>();
                var lightPower = (directionalLight.color.r + directionalLight.color.g + directionalLight.color.b) / 3f;
                lightPower = (lightPower * directionalLight.intensity + 1) / 2f;
                lightPower *= directionalLight.shadowStrength;
                ShadeLevel = 1f - (0.35f * lightPower);

            }

            var srcPos = transform.position + new Vector3(0, 0.5f, 0);

            var destDir = directionalLight.transform.rotation * Vector3.forward * -1;

            //Debug.Log(destDir + " : " + srcPos);
            var mask = ~LayerMask.GetMask("Characters");

            var ray = new Ray(srcPos, destDir);

            //Debug.DrawLine(srcPos, destDir, Color.yellow, 10f, false);
            //Debug.DrawRay(srcPos, destDir, Color.yellow, 100f, false);

            if (Physics.Raycast(ray, out var hit, 50f, mask))
            {
                //Debug.Log(hit.transform.gameObject);
                TargetShade = ShadeLevel;
            }
            else
                TargetShade = 1f;
        }
        
        public void UpdateColor()
        {
            var c = new Color(Color.r * CurrentShade, Color.g * CurrentShade, Color.b * CurrentShade, Alpha);

            mat.color = c;
            mat2.color = c;
        }

        public void UpdateChildColor()
        {
            var c = new Color(Parent.Color.r * Parent.CurrentShade, Parent.Color.g * Parent.CurrentShade, Parent.Color.b * Parent.CurrentShade, Parent.Alpha);

            mat.color = c;
            mat2.color = c;
        }

        public void LateUpdate()
        {
            if (canUpdateRenderer)
            {
                nextUseSmoothRender = !nextUseSmoothRender;
                canUpdateRenderer = false;
            }

            if (SortingGroup == null || Parent != null)
                return;
            if (mainCamera == null)
                mainCamera = Camera.main;

            var screenPos = Camera.main.WorldToScreenPoint(transform.position);
            screenPos = new Vector3(
                Mathf.Clamp(screenPos.x, -100, mainCamera.pixelWidth + 100),
                Mathf.Clamp(screenPos.y, -100, mainCamera.pixelHeight + 100), 
                0);
            
            var sortGroup = Mathf.RoundToInt(screenPos.y * Screen.width + screenPos.x);
            var ratio = 1f / (Screen.width * Screen.height / 20000f);
            var sortLayerNum = 10000 - Mathf.RoundToInt(sortGroup * ratio);

            //SortingGroup.sortingOrder = sortLayerNum;
            //if (ShadowSortingGroup != null)
            //	ShadowSortingGroup.sortingOrder = -20001;


            //trailCountdown -= Time.deltaTime;
            //if (trailCountdown < 0)
            //{
            //    trailCountdown += 0.1f;
            //    if (trailCountdown < 0)
            //        trailCountdown = 0;
            //    SpriteDataLoader.Instance.CloneObjectForTrail(this);
            //}
        }

        private float trailCountdown = 0.1f;

        public void Update()
        {
            if (!isInitialized)
                return;

            if (SetAsDirty)
            {
                isDirty = true;
                SetAsDirty = false;
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                SpriteData.Atlas.filterMode = nextUseSmoothRender ? FilterMode.Bilinear : FilterMode.Point;
                canUpdateRenderer = true;
            }

            if (Parent != null)
                return;


            UpdateShade();
            CurrentShade = Mathf.Lerp(CurrentShade, TargetShade, Time.deltaTime * 10f);

            for (var i = 0; i < ChildrenSprites.Count; i++)
                if (ChildrenSprites[i].IsInitialized)
                    ChildrenSprites[i].UpdateChildColor();

            UpdateColor();

            if (currentAction == null)
                ChangeAction(0);

            var angleIndex = 0;
            var is4dir = RoAnimationHelper.IsFourDirectionAnimation(Type, CurrentMotion);

            if (is4dir)
                angleIndex = RoAnimationHelper.GetFourDirectionSpriteIndexForAngle(Direction, 360 - CameraFollower.Instance.Rotation);
            else
                angleIndex = RoAnimationHelper.GetSpriteIndexForAngle(Direction, 360 - CameraFollower.Instance.Rotation);

            //if (CurrentMotion == SpriteMotion.Dead)
            //{
            //	deadResetTime += Time.deltaTime;
            //	if (deadResetTime > 5f)
            //	{
            //		State = SpriteState.Idle;
            //		ChangeMotion(SpriteMotion.Idle);
            //		deadResetTime = 0f;
            //		return;
            //	}

            //}

            if (currentAngleIndex != angleIndex && !LockAngle)
                ChangeAngle(angleIndex);

            if (doSpin)
            {
                rotate += Time.deltaTime * spinSpeed;
                if (rotate > 360)
                    rotate -= 360;
                if (rotate < 0)
                    rotate += 360;
                mat.SetFloat("_Rotation", rotate);
            }

            //if (Input.GetKeyDown(KeyCode.F11))
            //{
            //    DoSpin();
            //}

            if (Attack) // || Input.GetKeyDown(KeyCode.F))
            {
                ChangeMotion(SpriteMotion.Attack1);
                Attack = false;
            }
            if (Hit)
            {
                ChangeMotion(SpriteMotion.Hit);
                Hit = false;
            }
            if (Idle)
            {
                State = SpriteState.Idle;
                ChangeMotion(SpriteMotion.Idle);
                Idle = false;
            }
            if (Walk)
            {
                State = SpriteState.Walking;
                ChangeMotion(SpriteMotion.Walk);
                Walk = false;
            }

            if (Sit)
            {
                State = SpriteState.Sit;
                ChangeMotion(SpriteMotion.Sit);
                Sit = false;
            }
            if (Dead)
            {
                State = SpriteState.Dead;
                ChangeMotion(SpriteMotion.Dead);
                Dead = false;
            }
            if (Standby)
            {
                State = SpriteState.Standby;
                ChangeMotion(SpriteMotion.Standby);
                Standby = false;
            }

            if (Parent == null)
                currentFrameTime -= Time.deltaTime;

            if (currentFrameTime < 0 || currentFrame > maxFrame)
                AdvanceFrame();

            if (isDirty)
            {
                UpdateSpriteFrame();
                for (var i = 0; i < ChildrenSprites.Count; i++)
                {
                    if (ChildrenSprites[i].IsInitialized)
                        ChildrenSprites[i].ChildSetFrameData(currentActionIndex, currentAngleIndex, currentFrame);
                    else
                    {
                        ChildrenSprites[i].Initialize();
                        ChildrenSprites[i].ChildSetFrameData(currentActionIndex, currentAngleIndex, currentFrame);
                    }
                }

                for (var i = 0; i < BattleZSwapGroups.Count; i++)
                {
                    if (currentAngleIndex <= 1 || currentAngleIndex >= 6)
                    {
                        if (BattleZSwapGroups[i].sortingOrder < 0)
                            BattleZSwapGroups[i].sortingOrder *= -1;
                    }
                    else
                    {
                        if (BattleZSwapGroups[i].sortingOrder > 0)
                            BattleZSwapGroups[i].sortingOrder *= -1;
                    }
                }

                isDirty = false;
            }

        }
    }
}
