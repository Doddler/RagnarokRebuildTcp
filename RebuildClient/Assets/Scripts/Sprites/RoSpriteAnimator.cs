using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Utility;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Sprites
{
    public class RoSpriteAnimator : MonoBehaviour
    {
        public RoSpriteData SpriteData;
        public IRoSpriteRenderer SpriteRenderer;

        public Direction Direction
        {
            get => RoAnimationHelper.GetFacingForAngle(Angle);
            //set => Angle = RoAnimationHelper.FacingDirectionToRotation(value);
        }

        public float Angle;
        public SpriteType Type;
        public SpriteState State;
        public float AnimSpeed = 1;

        public Color BaseColor = UnityEngine.Color.white;
        public Color Color;
        public float Alpha { get; set; }
        public int SpriteOrder;

        private static bool nextUseSmoothRender = true;
        private static bool canUpdateRenderer = false;

        public List<RoSpriteAnimator> ChildrenSprites = new List<RoSpriteAnimator>();

        public RoSpriteAnimator Parent;
        public GameObject Shadow;
        public GameObject LightProbeAnchor;
        public SortingGroup ShadowSortingGroup { get; set; }
        public float VerticalOffset;

        public bool IgnoreAnchor = false;
        public bool IsHead = false;
        public HeadFacing HeadFacing;

        public bool SetAsDirty;

        public ServerControllable Controllable;
        private bool isInitialized;

        private string spriteName;

        private Light directionalLight;

        public Action OnFinishPlaying;

        public float CurrentShade = 1f;
        public float TargetShade;
        public float ShadeLevel = 0.85f;

        private const float shadeForShadow = 0.70f;

        public bool LockAngle;
        public bool DisableLoop;
        public bool RaycastForShadow = true;

        public SpriteMotion CurrentMotion;
        public int PreferredAttackMotion;

        public float MoveDistance;

        private RoAction currentAction;
        private int currentActionIndex;
        private int currentAngleIndex;

        private float currentFrameTime = 0;
        private float pauseTime = 0;
        private int currentFrame = 0;
        private int lastFrame = 0;
        private int lastWalkFrame = 0;
        private int maxFrame { get; set; } = 0;
        private bool isPaused;
        private bool isDirty;
        private bool isActive;

        public void OverrideCurrentFrame(int frame)
        {
            lastFrame = frame - 1;
            currentFrame = frame - 1;
            AdvanceFrame();
            UpdateSpriteFrame();
        }

        public static RoSpriteData FallbackSpriteData;

        //private float rotate = 0;
        //private bool doSpin = false;
        //private float spinSpeed = 100f;

        private Shader shader;

        public bool IsInitialized => isInitialized;

        private Camera mainCamera;

        public bool IsAttackMotion => CurrentMotion == SpriteMotion.Attack1 || CurrentMotion == SpriteMotion.Attack2 ||
                                      CurrentMotion == SpriteMotion.Attack3;

        public bool Is8Direction => RoAnimationHelper.Is8Direction(SpriteData.Type, CurrentMotion);

        public RoAction GetActionForMotion(SpriteMotion motion) => SpriteData.Actions[RoAnimationHelper.GetMotionIdForSprite(Type, motion)];

        public Action OnFinishAnimation;

        public void PauseAnimation(float time = 999_999_999f)
        {
            isPaused = true;
            pauseTime = time;
        }

        public void Unpause() => isPaused = false;

        public void SetDirty()
        {
            isDirty = true;
        }

        public void SetRenderActive(bool isActive)
        {
            // if (Shadow)
            //     Shadow.SetActive(isActive);
            gameObject.SetActive(isActive);
            if (SpriteRenderer != null)
            {
                SpriteRenderer.SetActive(isActive);
                foreach (var c in ChildrenSprites)
                    c.SpriteRenderer?.SetActive(isActive);
            }
        }

        public Vector2 GetAnimationAnchor()
        {
            if (SpriteRenderer == null || SpriteData == null)
            {
                //probably hasn't finished loading yet, not much we can do.
                return Vector2.zero;
            }

            var frame = SpriteRenderer.GetActiveRendererFrame();

            if (currentFrame >= currentAction.Frames.Length)
                return Vector2.zero;

            if (frame.Pos.Length > 0)
                return frame.Pos[0].Position;
            if (IsHead && (State == SpriteState.Idle || State == SpriteState.Sit))
                return frame.Pos[currentFrame].Position;

            return Vector2.zero;
        }

        public void OnSpriteDataLoad(RoSpriteData spriteData)
        {
            if (spriteData == null)
            {
                Debug.LogError($"Failed to load sprite data for sprite as the passed spriteData object was empty!");
                spriteData = FallbackSpriteData;
            }

            //Debug.Log("Loaded sprite data for sprite " + spriteData.Name);
            SpriteData = spriteData;
            Initialize();

            if (currentAction == null)
                ChangeAction(0);
        }


        public void OnSpriteDataLoadNoCollider(RoSpriteData spriteData)
        {
            if (spriteData == null)
                throw new Exception($"Failed to load sprite data for sprite as the passed spriteData object was empty!");
            //Debug.Log("Loaded sprite data for sprite " + spriteData.Name);
            SpriteData = spriteData;
            Initialize(false);

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
            SpriteData.Atlas.filterMode = FilterMode.Bilinear;

            var parent = gameObject.transform.parent;

            if (parent != null)
            {
                Parent = parent.gameObject.GetComponent<RoSpriteAnimator>();
                if (Parent != null)
                    Controllable = Parent.Controllable;
            }

            isInitialized = true;
            isActive = true;

            if (Parent == null)
            {
                LightProbeAnchor = new GameObject("LightProbeAnchor");
                LightProbeAnchor.transform.SetParent(gameObject.transform.parent);
                LightProbeAnchor.transform.localPosition = new Vector3(0f, 1f, 0f);
            }
            else
                LightProbeAnchor = Parent.LightProbeAnchor;

            ChangeMotion(CurrentMotion, true);

            if (Type == SpriteType.Player && State == SpriteState.Idle)
                PauseAnimation();

            if (ChildrenSprites.Count > 0)
            {
                foreach (var child in ChildrenSprites)
                    child.Initialize();
            }

            spriteName = SpriteData.Name;
            IgnoreAnchor = SpriteData.IgnoreAnchor;
            Color = Color.white;
            Alpha = 1;

            if (Parent == null)
            {
                UpdateShade();
                CurrentShade = TargetShade; //skip fade in at first spawn
            }
            else
            {
                Parent.isDirty = true;
                ChildUpdate();
            }

            if (SpriteRenderer == null)
            {
                var stdRenderer = gameObject.AddComponent<RoSpriteRendererStandard>();
                stdRenderer.SecondPassForWater = !LockAngle; //anything you'd want to lock angle for probably doesn't need a water pass
                stdRenderer.UpdateAngleWithCamera = !LockAngle;
                stdRenderer.SortingOrder = SpriteOrder;
                SpriteRenderer = stdRenderer;
            }

            SpriteRenderer.SetSprite(SpriteData);
            SpriteRenderer.SetColor(Color);
            SpriteRenderer.SetDirection(Direction);

            if (SpriteRenderer is RoSpriteRendererStandard std)
                std.VerticalOffset = VerticalOffset;

            if (Parent != null)
                SpriteRenderer.SetOffset(Parent.SpriteData.Size / 125f);

            SpriteRenderer.Initialize(makeCollider);
            SpriteRenderer.SetLightProbeAnchor(LightProbeAnchor);

            isDirty = true;
        }

        //public void OnDrawGizmos()
        //{
        //    //if (Parent != null)
        //    // return; //don't draw gizmo if we're parented to someone else.
        //    //   var pos = RoAnimationHelper.FacingDirectionToVector(Direction);
        //    //   Gizmos.DrawLine(transform.position, transform.position + new Vector3(pos.x, 0, pos.y));
        //}

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
            if (!isInitialized || SpriteRenderer == null)
                return;

            if (currentFrame >= currentAction.Frames.Length)
            {
                //Debug.LogWarning($"Current frame is {currentFrame}, max frame is {maxFrame}, but actual frame max is {currentAction.Frames.Length}");
                //return;
                currentFrame = currentAction.Frames.Length - 1; //hold on last frame. This only happens if this is a child of an animation of a longer length
            }

            var frame = currentAction.Frames[currentFrame];

            if (frame.Sound > -1 && frame.Sound < SpriteData.Sounds.Length && lastFrame != currentFrame && !isPaused)
            {
                var distance = (transform.position - CameraFollower.Instance.ListenerProbe.transform.position).magnitude;
                if (distance < 50)
                {
                    var sound = SpriteData.Sounds[frame.Sound];
                    if (sound != null)
                        AudioManager.Instance.AttachSoundToEntity(Controllable.Id, sound, gameObject);
                    // var src = AudioSources[frame.Sound];
                    // if (src != null)
                    // {
                    //     src.enabled = true;
                    //     src.priority = 32 + Mathf.FloorToInt(distance); 
                    //     src.Play();
                    // }
                }
            }

            lastFrame = currentFrame;


            if (Parent == null)
                SpriteRenderer.SetAction(currentActionIndex, Is8Direction);
            else
                SpriteRenderer.SetAction(currentActionIndex, Parent.Is8Direction);
            //SpriteRenderer.SetDirection((Direction)currentAngleIndex);
            SpriteRenderer.SetAngle(Angle);
            SpriteRenderer.SetFrame(currentFrame);

            if (currentFrame >= currentAction.Frames.Length)
            {
                //for some reason we've changed to an action with a shorter length, loop around or stop depending on the action type
                if (!DisableLoop)
                    currentFrame = 0;
                else
                {
                    currentFrame = currentAction.Frames.Length - 1;
                    if (OnFinishPlaying != null)
                        OnFinishPlaying();
                }
            }

            SpriteRenderer.UpdateRenderer();
            SpriteRenderer.Rebuild();
        }

        public void ChangeAngle(float angle)
        {
            if (Angle == angle)
                return;
            Angle = angle;
            currentAngleIndex = (int)RoAnimationHelper.GetFacingForAngle(angle);
            if (!isInitialized)
                return;
            currentAction = SpriteData.Actions[currentActionIndex + currentAngleIndex];
            maxFrame = currentAction.Frames.Length - 1;
            if (currentFrame > maxFrame)
                currentFrame = 0;
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
            if (currentFrame > maxFrame)
                currentFrame = 0;
            isDirty = true;
        }

        public void ChangeActionExact(int newActionIndex)
        {
            if (!isInitialized) return;
            currentActionIndex = newActionIndex / 8 * 8;
            currentAngleIndex = newActionIndex % 8;
            currentAction = SpriteData.Actions[newActionIndex];
            Angle = RoAnimationHelper.FacingDirectionToRotation((Direction)currentAngleIndex);
            maxFrame = currentAction.Frames.Length - 1;
            currentFrameTime = 0; //reset current frame time
            if (currentFrame > maxFrame)
                currentFrame = 0;
            isDirty = true;
            isPaused = false;
        }

        public void ChangeMotion(SpriteMotion nextMotion, bool forceUpdate = false)
        {
            // if(SpriteData?.Name == "초보자_남")
            // Debug.Log($"{name} state {State} change motion from {CurrentMotion} to {nextMotion}");

            if (CurrentMotion == SpriteMotion.Dead && !forceUpdate)
                Debug.LogWarning("Changing from dead to something else!");

            if (CurrentMotion == nextMotion && !forceUpdate)
                return;

            CurrentMotion = nextMotion;
            currentFrame = 0;
            if (nextMotion == SpriteMotion.Walk)
                currentFrame = lastWalkFrame;

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
                    PauseAnimation();
                }
                else
                    HeadFacing = HeadFacing.Center;
            }

            if (Shadow != null && isActive)
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
                {
                    if (nextMotion == SpriteMotion.Idle || nextMotion == SpriteMotion.Standby || nextMotion == SpriteMotion.Dead)
                        AnimSpeed = 1;
                    ChangeMotion(nextMotion);
                }
                else
                {
                    OnFinishAnimation?.Invoke();

                    if (State != SpriteState.Dead && !DisableLoop)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        currentFrame = maxFrame;
                        PauseAnimation();
                    }
                }
            }

            if (State == SpriteState.Walking && CurrentMotion == SpriteMotion.Walk)
            {
                var stepSize = DebugValueHolder.GetOrDefault("stepSize", 4.6f);
                var newWalkFrame = MoveDistance * stepSize * 0.37f * 4f / (currentAction.Delay / 24f);
                // Debug.Log($"{newWalkFrame} {MoveDistance} {currentAction.Delay}");
                // if(newWalkFrame != currentFrame)
                //     Debug.Log($"{newWalkFrame} {MoveDistance} {currentAction.Delay}");
                currentFrame = lastWalkFrame = Mathf.FloorToInt(newWalkFrame) % (maxFrame + 1);
            }
            else
            {
                if (currentFrameTime < 0)
                    currentFrameTime += (float)currentAction.Delay / 1000f * AnimSpeed;
                if (currentFrameTime < 0)
                    currentFrameTime = 0; //if we're more than a full frame behind lets just reset the clock
            }

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
            if (SpriteRenderer != null)
            {
                Angle = Parent.Angle;
                SpriteRenderer.SetAngle(Angle);
                currentAngleIndex = Parent.currentAngleIndex;
            }

            if (!IgnoreAnchor)
            {
                var parentAnchor = Parent.GetAnimationAnchor();
                var ourAnchor = GetAnimationAnchor();

                if (ourAnchor == Vector2.zero)
                {
                    transform.localPosition = new Vector3(0f, 0f, 0f);
                    return;
                }

                var diff = parentAnchor - ourAnchor;

                transform.localPosition = new Vector3(diff.x / 50f, -diff.y / 50f, -SpriteOrder * 0.01f);
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
                ShadeLevel = shadeForShadow;
            }

            var srcPos = transform.position + new Vector3(0, 0.2f, 0);

            var destDir = directionalLight.transform.rotation * Vector3.forward * -1;

            var mask = LayerMask.GetMask("Ground") | LayerMask.GetMask("Object");

            var ray = new Ray(srcPos, destDir);

            //Debug.DrawLine(srcPos, destDir, Color.yellow, 10f, false);
            //Debug.DrawRay(srcPos, destDir, Color.yellow, 100f, false);

            if (RaycastForShadow && Physics.Raycast(ray, out var hit, 50f, mask))
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
            c *= BaseColor;

            if (SpriteRenderer != null)
                SpriteRenderer.SetColor(c);
        }

        public void UpdateChildColor()
        {
            var c = new Color(Parent.Color.r * Parent.CurrentShade, Parent.Color.g * Parent.CurrentShade, Parent.Color.b * Parent.CurrentShade, Parent.Alpha);
            c *= Parent.BaseColor;

            if (SpriteRenderer != null)
                SpriteRenderer.SetColor(c);
        }

        public void LateUpdate()
        {
            if (canUpdateRenderer)
            {
                nextUseSmoothRender = !nextUseSmoothRender;
                canUpdateRenderer = false;
            }

            if (Parent != null)
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
        }

        public void Update()
        {
            if (!isInitialized)
                return;

            if (SetAsDirty)
            {
                isDirty = true;
                SetAsDirty = false;
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

            pauseTime--;
            if (pauseTime < 0)
                isPaused = false;

            var angleIndex = (int)Direction;

            if (currentAngleIndex != (int)RoAnimationHelper.GetFacingForAngle(Angle) && !LockAngle)
                ChangeAngle(Angle);

            if (Parent == null)
                currentFrameTime -= Time.deltaTime;

            if (currentFrameTime < 0 || currentFrame > maxFrame)
                AdvanceFrame();

            //if (isDirty)
            {
                UpdateSpriteFrame();
                for (var i = 0; i < ChildrenSprites.Count; i++)
                {
                    if (!ChildrenSprites[i].IsInitialized)
                        ChildrenSprites[i].Initialize();

                    ChildrenSprites[i].ChangeAngle(Angle);
                    ChildrenSprites[i].ChildSetFrameData(currentActionIndex, currentAngleIndex, currentFrame);
                }

                isDirty = false;
            }
        }
    }
}