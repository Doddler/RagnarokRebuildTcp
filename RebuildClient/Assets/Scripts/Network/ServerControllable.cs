using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Effects;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Assets.Scripts.Network
{
    public class ServerControllable : MonoBehaviour, IEffectOwner
    {
        private RoWalkDataProvider walkProvider;

        public CharacterType CharacterType;
        public RoSpriteAnimator SpriteAnimator;
        public int Id;
        public Vector2Int Position;
        public Vector3 StartPos;
        public float ShadowSize;
        public bool IsAlly;
        public bool IsMale;
        public bool IsMainCharacter;
        public bool IsInteractable;
        public int Level;
        public string Name { get; set; }
        public int Hp;
        public int MaxHp
        {
            get;
            set;
        }
        public int WeaponClass;

        public GameObject PopupDialog;
        public List<Ragnarok3dEffect> EffectList;

        public string DisplayName => CharacterType == CharacterType.NPC || !GameConfig.Data.ShowLevelsInOverlay ? Name : $"Lv.{Level} {Name}";

        public Vector3 CounterHitDir;

        public ClientSpriteType SpriteMode;
        public GameObject EntityObject;
        public GameObject ComboIndicator;

        public CharacterFloatingDisplay FloatingDisplay;
        private List<Vector2Int> movePath;
        private Vector2Int[] tempPath;
        private float moveSpeed = 0.2f;
        private float tempSpeed = -1f;
        private float tempSpeedTime = 0f;
        private float moveProgress;
        private float hitLockImmuneTime = 0f;
        private bool isDirectMove;
        private bool isHidden;
        public float PosLockTime;
        public float AttackAnimationSpeed = 1f;

        private UniqueAttackAction uniqueAttackAction;
        private float uniqueAttackStart;
        private bool skipNextAttackMotion;
        private Vector2 lastPosition;

        private bool isMoving;
        // {
        //     get => _isMoving;
        //     set
        //     {
        //         if (IsMainCharacter && isMoving && !value)
        //             Debug.Log($"Stopping main character.");
        //         _isMoving = value;
        //     }
        // }
        //private bool _isMoving;

        private Vector3 snapStartPos;
        private LTDescr snapAnim;

        private SpriteRenderer shadowSprite;
        private Material shadowMaterial;

        private float hitDelay = 0f;
        private float dialogCountdown = 0f;
        private float movePauseTime = 0f;
        public bool IsMoving => isMoving;
        public bool IsCasting;

        public bool IsWalking => movePath != null && movePath.Count > 1;

        public bool IsHidden
        {
            get => isHidden;
            set
            {
                isHidden = value;
                RefreshHiddenState();
            }
        }

        public CharacterFloatingDisplay EnsureFloatingDisplayCreated(bool makeEnabled = false)
        {
            if (FloatingDisplay == null)
                FloatingDisplay = NetworkManager.Instance.OverlayManager.GetNewFloatingDisplay();
            if(makeEnabled)
                FloatingDisplay.gameObject.SetActive(true);
            return FloatingDisplay;
        }

        public void SetHp(int hp, int maxHp)
        {
            FloatingDisplay.UpdateMaxHp(maxHp);
            SetHp(hp);
        }
        
        public void SetHp(int hp)
        {
            if (GameConfig.Data.AutoHideFullHPBars && hp >= MaxHp)
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
            }
            else
                FloatingDisplay = FloatingDisplay;
            if(CharacterType != CharacterType.NPC)
                FloatingDisplay.UpdateHp(hp);
        }

        public void ShowSkillCastMessage(CharacterSkill skill, float duration = 5f)
        {
            if (CharacterType != CharacterType.Player)
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
            
            if(CharacterType == CharacterType.Player)
                FloatingDisplay.ShowChatBubbleMessage(sName + "!!");
            else
                FloatingDisplay.ShowChatBubbleMessage("<size=-2><color=#FF8888>" + sName + "</size>", duration);

            if (SpriteAnimator != null && ClientDataLoader.Instance.GetUniqueAction(SpriteAnimator.SpriteData.Name, skill, out var action))
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
        }

        public void RefreshHiddenState()
        {
            if (shadowSprite)
                shadowSprite.enabled = !isHidden;
            if (SpriteAnimator)
                SpriteAnimator.SetRenderActive(!isHidden);
        }

        public void ShowName(string name)
        {
            EnsureFloatingDisplayCreated();
            FloatingDisplay.UpdateName(name);
            FloatingDisplay.ShowNamePlate();
        }

        public void HideName()
        {
            if (FloatingDisplay == null)
                return;
            FloatingDisplay.HideNamePlate();
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
            
            if(IsMainCharacter)
                FloatingDisplay.transform.SetAsLastSibling();

            var cf = CameraFollower.Instance;
            var rect = FloatingDisplay.transform as RectTransform;
            var screenPos = cf.Camera.WorldToScreenPoint(transform.position);
            
            var d = 70 / cf.Distance;
            var reverseScale = 1f / cf.CanvasScaler.scaleFactor;

            if (!GameConfig.Data.ScalePlayerDisplayWithZoom)
                d = 1f;
            
            rect.localScale = new Vector3(d,d,d);
            rect.anchoredPosition = new Vector2(screenPos.x * reverseScale, (screenPos.y - cf.UiCanvas.pixelRect.height) * reverseScale);
        }

        public void ConfigureEntity(int id, Vector2Int worldPos, Direction direction)
        {
            if (walkProvider == null)
                walkProvider = RoWalkDataProvider.Instance;

            Id = id;

            Position = worldPos;

            var offset = 0f;
            if (SpriteMode == ClientSpriteType.Prefab)
                offset = 0.15f; //haaack

            var start = new Vector3(worldPos.x + 0.5f, 0f, worldPos.y + 0.5f);
            var position = new Vector3(start.x, walkProvider.GetHeightForPosition(start) + offset, start.z);

            transform.localPosition = position;

            if (SpriteMode == ClientSpriteType.Sprite)
                SpriteAnimator.Direction = direction;
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
            var src = new Vector2(transform.position.x - 0.5f, transform.position.z - 0.5f);
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

        public void StartMove(float speed, float progress, int stepCount, int curStep, List<Vector2Int> steps)
        {
            // if (IsMainCharacter)
            //     Debug.Log(
            //         $"{name} Start Move - From {steps[0]} to {steps[stepCount - 1]}\nSpeed:{speed} Progress:{progress} StepCount:{stepCount} CurStep:{curStep} StepLength:{steps.Count}");

            moveSpeed = speed;
            //tempSpeed = -1;
            isDirectMove = false;

            //don't reset start pos if the next tile is the same
            if (movePath == null || movePath.Count <= 1 || movePath[1] != steps[1])
                //    if (stepCount > 1)
                StartPos = transform.position - new Vector3(0.5f, 0f, 0.5f);

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

        public void UpdateMove(bool forceUpdate = false)
        {
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
                    SpriteAnimator.Direction = Directions.GetFacingForAngle(angle);
                }
                else
                {
                    var offset = movePath[1] - movePath[0];
                    if (IsDiagonal(GetDirectionForOffset(offset)))
                        moveProgress -= Time.deltaTime / speed * 0.70f;
                    else
                        moveProgress -= Time.deltaTime / speed;
                    SpriteAnimator.Direction = GetDirectionForOffset(offset);
                }
            }

            if (moveProgress < 0.5f)
                Position = movePath[1];
            
            while (moveProgress < 0f && movePath.Count > 1)
            {
                movePath.RemoveAt(0);
                StartPos = new Vector3(movePath[0].x, walkProvider.GetHeightForPosition(transform.position), movePath[0].y);
                Position = movePath[0];
                moveProgress += 1f;
                // tempSpeed = -1;
            }

            if (movePath.Count == 0)
                Debug.Log("WAAA");

            if (PosLockTime > 0f && !forceUpdate)
                return;

            if (movePath.Count == 1)
            {
                Position = movePath[0];
                transform.position = new Vector3(Position.x + 0.5f, walkProvider.GetHeightForPosition(transform.position), Position.y + 0.5f);
                movePath.Clear();
                // if (IsMainCharacter)
                //     Debug.Log($"We've finished pathing, stopping character.");
                isMoving = false;
            }
            else
            {
                var xPos = Mathf.Lerp(StartPos.x, movePath[1].x, 1 - moveProgress);
                var yPos = Mathf.Lerp(StartPos.z, movePath[1].y, 1 - moveProgress);

                transform.position = new Vector3(xPos + 0.5f, walkProvider.GetHeightForPosition(transform.position), yPos + 0.5f);

                //var offset = movePath[1] - movePath[0];
            }
        }

        public void MovePosition(Vector2Int targetPosition)
        {
            transform.position = new Vector3(targetPosition.x + 0.5f, walkProvider.GetHeightForPosition(transform.position), targetPosition.y + 0.5f);
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
            var targetPos = new Vector3(position.x + 0.5f, walkProvider.GetHeightForPosition(transform.position), position.y + 0.5f);

            LeanTween.cancel(gameObject);

            // if (IsMainCharacter)
            //     Debug.Log(
            //         $"SnapToTile {Name} has distance {(transform.localPosition - targetPos).magnitude} and speed of {snapSpeed}f. {leeway}f required to execute snap.");


            if ((transform.localPosition - targetPos).magnitude > 0.75f)
                LeanTween.move(gameObject, targetPos, 0.07f);
        }

        public void StopImmediate(Vector2Int position, bool snapToTile = true)
        {
            isMoving = false;
            isDirectMove = false;
            movePath = null;
            if (SpriteAnimator.State == SpriteState.Walking)
                SpriteAnimator.ChangeMotion(SpriteMotion.Idle);

            // if (IsMainCharacter)
            //     Debug.Log($"Character asked to stop immediately.\nSnap to tile if out of range:{snapToTile}");

            if (snapToTile)
                SnapToTile(position);
        }

        public void AttachEffect(Ragnarok3dEffect effect)
        {
            if(EffectList == null)
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
                baseMotionTime = 0.5f;
            if (baseMotionTime < motionTime)
            {
                AttackAnimationSpeed = 1;
                return;
            }

            AttackAnimationSpeed = motionTime / baseMotionTime;
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

            for (var i = 0; i < movePath.Count - 1; i++)
            {
                var p1 = new Vector3(movePath[i].x + 0.5f, 0f, movePath[i].y + 0.5f);
                p1.y = walkProvider.GetHeightForPosition(p1);

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

        private IEnumerator MonsterDeathCoroutine(int hitCount)
        {
            if (hitCount > 1)
            {
                var hitTiming = 0.2f; //SpriteAnimator.GetHitTiming();
                for (var i = 0; i < hitCount; i++)
                {
                    SpriteAnimator.ChangeMotion(SpriteMotion.Hit, true);
                    SpriteAnimator.AnimSpeed = 1f;
                    if (i == hitCount - 1)
                        hitTiming = SpriteAnimator.GetHitTiming();
                    yield return new WaitForSeconds(hitTiming);
                }
            }

            var deathTiming = SpriteAnimator.GetDeathTiming();
            SpriteAnimator.State = SpriteState.Dead;
            SpriteAnimator.ChangeMotion(SpriteMotion.Dead, true);
            SpriteAnimator.AnimSpeed = 1f;

            yield return new WaitForSeconds(deathTiming);

            if (CameraFollower.Instance.SelectedTarget == gameObject)
                CameraFollower.Instance.ClearSelected();

            yield return new WaitForSeconds(2f);

            FadeOutAndVanish(2f);
        }

        public void MonsterDie(int hitCount)
        {
            isMoving = false;
            movePath = null;
            
            FloatingDisplay.Close();
            FloatingDisplay = null;

            StopCasting();

            StartCoroutine(MonsterDeathCoroutine(hitCount));
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
        private void Update()
        {
            if(FloatingDisplay != null)
                SnapDialog();
            
            if (SpriteMode == ClientSpriteType.Prefab)
                return;
            
            if (IsMainCharacter)
            {
                var mc = MinimapController.Instance;
                if (mc == null || SpriteAnimator == null)
                    return;

                //Debug.Log(transform.position);
                MinimapController.Instance.SetPlayerPosition(new Vector2Int((int)transform.position.x, (int)transform.position.z),
                    Directions.GetAngleForDirection(SpriteAnimator.Direction) + 180f);
            }
            
            if(SpriteAnimator.SpriteData == null)
                return;

            //this is dumb
            tempSpeedTime -= Time.deltaTime;
            hitDelay -= Time.deltaTime;
            movePauseTime -= Time.deltaTime;
            PosLockTime -= Time.deltaTime;
            
            if (hitDelay >= 0f)
                return;
            
            if (IsCasting && uniqueAttackAction != null && Time.timeSinceLevelLoad > uniqueAttackStart)
            {
                SpriteAnimator.ChangeMotion((SpriteMotion)uniqueAttackAction.Animation);
                skipNextAttackMotion = true;
                uniqueAttackAction = null;
            }

            if (isMoving)
            {
                var newPosition = new Vector2(transform.position.x, transform.position.z);
                var distance = Vector2.Distance(lastPosition, newPosition);
                // Debug.Log($"{lastPosition} -> {newPosition} = {distance}");
                SpriteAnimator.MoveDistance += distance;
                lastPosition = newPosition;
                
                if (movePauseTime > 0f)
                {
                    SpriteAnimator.AnimSpeed = 1f;
                    if (CharacterType == CharacterType.Player && SpriteAnimator.CurrentMotion != SpriteMotion.Hit)
                        SpriteAnimator.ChangeMotion(SpriteMotion.Standby);
                    return;
                }

                UpdateMove();

                if (SpriteAnimator.State != SpriteState.Walking && SpriteAnimator.CurrentMotion != SpriteMotion.Hit)
                {
                    SpriteAnimator.AnimSpeed = moveSpeed * 4f;
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
                    SpriteAnimator.AnimSpeed = 1f;
                    SpriteAnimator.State = SpriteState.Idle;
                    SpriteAnimator.ChangeMotion(SpriteMotion.Idle);
                }
            }

            if (IsMainCharacter)
                RefreshHiddenState();
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