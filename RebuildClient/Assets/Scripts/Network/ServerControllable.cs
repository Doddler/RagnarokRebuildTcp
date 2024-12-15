using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Assets.Scripts.Effects;
using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Misc;
using Assets.Scripts.Network.Messaging;
using Assets.Scripts.Objects;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Hud;
using Assets.Scripts.Utility;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Assets.Scripts.Network
{
    public class ServerControllable : MonoBehaviour, IEffectOwner
    {
        private RoWalkDataProvider walkProvider;

        public CharacterType CharacterType;
        public RoSpriteAnimator SpriteAnimator;
        public int Id;
        public int ClassId;
        public Vector2Int CellPosition;
        public Vector2 MoveStartPos;
        public Vector3 StartPos;
        public Vector3 PositionOffset;
        public Vector3 RealPosition;
        public float ShadowSize;
        public bool IsAlly;
        public bool IsMale;
        public bool IsMainCharacter;
        public bool IsInteractable { get; set; }
        public int Level;
        public string Name { get; set; }

        public int Hp;
        public int MaxHp { get; set; }
        public int Sp;
        public int MaxSp;
        public int WeaponClass;

        public GameObject PopupDialog;
        public List<Ragnarok3dEffect> EffectList;

        public Dictionary<EquipPosition, GameObject> AttachedComponents = new();

        public string DisplayName => CharacterType == CharacterType.NPC || !GameConfig.Data.ShowLevelsInOverlay || Name.StartsWith("[NPC]") ? Name : $"Lv.{Level} {Name}";

        [NonSerialized] public Vector3 CounterHitDir;

        [NonSerialized] public ClientSpriteType SpriteMode;
        [NonSerialized] public GameObject EntityObject;
        [NonSerialized]public GameObject ComboIndicator;
        [NonSerialized]public GameObject FollowerObject;
        [NonSerialized] public StatusEffectState StatusEffectState;

        [NonSerialized] public CharacterFloatingDisplay FloatingDisplay;
        private List<Vector2Int> movePath;
        private Vector2Int[] tempPath;
        private int currentMoveStep;
        private float moveSpeed = 0.2f;
        private float tempSpeed = -1f;
        private float tempSpeedTime = 0f;
        private float moveProgress;
        private float moveLength;
        private float hitLockImmuneTime = 0f;
        private bool isDirectMove;
        public float PosLockTime;
        public float AttackAnimationSpeed = 1f;

        private UniqueAttackAction uniqueAttackAction;
        private float uniqueAttackStart;
        private bool skipNextAttackMotion;
        private Vector2 lastFramePosition;

        private bool isMoving;
        //  {
        //      get => _isMoving;
        //      set
        //      {
        //          if (IsMainCharacter)
        //              Debug.Log($"Changing main character move state.");
        //          _isMoving = value;
        //      }
        //  }
        // private bool _isMoving;

        private Vector3 snapStartPos;
        private LTDescr snapAnim;

        private SpriteRenderer shadowSprite;
        private Material shadowMaterial;

        private float hitDelay = 0f;

        //private float dialogCountdown = 0f;
        private float movePauseTime = 0f;
        public bool IsMoving => isMoving;
        public bool IsCasting;
        public bool HideCastName;
        public readonly EntityMessageQueue Messages = new();

        public bool IsWalking => movePath != null && movePath.Count > 1;

        public bool IsHidden { get; set; }

        public void Init()
        {
            Messages.Owner = this;
        }

        public CharacterFloatingDisplay EnsureFloatingDisplayCreated(bool makeEnabled = false)
        {
            if (FloatingDisplay == null)
                FloatingDisplay = NetworkManager.Instance.OverlayManager.GetNewFloatingDisplay();
            if (makeEnabled)
                FloatingDisplay.gameObject.SetActive(true);
            return FloatingDisplay;
        }

        public void LookAt(Vector3 lookAt)
        {
            var pos1 = new Vector2(transform.position.x, transform.position.z);
            var pos2 = new Vector2(lookAt.x, lookAt.z);
            var dir = (pos2 - pos1).normalized;
            var angle = Vector2.SignedAngle(dir, Vector2.up);
            if (angle < 0)
                angle += 360f;

            if (SpriteAnimator != null)
                SpriteAnimator.ChangeAngle(angle);
        }

        public void LookAtOrDefault(ServerControllable target, Direction fallbackDir)
        {
            if (target != null)
                LookAt(target.transform.position);
            else
                SpriteAnimator.ChangeAngle(RoAnimationHelper.FacingDirectionToRotation(fallbackDir));
        }
        
        public void SetSp(int sp, int maxSp)
        {
            EnsureFloatingDisplayCreated();
            MaxSp = maxSp;
            Sp = sp;
            FloatingDisplay.UpdateMaxMp(maxSp);
            FloatingDisplay.UpdateMp(sp);
        }

        public void SetHp(int hp, int maxHp)
        {
            if (SpriteAnimator != null && SpriteAnimator.IsHidden && !IsMainCharacter)
                return;
            
            MaxHp = maxHp;
            Hp = hp;
            FloatingDisplay.UpdateMaxHp(maxHp);
            SetHp(hp);
        }

        public void SetHp(int hp)
        {
            if (SpriteAnimator != null && SpriteAnimator.IsHidden && !IsMainCharacter)
                return;
            
            var oldHp = Hp;
            Hp = hp;

            if ((GameConfig.Data.AutoHideFullHPBars && hp >= MaxHp) || !IsInteractable)
            {
                if (FloatingDisplay == null)
                    return;
                FloatingDisplay.HideHpBar();
                return;
            }

            EnsureFloatingDisplayCreated();
            if (IsMainCharacter)
            {
                FloatingDisplay.ForceHpBarOn();
                FloatingDisplay.ForceMpBarOn();
            }

            if (CharacterType != CharacterType.NPC)
                FloatingDisplay.UpdateHp(oldHp, hp);
        }

        public void ShowSkillCastMessage(CharacterSkill skill, float duration = 5f)
        {
            if (CharacterType != CharacterType.Player)
                return;

            if (SpriteAnimator.IsHidden && skill == CharacterSkill.Hiding)
                return;

            var sName = ClientDataLoader.Instance.GetSkillName(skill);
            FloatingDisplay.ShowChatBubbleMessage(sName + "!!", duration);
        }

        public void StartCastBar(CharacterSkill skill, float duration)
        {
            EnsureFloatingDisplayCreated();
            FloatingDisplay.StartCasting(duration);
            var sName = ClientDataLoader.Instance.GetSkillName(skill);
            //
            // if (skill == CharacterSkill.FireBolt)
            //     sName = "Fire Bolt";
            // if (skill == CharacterSkill.ColdBolt)
            //     sName = "Cold Bolt";
            // if (skill == CharacterSkill.Bash)
            //     sName = "Bash";
            // if (skill == CharacterSkill.Mammonite)
            //     sName = "Mammonite";

            IsCasting = true;

            if (!HideCastName && (skill != CharacterSkill.Hiding || !SpriteAnimator.IsHidden)) //don't show skill name when unhiding
            {
                if (CharacterType == CharacterType.Player)
                    FloatingDisplay.ShowChatBubbleMessage(sName + "!!");
                else
                {
                    FloatingDisplay.ShowChatBubbleMessage("<size=-2><color=#FF8888>" + sName + "</size>", duration);
                }
            }

            if (SpriteAnimator != null && SpriteAnimator.SpriteData != null && ClientDataLoader.Instance.GetUniqueAction(SpriteAnimator.SpriteData.Name, skill, out var action))
            {
                skipNextAttackMotion = true;
                var start = action.StartAt / 1000f;
                if (duration < start)
                {
                    SpriteAnimator.ChangeMotion((SpriteMotion)action.Animation);
                }
                else
                {
                    uniqueAttackAction = action;
                    uniqueAttackStart = Time.timeSinceLevelLoad + duration - start;
                }
            }
        }

        public void StopCasting()
        {
            uniqueAttackAction = null;
            FloatingDisplay?.CancelCasting();
            EndEffectOfType(EffectType.CastEffect);
        }

        public void StopCastingAnimation()
        {
            if (SpriteAnimator == null)
                return;
            if (SpriteAnimator.CurrentMotion == SpriteMotion.Casting)
                SpriteAnimator.Unpause();

        }

        // public void RefreshHiddenState()
        // {
        //     if (shadowSprite)
        //         shadowSprite.enabled = !isHidden;
        //     if (SpriteAnimator)
        //         SpriteAnimator.SetRenderActive(!isHidden);
        // }

        public void ShowTargetNamePlate(string name)
        {
            EnsureFloatingDisplayCreated();
            FloatingDisplay.UpdateName(name);
            FloatingDisplay.TargetingNamePlate();
        }

        public void ShowHoverNamePlate(string name)
        {
            EnsureFloatingDisplayCreated();
            FloatingDisplay.UpdateName(name);
            FloatingDisplay.HoverNamePlate();
        }

        public void HideTargetNamePlate()
        {
            if (FloatingDisplay == null)
                return;
            FloatingDisplay.EndTargetingNamePlate();
        }

        public void HideHoverNamePlate()
        {
            if (FloatingDisplay == null)
                return;
            FloatingDisplay.EndHoverNamePlate();
        }

        public void HideHpBar()
        {
            if (FloatingDisplay == null)
                return;
            FloatingDisplay.HideHpBar();
        }

        public void DialogBox(string text)
        {
            EnsureFloatingDisplayCreated();
            FloatingDisplay.ShowChatBubbleMessage(text, 8f);

            SnapDialog();
        }

        public void SnapDialog()
        {
            if (FloatingDisplay == null)
                return;

            if (IsMainCharacter)
                FloatingDisplay.transform.SetAsLastSibling();

            var cf = CameraFollower.Instance;
            var rect = FloatingDisplay.transform as RectTransform;
            var screenPos = cf.Camera.WorldToScreenPoint(transform.position);

            var d = 70 / cf.Distance;
            var reverseScale = 1f / cf.CanvasScaler.scaleFactor;

            if (!GameConfig.Data.ScalePlayerDisplayWithZoom)
                d = 1f;
            var screenScale = Screen.height / 1920f * 2f;
            d *= screenScale;

            rect.localScale = new Vector3(d, d, d);
            rect.anchoredPosition = new Vector2(screenPos.x * reverseScale, (screenPos.y - cf.UiCanvas.pixelRect.height) * reverseScale);
        }

        public void ConfigureEntity(int id, Vector2Int worldPos, Direction direction)
        {
            if (walkProvider == null)
                walkProvider = RoWalkDataProvider.Instance;

            Id = id;

            CellPosition = worldPos;

            var offset = 0f;
            if (SpriteMode == ClientSpriteType.Prefab)
                offset = 0.15f; //haaack

            var start = new Vector3(worldPos.x + 0.5f, 0f, worldPos.y + 0.5f);
            var position = new Vector3(start.x, walkProvider.GetHeightForPosition(start) + offset, start.z);

            // Debug.Log($"Configure entity on {name} setting position to {position}");
            RealPosition = position;
            transform.localPosition = RealPosition + PositionOffset;
            

            if (SpriteMode == ClientSpriteType.Sprite)
                SpriteAnimator.ChangeAngle(RoAnimationHelper.FacingDirectionToRotation(direction));
            else
                gameObject.transform.rotation = Quaternion.Euler(0f, RoAnimationHelper.FacingDirectionToRotation(direction), 0f);
        }

        private bool IsNeighbor(Vector2Int pos1, Vector2Int pos2)
        {
            var x = Mathf.Abs(pos1.x - pos2.x);
            var y = Mathf.Abs(pos1.y - pos2.y);

            if (x <= 1 && y <= 1)
                return true;
            return false;
        }

        private Direction GetDirectionForHeading(Vector2Int dest)
        {
            var src = new Vector2(RealPosition.x - 0.5f, RealPosition.z - 0.5f);
            var angle = Vector2.SignedAngle(src, dest);
            return Directions.GetFacingForAngle(angle);
        }

        private Direction GetDirectionForOffset(Vector2Int offset)
        {
            if (offset.x == -1 && offset.y == -1) return Direction.SouthWest;
            if (offset.x == -1 && offset.y == 0) return Direction.West;
            if (offset.x == -1 && offset.y == 1) return Direction.NorthWest;
            if (offset.x == 0 && offset.y == 1) return Direction.North;
            if (offset.x == 1 && offset.y == 1) return Direction.NorthEast;
            if (offset.x == 1 && offset.y == 0) return Direction.East;
            if (offset.x == 1 && offset.y == -1) return Direction.SouthEast;
            if (offset.x == 0 && offset.y == -1) return Direction.South;

            return Direction.South;
        }

        private bool IsDiagonal(Direction dir)
        {
            if (dir == Direction.NorthEast || dir == Direction.NorthWest ||
                dir == Direction.SouthEast || dir == Direction.SouthWest)
                return true;
            return false;
        }

        public void DirectWalkMove(float speed, float time, Vector2Int dest)
        {
            Debug.Log("WAIT NO WHY ARE YOU CALLING THIS CODE STOPPPPP");
            StartPos = transform.position - new Vector3(0.5f, 0f, 0.5f);

            if (movePath == null)
                movePath = new List<Vector2Int>(20);
            else
                movePath.Clear();

            movePath.Add(dest);
            movePath.Add(dest);

            //movePath[0] = StartPos;
            moveProgress = time / speed;
            moveSpeed = speed;

            isDirectMove = true;
        }

        public void StartMove2(float speed, float nextCellTime, int stepCount, int curStep, Vector2 startPosition, List<Vector2Int> steps)
        {
            // if (IsMainCharacter)
            //     Debug.Log($"{name} Start Move - From {steps[0]}({startPosition} )to {steps[stepCount - 1]}\nSpeed:{speed} StepCount:{stepCount} CurStep:{curStep} StepLength:{steps.Count}");

            moveSpeed = speed;

            if (movePath == null)
                movePath = new List<Vector2Int>(20);
            else
                movePath.Clear();

            moveLength = nextCellTime;
            moveProgress = 0;
            currentMoveStep = curStep;

            for (var i = 0; i < stepCount; i++)
                movePath.Add(steps[i]);

            MoveStartPos = new Vector2(RealPosition.x, RealPosition.z); // startPosition;
            LeanTween.cancel(gameObject);
            isMoving = true;

            var sb = new StringBuilder();
            sb.Append(movePath[0]);
            for (var i = 1; i < stepCount; i++)
                sb.Append(", " + movePath[i]);
            // Debug.Log($"Starting move from {startPosition} (but really we're starting from {MoveStartPos}). MoveData: \n{sb.ToString()}");
        }

        public void SetHiding(bool isHidden)
        {
            
        }

        public void StartMove(float speed, float progress, int stepCount, int curStep, List<Vector2Int> steps)
        {
            // if (IsMainCharacter)
            //     Debug.Log($"{name} Start Move - From {steps[0]} to {steps[stepCount - 1]}\nSpeed:{speed} Progress:{progress} StepCount:{stepCount} CurStep:{curStep} StepLength:{steps.Count}");

            moveSpeed = speed;
            //tempSpeed = -1;
            isDirectMove = false;

            //don't reset start pos if the next tile is the same
            if (movePath == null || movePath.Count <= 1 || movePath[1] != steps[1])
                //    if (stepCount > 1)
                StartPos = RealPosition - new Vector3(0.5f, 0f, 0.5f);

            moveProgress = progress / speed; //hack please fix


            if (movePath == null)
                movePath = new List<Vector2Int>(20);
            else
                movePath.Clear();

            //var pathString = "";

            for (var i = curStep; i < stepCount; i++)
            {
                //pathString += steps[i] + " ";

                movePath.Add(steps[i]);
            }

            //Debug.Log(pathString);
            //Debug.Log(movePath.Count);

            LeanTween.cancel(gameObject);

            isMoving = true;
        }

        private void UpdateMovePosition()
        {
            if (movePath == null || movePath.Count == 0 || SpriteMode == ClientSpriteType.Prefab) return;

            var totalSteps = movePath.Count;
            var dir = SpriteAnimator.Direction;
            while (moveProgress >= moveLength)
            {
                currentMoveStep++;
                if (currentMoveStep >= totalSteps - 1)
                {
                    // Debug.Log($"Ending Move at {movePath[currentMoveStep].ToWorldPosition()}");
                    RealPosition = movePath[currentMoveStep].ToWorldPosition();
                    transform.localPosition = RealPosition + PositionOffset;
                    CellPosition = RealPosition.ToTilePosition();
                    movePath.Clear();
                    isMoving = false;
                    return;
                }

                moveProgress -= moveLength;
                moveLength = moveSpeed;
                dir = GetDirectionForOffset(movePath[currentMoveStep + 1] - movePath[currentMoveStep]);
                if (IsDiagonal(dir))
                    moveLength *= 1.4142f;
            }

            var prog = moveProgress / moveLength;
            if (currentMoveStep == 0)
            {
                var x = Mathf.Lerp(MoveStartPos.x, movePath[1].x + 0.5f, prog);
                var y = Mathf.Lerp(MoveStartPos.y, movePath[1].y + 0.5f, prog);
                RealPosition = walkProvider.GetWorldPositionFor2DLocation(x, y);
                transform.localPosition = RealPosition + PositionOffset;
                CellPosition = RealPosition.ToTilePosition();
                SpriteAnimator.Angle = RoAnimationHelper.AngleDir(movePath[1].ToMapPosition(), new Vector2(x, y));
                // Debug.Log($"First step lerp from {MoveStartPos} to {movePath[currentMoveStep+1]} returns {transform.localPosition}");
            }
            else
            {
                var x = Mathf.Lerp(movePath[currentMoveStep].x, movePath[currentMoveStep + 1].x, prog) + 0.5f;
                var y = Mathf.Lerp(movePath[currentMoveStep].y, movePath[currentMoveStep + 1].y, prog) + 0.5f;
                RealPosition = walkProvider.GetWorldPositionFor2DLocation(x, y);
                transform.localPosition = RealPosition + PositionOffset;
                CellPosition = RealPosition.ToTilePosition();
                SpriteAnimator.ChangeAngle(RoAnimationHelper.FacingDirectionToRotation(dir));
                // Debug.Log($"Lerp from {movePath[currentMoveStep]} to {movePath[currentMoveStep+1]} returns {transform.localPosition}");
            }
        }

        public void UpdateMove2()
        {
            if (movePath == null || movePath.Count == 0 || SpriteMode == ClientSpriteType.Prefab) return;
            var speed = 1f;
            if (tempSpeedTime > 0)
                speed = tempSpeed / moveSpeed;

            moveProgress += speed * Time.deltaTime;

            if (movePauseTime <= 0)
                UpdateMovePosition();
        }

        public void UpdateMove(bool forceUpdate = false)
        {
            Debug.Log("WHY NO STOP DON'T CALL THIS CODE");
            if (movePath.Count == 0 || SpriteMode == ClientSpriteType.Prefab) return;
            var speed = moveSpeed;
            if (tempSpeedTime > 0)
                speed = tempSpeed;

            if (movePath.Count > 1)
            {
                if (isDirectMove)
                {
                    moveProgress -= Time.deltaTime / speed;
                    var angle = Vector2.SignedAngle(Vector2.up, movePath[1] - new Vector2(StartPos.x, StartPos.z));
                    // Debug.Log($"Next Step {movePath[1]} curPosition {new Vector2(StartPos.x, StartPos.z)} angle {angle}");
                    SpriteAnimator.ChangeAngle(angle);
                }
                else
                {
                    var offset = movePath[1] - movePath[0];
                    if (IsDiagonal(GetDirectionForOffset(offset)))
                        moveProgress -= Time.deltaTime / speed * 0.70f;
                    else
                        moveProgress -= Time.deltaTime / speed;
                    SpriteAnimator.ChangeAngle(RoAnimationHelper.AngleDir(offset, Vector2.up));
                }
            }

            if (moveProgress < 0.5f)
                CellPosition = movePath[1];

            while (moveProgress < 0f && movePath.Count > 1)
            {
                movePath.RemoveAt(0);
                StartPos = new Vector3(movePath[0].x, walkProvider.GetHeightForPosition(RealPosition), movePath[0].y);
                CellPosition = movePath[0];
                moveProgress += 1f;
                // tempSpeed = -1;
            }

            if (movePath.Count == 0)
                Debug.Log("WAAA");

            if (PosLockTime > 0f && !forceUpdate)
                return;

            if (movePath.Count == 1)
            {
                CellPosition = movePath[0];
                RealPosition = new Vector3(CellPosition.x + 0.5f, walkProvider.GetHeightForPosition(RealPosition), CellPosition.y + 0.5f);
                transform.localPosition = RealPosition + PositionOffset;
                movePath.Clear();
                // if (IsMainCharacter)
                //     Debug.Log($"We've finished pathing, stopping character.");
                isMoving = false;
            }
            else
            {
                var xPos = Mathf.Lerp(StartPos.x, movePath[1].x, 1 - moveProgress) + 0.5f;
                var yPos = Mathf.Lerp(StartPos.z, movePath[1].y, 1 - moveProgress) + 0.5f;

                RealPosition = new Vector3(xPos + 0.5f, walkProvider.GetHeightForPosition(transform.position), yPos + 0.5f);
                transform.localPosition = RealPosition + PositionOffset;

                //var offset = movePath[1] - movePath[0];
            }
        }

        public void MovePosition(Vector2Int targetPosition)
        {
            RealPosition = targetPosition.ToWorldPosition();
            //transform.localPosition = RealPosition + PositionOffset;
        }

        public void StopWalking()
        {
            if (movePath.Count > 2)
                movePath.RemoveRange(2, movePath.Count - 2);

            isDirectMove = false;
        }

        public void SlowMove(float factor, float time)
        {
            if (factor <= 0)
                return;
            tempSpeed = moveSpeed * (1f / factor);
            tempSpeedTime = time;
        }

        public void PauseMove(float time)
        {
            if (!isMoving)
                return;

            if (SpriteAnimator.SpriteData.Type == SpriteType.Player)
                SpriteAnimator.State = SpriteState.Standby;
            else
                SpriteAnimator.State = SpriteState.Idle;

            movePauseTime = time;
        }

        public int FindPosInPath(Vector2Int pos)
        {
            Assert.IsTrue(IsMoving);
            for (var i = 0; i < movePath.Count; i++)
                if (movePath[i] == pos)
                    return i;

            return -1;
        }

        public void AdjustMovePathToMatchServerPosition(Vector2Int pos, float progressToNextTile)
        {
            var targetPos = new Vector3(pos.x + 0.5f, pos.y + 0.5f);
            var distance = Vector2.Distance(movePath[0], targetPos);

            // if(IsMainCharacter && Application.isEditor)
            //     Debug.Log($"SnapToMovePath: targetPos: {targetPos} distance: {distance}\nPosition in move path: {FindPosInPath(pos)}");

            if (distance < 0.35f) //no point if we're pretty close to where we should be
                return;

            var inPathPosition = FindPosInPath(pos);

            if (inPathPosition < 0)
            {
                //we're ahead of the server as we've already removed the tile we should be on.
                //We'll let the server catch up by waiting the amount of time it would take for
                //a character to travel the distance, assuming it doesn't get hit again.
                movePauseTime = moveSpeed * distance;
                // Debug.Log($"Setting movePauseTime of {movePauseTime}");
            }
            else
            {
                //the server is ahead of where we currently are.
                //we will prevent this character from visually being hit locked until we could
                //possibly catch up, plus a little extra.
                var timeToTile = (inPathPosition + (1 - progressToNextTile)) * moveSpeed;
                hitLockImmuneTime = Time.time + timeToTile;
                // Debug.Log($"Setting hitLockImmuneTime {timeToTile}");
            }
        }

        public void SnapToTile(Vector2Int position, float snapSpeed = 0.07f, float leeway = 0.75f)
        {
            var targetPos = new Vector3(position.x + 0.5f, walkProvider.GetHeightForPosition(RealPosition), position.y + 0.5f);

            LeanTween.cancel(gameObject);

            // if (IsMainCharacter)
            //     Debug.Log(
            //         $"SnapToTile {Name} has distance {(transform.localPosition - targetPos).magnitude} and speed of {snapSpeed}f. {leeway}f required to execute snap.");

            RealPosition = targetPos;

            if ((RealPosition - targetPos).magnitude > 0.75f)
                LeanTween.move(gameObject, RealPosition + PositionOffset, 0.07f);
        }

        public void StopImmediate(Vector2Int position, bool snapToTile = true)
        {
            isMoving = false;
            isDirectMove = false;
            movePath?.Clear();
            if (SpriteAnimator.State == SpriteState.Walking)
            {
                SpriteAnimator.State = SpriteState.Idle;
                SpriteAnimator.ChangeMotion(SpriteMotion.Idle);
                SpriteAnimator.AnimSpeed = 1f;
            }

            // if (IsMainCharacter)
            //     Debug.Log($"Character asked to stop immediately.\nSnap to tile if out of range:{snapToTile}");

            if (snapToTile)
                SnapToTile(position);
        }

        public void AttachEffect(Ragnarok3dEffect effect)
        {
            if (EffectList == null)
                EffectList = new List<Ragnarok3dEffect>();

#if UNITY_EDITOR
            if (EffectList.Contains(effect))
            {
                Debug.LogWarning($"Attempting to attach effect {effect} but it is already attached!");
                return;
            }
#endif

            EffectList.Add(effect);
            effect.EffectOwner = this;
        }

        public void EndEffectOfType(EffectType type)
        {
            if (EffectList == null || EffectList.Count == 0)
                return; //job well done

            Span<int> endEffect = stackalloc int[EffectList.Count];
            var pos = 0;
            for (var i = 0; i < EffectList.Count; i++)
            {
                if (EffectList[i].EffectType == type)
                {
                    EffectList[i].EndEffect();
                    endEffect[pos] = i;
                    pos++;
                }
            }

            //remove all the effects that are flagged as ended. Iterating from back to front allows the indexes to be correct even after removing some.
            for (var i = pos; i > 0; i--)
            {
                EffectList.RemoveAt(endEffect[i - 1]);
            }
        }

        public void SetAttackAnimationSpeed(float motionTime)
        {
#if DEBUG
            if (CameraFollower.Instance.DebugIgnoreAttackMotion)
            {
                AttackAnimationSpeed = 1;
                return;
            }
#endif

            if (SpriteAnimator == null || SpriteAnimator.SpriteData == null)
            {
                AttackAnimationSpeed = 1;
                return;
            }

            var baseMotionTime = SpriteAnimator.SpriteData.AttackFrameTime / 1000f;
            if (CharacterType == CharacterType.Player)
            {
                baseMotionTime = 0.6f;

                if (motionTime > 1.5f)
                    motionTime = 1.5f;
            }
            // if (ClassId == 2)
            //     baseMotionTime = 0.6f;


            //
            // if (baseMotionTime < motionTime && ClassId != 2)
            // {
            //     AttackAnimationSpeed = 1;
            //     return;
            // }

            AttackAnimationSpeed = motionTime / baseMotionTime;

            // Debug.Log($"Attack! speed {AttackAnimationSpeed} = motionTime {motionTime} / baseMotionTime {baseMotionTime}");
        }

        public void AttachShadow(Sprite spriteObj)
        {
            if (gameObject == null)
                return;
            var go = new GameObject("Shadow");
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(ShadowSize, ShadowSize, ShadowSize);

            if (Mathf.Approximately(0, ShadowSize))
                ShadowSize = 0.5f;

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = spriteObj;
            shadowSprite = sprite;

            var shader = ShaderCache.Instance.SpriteShaderNoZWrite;
            var mat = new Material(shader);
            mat.SetFloat("_Offset", 0.4f);
            mat.color = new Color(1f, 1f, 1f, 0.5f);
            mat.renderQueue = 2999;
            sprite.material = mat;

            sprite.sortingOrder = -1;

            SpriteAnimator.Shadow = go;
            SpriteAnimator.ShadowSortingGroup = go.AddComponent<SortingGroup>();
            SpriteAnimator.ShadowSortingGroup.sortingOrder = -20001;
            if (SpriteAnimator.State == SpriteState.Sit)
                go.SetActive(false);
        }

        public void PerformSkillMotion()
        {
            if (skipNextAttackMotion) //if the character casts a skill indirectly they shouldn't play their attack motion
            {
                // Debug.Log($"{name}: Skipping attack motion");
                skipNextAttackMotion = false;
                return;
            }
            // Debug.Log($"{name}:Performing attack motion.");

            SpriteAnimator.State = SpriteState.Idle;

            SpriteAnimator.AnimSpeed = 1f;

            if (SpriteAnimator.Type == SpriteType.Player)
            {
                SpriteAnimator.ChangeMotion(SpriteMotion.Casting, true);
            }
            else
                SpriteAnimator.ChangeMotion(SpriteMotion.Attack1, true);

            // Debug.Log($"PerformBasicAttackMotion {name} speed {AttackAnimationSpeed}");
            //SpriteAnimator.AnimSpeed = AttackAnimationSpeed;
        }

        public void PerformBasicAttackMotion(CharacterSkill skill = CharacterSkill.None)
        {
            if (skipNextAttackMotion) //if the character casts a skill indirectly they shouldn't play their attack motion
            {
                // Debug.Log($"{name}: Skipping attack motion");
                skipNextAttackMotion = false;
                return;
            }
            // Debug.Log($"{name}:Performing attack motion.");

            if (SpriteAnimator.Type == SpriteType.Player)
            {
                switch (SpriteAnimator.PreferredAttackMotion)
                {
                    default:
                    case 1:
                        SpriteAnimator.ChangeMotion(SpriteMotion.Attack1, true);
                        break;
                    case 2:
                        SpriteAnimator.ChangeMotion(SpriteMotion.Attack2, true);
                        break;
                    case 3:
                        SpriteAnimator.ChangeMotion(SpriteMotion.Attack3, true);
                        break;
                }
            }
            else
                SpriteAnimator.ChangeMotion(SpriteMotion.Attack1, true);

            // Debug.Log($"PerformBasicAttackMotion {name} speed {AttackAnimationSpeed}");
            SpriteAnimator.AnimSpeed = AttackAnimationSpeed;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!isMoving || SpriteAnimator == null)
                return;

            var color = SpriteAnimator.Type == SpriteType.Player ? Color.blue : Color.red;

            var p1 = transform.localPosition;
            //p1.y = walkProvider.GetHeightForPosition(StartPos);

            for (var i = currentMoveStep; i < movePath.Count - 1; i++)
            {
                if (i > currentMoveStep)
                {
                    p1 = new Vector3(movePath[i].x + 0.5f, 0f, movePath[i].y + 0.5f);
                    p1.y = walkProvider.GetHeightForPosition(p1);
                }

                var p2 = new Vector3(movePath[i + 1].x + 0.5f, 0f, movePath[i + 1].y + 0.5f);
                p2.y = walkProvider.GetHeightForPosition(p2);

                Handles.DrawBezier(p1, p2, p1, p2, color, null, 10f);

                //Gizmos.DrawLine(p1, p2);
            }
        }
#endif

        public void SetHitDelay(float time)
        {
            if (hitLockImmuneTime < Time.time || time <= 0)
                hitDelay = time;
        }

        private static void CompleteDestroy(object go)
        {
            var go2 = go as GameObject;
            if (go2 == null)
                return;
            GameObject.Destroy(go2);
        }

        public void UpdateSnap(float f)
        {
        }

        public void UpdateFade(float f)
        {
            if (SpriteAnimator != null)
            {
                SpriteAnimator.Alpha = f;
                SpriteAnimator.SetDirty();
            }

            if (shadowSprite != null)
                shadowSprite.color = new Color(1f, 1f, 1f, f / 2f);
        }

        public void FadeOutAndVanish(float time)
        {
            var lt = LeanTween.value(gameObject, UpdateFade, 1, 0, time);

            lt.setOnComplete(CompleteDestroy, gameObject);
        }

        private IEnumerator MonsterDeathCoroutine()
        {
            var time = Messages.TimeUntilMessageLogClears(EntityMessageType.ShowDamage);

            yield return new WaitForSeconds(time);
            if (SpriteAnimator == null)
            {
                FadeOutAndVanish(2f);
                yield break;
            }

            //yield return new WaitForSeconds(SpriteAnimator.GetHitTiming());

            var deathTiming = SpriteAnimator.GetDeathTiming();
            SpriteAnimator.State = SpriteState.Dead;
            SpriteAnimator.ChangeMotion(SpriteMotion.Dead, true);
            SpriteAnimator.AnimSpeed = 1f;

            yield return new WaitForSeconds(deathTiming);

            if (CameraFollower.Instance.SelectedTarget == gameObject)
                CameraFollower.Instance.ClearSelected();

            //yield return new WaitForSeconds(2f);

            FadeOutAndVanish(2f);
        }

        public void MonsterDie()
        {
            isMoving = false;
            movePath = null;

            FloatingDisplay.Close();
            FloatingDisplay = null;

            StopCasting();

            StartCoroutine(MonsterDeathCoroutine());
        }

        public void UpdateHp(int hp)
        {
            EnsureFloatingDisplayCreated();
        }

        //     public void BlastOff(Vector3 direction)
        //     {
        //         isMoving = false;
        //         movePath = null;

        //         FadeOutAndVanish(2f);

        //SpriteAnimator.DoSpin();

        //         StartCoroutine(BlastOffCoroutine(direction));
        //     }

        //      private IEnumerator BlastOffCoroutine(Vector3 direction)
        //      {
        //          var time = 0f;

        //          while (true)
        //          {
        //              var factor = 1f * Time.deltaTime * 0.8f;
        //              transform.localPosition += direction * Time.deltaTime * 0.5f;
        //              transform.localScale += new Vector3(factor, factor, factor) * 3f;
        //              time += Time.deltaTime;
        //              if (time < 0.5f)
        //                  yield return null;
        //              else
        //              {
        //                  var go = GameObject.Instantiate(Resources.Load<GameObject>("Explosion"));
        //                  go.transform.localPosition = gameObject.transform.localPosition;
        //			go.transform.localScale = new Vector3(5f, 5f, 5f);
        //                  break;
        //              }
        //          }
        //	if(gameObject != null)
        //	    GameObject.Destroy(gameObject);
        //}

        public void AttachFloatingTextIndicator(string text)
        {
            var di = RagnarokEffectPool.GetDamageIndicator();
            var height = 1f;
            var color = "white";
            di.DoDamage(TextIndicatorType.Miss, text, new Vector3(0f, 0.6f, 0f), height,
                SpriteAnimator.Direction, color, false);
            di.AttachDamageIndicator(this);
        }

        
        private void AttachMissIndicator()
        {
            var di = RagnarokEffectPool.GetDamageIndicator();
            var height = 1f;
            var color = IsAlly ? "red" : "white";
            di.DoDamage(TextIndicatorType.Miss, "<font-weight=\"300\">Miss", new Vector3(0f, 0.6f, 0f), height,
                SpriteAnimator.Direction, color, false);
            di.AttachDamageIndicator(this);
        }

        private void AttachHealIndicator(int damage)
        {
            var di = RagnarokEffectPool.GetDamageIndicator();
            var height = 1f;
            di.DoDamage(TextIndicatorType.Heal, damage.ToString(), new Vector3(0f, 0.6f, 0f), height,
                SpriteAnimator.Direction, "green", false);
            di.AttachDamageIndicator(this);
        }

        private void AttachCriticalDamageIndicator(int damage, int totalDamage)
        {
            var di = RagnarokEffectPool.GetDamageIndicator();
            var red = SpriteAnimator.Type == SpriteType.Player;
            var color = red ? "#FF8888" : "#FFFF00";
            var height = 1f;
            di.DoDamage(TextIndicatorType.Critical, damage.ToString(), gameObject.transform.localPosition, height,
                SpriteAnimator.Direction, color, true);

            if (totalDamage > 0 && CharacterType != CharacterType.Player)
            {
                var di2 = RagnarokEffectPool.GetDamageIndicator();
                di2.DoDamage(TextIndicatorType.ComboDamage, $"{totalDamage}", Vector3.zero, height,
                    SpriteAnimator.Direction, color, false);
                di2.AttachComboIndicatorToControllable(this);
            }
        }

        private void AttachDamageIndicator(int damage, int totalDamage)
        {
            var di = RagnarokEffectPool.GetDamageIndicator();
            var red = SpriteAnimator.Type == SpriteType.Player;
            var height = 1f;
            di.DoDamage(TextIndicatorType.Damage, damage.ToString(), gameObject.transform.localPosition, height,
                SpriteAnimator.Direction, red ? "red" : null, false);

            if (totalDamage > 0 && CharacterType != CharacterType.Player)
            {
                var di2 = RagnarokEffectPool.GetDamageIndicator();
                di2.DoDamage(TextIndicatorType.ComboDamage, $"{totalDamage}", Vector3.zero, height,
                    SpriteAnimator.Direction, "#FFFF00", false);
                di2.AttachComboIndicatorToControllable(this);
            }
        }

        private void OnMessageFaceDirection(EntityMessage msg)
        {
            SpriteAnimator.Angle = RoAnimationHelper.FacingDirectionToRotation((Direction)msg.Value1);
            // Debug.Log($"{Time.timeSinceLevelLoad} - {Name}: OnMessageFacingDirection({(Direction)msg.Value1})");
            
        }
        
        private void OnMessageAttackMotion(EntityMessage msg)
        {
            if(msg.Entity != null)
                LookAt(msg.Entity.transform.position);
            
            // Debug.Log($"{Time.timeSinceLevelLoad} - {Name}: OnMessageAttackMotion({msg.Float1})");

            SetAttackAnimationSpeed(msg.Float1);
            
            PerformBasicAttackMotion((CharacterSkill)msg.Value1);
        }

        private void OnMessageHitEffect(EntityMessage msg)
        {
            if (msg.Value1 == 2) //temporary bad way to handle hit2
            {
                if (msg.Entity != null)
                    HitEffect.Hit2(msg.Entity, this);
                return;
            }

            var hitPosition = transform.position + new Vector3(0, 2, 0);
            if (msg.Entity != null)
                HitEffect.Hit1(msg.Entity.SpriteAnimator.transform.position + new Vector3(0, 2, 0), hitPosition);
            else
            {
                if (SpriteMode == ClientSpriteType.Sprite)
                {
                    var dir = RoAnimationHelper.FacingDirectionToVector(SpriteAnimator.Direction);
                    var srcPos = hitPosition + new Vector3(dir.x, 0f, dir.y);
                    HitEffect.Hit1(srcPos, hitPosition);
                }
                else
                {
                    var dir = transform.forward;
                    var srcPos = hitPosition + dir;
                    HitEffect.Hit1(srcPos, hitPosition);
                }
            }
        }

        public void OnMessageDamageEvent(EntityMessage msg)
        {
            var dmg = msg.Value1;
            if (dmg < 0)
            {
                AttachHealIndicator(-dmg);
                return;
            }

            var hasEndure = StatusEffectState != null && StatusEffectState.HasStatusEffect(CharacterStatusEffect.Endure);

            if(!hasEndure)
                movePauseTime = DebugValueHolder.GetOrDefault("movePauseTime", 0.2f);
            UpdateMovePosition();

            var weaponClass = 0;
            if (msg.Entity != null)
                weaponClass = msg.Entity.WeaponClass;
            
            if (SpriteAnimator.CurrentMotion != SpriteMotion.Dead && !hasEndure)
            {
                if (SpriteAnimator.Type == SpriteType.Player)
                    SpriteAnimator.State = SpriteState.Standby;

                if (!SpriteAnimator.IsAttackMotion)
                {
                    SpriteAnimator.AnimSpeed = 1f;
                    SpriteAnimator.ChangeMotion(SpriteMotion.Hit, true);
                }
            }

            if (weaponClass >= 0)
            {
                var hitSound = ClientDataLoader.Instance.GetHitSoundForWeapon(weaponClass);
                AudioManager.Instance.OneShotSoundEffect(Id, hitSound, transform.position, 1f);
            }

            if(msg.Value3 > 0)
                AttachCriticalDamageIndicator(dmg, msg.Value2);//DamageIndicator(dmg, msg.Value2);
            else
                AttachDamageIndicator(dmg, msg.Value2);
        }

        public void ExecuteMessage(EntityMessage msg)
        {
            switch (msg.Type)
            {
                case EntityMessageType.HitEffect:
                    OnMessageHitEffect(msg);
                    break;
                case EntityMessageType.ShowDamage:
                    OnMessageDamageEvent(msg);
                    break;
                case EntityMessageType.Miss:
                    AttachMissIndicator();
                    break;
                case EntityMessageType.AttackMotion:
                    OnMessageAttackMotion(msg);
                    break;
                case EntityMessageType.FaceDirection:
                    OnMessageFaceDirection(msg);
                    break;
                default:
                    Debug.LogError($"Unhandled entity message type {msg.Type} on entity {Name}!");
                    break;
            }
            
            EntityMessagePool.Return(msg);
        }

        public void HandleMessages()
        {
            while (Messages.TryGetMessage(out var msg))
                ExecuteMessage(msg);
        }

        private void Update()
        {
            if (FloatingDisplay != null)
                SnapDialog();

            if (SpriteMode == ClientSpriteType.Prefab)
                return;

            if (IsMainCharacter)
            {
                var mc = MinimapController.Instance;
                if (mc == null || SpriteAnimator == null)
                    return;

                MinimapController.Instance.SetPlayerPosition(CellPosition, Directions.GetAngleForDirection(SpriteAnimator.Direction) + 180f);
            }

            if (SpriteAnimator.SpriteData == null)
                return;

            HandleMessages();

            var noShadowState = SpriteAnimator.CurrentMotion == SpriteMotion.Sit || SpriteAnimator.CurrentMotion == SpriteMotion.Dead || IsHidden;
            if (shadowSprite != null && (noShadowState != !shadowSprite.gameObject.activeInHierarchy))
                shadowSprite.gameObject.SetActive(!noShadowState);

            if (SpriteAnimator) SpriteAnimator.SetRenderActive(!IsHidden);

            //this is dumb
            tempSpeedTime -= Time.deltaTime;
            hitDelay -= Time.deltaTime;
            movePauseTime -= Time.deltaTime;
            PosLockTime -= Time.deltaTime;

            if (hitDelay >= 0f)
            {
                Debug.Log($"{name} hitDelay {hitDelay}");
                return;
            }

            if (IsCasting && uniqueAttackAction != null && Time.timeSinceLevelLoad > uniqueAttackStart)
            {
                SpriteAnimator.ChangeMotion((SpriteMotion)uniqueAttackAction.Animation);
                skipNextAttackMotion = true;
                uniqueAttackAction = null;
            }

            if (isMoving)
            {
                var newPosition = new Vector2(RealPosition.x, RealPosition.z);
                var distance = Vector2.Distance(lastFramePosition, newPosition);
                // Debug.Log($"{lastFramePosition} -> {newPosition} = {distance}");
                SpriteAnimator.MoveDistance += distance;
                lastFramePosition = newPosition;

                UpdateMove2();

                if (movePauseTime > 0f)
                {
                    // Debug.Log($"{name} movePauseTime {movePauseTime}");
                    // SpriteAnimator.AnimSpeed = 1f;
                    // if (CharacterType == CharacterType.Player && SpriteAnimator.CurrentMotion != SpriteMotion.Hit)
                    //     SpriteAnimator.ChangeMotion(SpriteMotion.Standby);
                    return;
                }

                if (SpriteAnimator.State != SpriteState.Walking || SpriteAnimator.CurrentMotion != SpriteMotion.Walk)
                {
                    // Debug.Log($"{name} switching to walking");
                    SpriteAnimator.AnimSpeed = 1f; //moveSpeed * 4f;
                    // var action = SpriteAnimator.GetActionForMotion(SpriteMotion.Walk);
                    // var frameTime = action.Frames.Length * (action.Delay / 1000f);
                    // // Debug.Log($"{moveSpeed} {frameTime} {action.Frames.Length} {action.Delay}");
                    // if(CharacterType == CharacterType.Player)
                    //     SpriteAnimator.AnimSpeed = moveSpeed * 4f / frameTime;
                    // else
                    //     SpriteAnimator.AnimSpeed = 1f; //hack, you should calculate this properly
                    SpriteAnimator.State = SpriteState.Walking;
                    SpriteAnimator.ChangeMotion(SpriteMotion.Walk);
                }
            }
            else
            {
                if (SpriteAnimator.State == SpriteState.Walking && SpriteAnimator.CurrentMotion != SpriteMotion.Hit)
                {
                    //hold in the move animation for 0.05s. Keeps us from falling to idle between server move updates for continuous moving.
                    //SpriteAnimator.QueueMotionTransition(SpriteMotion.Idle, 3/60f); 
                    SpriteAnimator.AnimSpeed = 1f;
                    SpriteAnimator.State = SpriteState.Idle;
                    SpriteAnimator.ChangeMotion(SpriteMotion.Idle);
                }

                
                transform.position = Vector3.Lerp(transform.position, RealPosition + PositionOffset, Time.deltaTime * 20f);
            }
        }

        private void OnDestroy()
        {
            if (shadowMaterial != null)
                Destroy(shadowMaterial);
            LeanTween.cancel(gameObject);

            if (PopupDialog != null)
                Destroy(PopupDialog);

            if (FloatingDisplay != null)
            {
                FloatingDisplay.Close();
                FloatingDisplay = null;
            }

            if (EffectList != null)
            {
                for (var i = 0; i < EffectList.Count; i++)
                    EffectList[i].EndEffect();

                EffectList.Clear();
            }

            if (ComboIndicator != null)
            {
                var di = ComboIndicator.GetComponent<DamageIndicator>();
                di.EndDamageIndicator();
            }
        }

        public void OnEffectEnd(Ragnarok3dEffect effect)
        {
            EffectList.Remove(effect);
        }
    }
}