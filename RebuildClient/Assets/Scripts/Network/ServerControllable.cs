﻿using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Effects;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
        public string Name;
        public int Hp;
        public int MaxHp;

        public GameObject PopupDialog;
        public List<Ragnarok3dEffect> EffectList;

        public string DisplayName => CharacterType == CharacterType.NPC ? Name : $"Lv.{Level} {Name}";

        public Vector3 CounterHitDir;

        public ClientSpriteType SpriteMode;
        public GameObject EntityObject;
        public GameObject ComboIndicator;

        private List<Vector2Int> movePath;
        private Vector2Int[] tempPath;
        private float moveSpeed = 0.2f;
        private float moveProgress;
        private bool isMoving;
        private bool isHidden;

        private SpriteRenderer shadowSprite;
        private Material shadowMaterial;

        private float hitDelay = 0f;
        private float dialogCountdown = 0f;
        private float movePauseTime = 0f;

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

        public void RefreshHiddenState()
        {
            if (shadowSprite)
                shadowSprite.enabled = !isHidden;
            if (SpriteAnimator)
                SpriteAnimator.SetRenderActive(!isHidden);
        }

        public void DialogBox(string text)
        {
            if (PopupDialog == null)
                PopupDialog = GameObject.Instantiate(Resources.Load<GameObject>("Dialog"));

            PopupDialog.transform.SetParent(CameraFollower.Instance.UiCanvas.transform);
            PopupDialog.transform.localScale = Vector3.one;

            PopupDialog.GetComponent<CharacterChat>().SetText(text);
            //textObject.text = text;

            SnapDialog();
            dialogCountdown = 8f;
        }

        public void SnapDialog()
        {
            if (PopupDialog == null)
                return;

            var rect = PopupDialog.transform as RectTransform;

            var screenPos = Camera.main.WorldToScreenPoint(transform.position + new Vector3(0f, 4f, 0f));

            //Debug.Log(screenPos);

            var reverseScale = 1f / CameraFollower.Instance.CanvasScaler.scaleFactor;

            rect.anchoredPosition = new Vector2(screenPos.x * reverseScale,
                ((screenPos.y - CameraFollower.Instance.UiCanvas.pixelRect.height) + 0) * reverseScale);
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

        public void StartMove(float speed, float progress, int stepCount, int curStep, List<Vector2Int> steps)
        {
            // Debug.Log($"{name} Start Move - Speed:{speed} Progress:{progress} StepCount:{stepCount} CurStep:{curStep} StepLength:{steps.Count}");
            
            //don't reset start pos if the next tile is the same
            if (movePath == null || movePath.Count <= 1 || movePath[1] != steps[1])
                StartPos = transform.position - new Vector3(0.5f, 0f, 0.5f);

            moveSpeed = speed;
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

        private void UpdateMove()
        {
            if (movePath.Count == 0 || SpriteMode == ClientSpriteType.Prefab) return;

            if (movePath.Count > 1)
            {
                var offset = movePath[1] - movePath[0];
                if (IsDiagonal(GetDirectionForOffset(offset)))
                    moveProgress -= Time.deltaTime / moveSpeed * 0.80f;
                else
                    moveProgress -= Time.deltaTime / moveSpeed;
                SpriteAnimator.Direction = GetDirectionForOffset(offset);
            }

            while (moveProgress < 0f && movePath.Count > 1)
            {
                movePath.RemoveAt(0);
                StartPos = new Vector3(movePath[0].x, walkProvider.GetHeightForPosition(transform.position), movePath[0].y);
                Position = movePath[0];
                moveProgress += 1f;
            }

            if (movePath.Count == 0)
                Debug.Log("WAAA");

            if (movePath.Count == 1)
            {
                Position = movePath[0];
                transform.position = new Vector3(Position.x + 0.5f, walkProvider.GetHeightForPosition(transform.position), Position.y + 0.5f);
                movePath.Clear();
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

        public void StopImmediate(Vector2Int position)
        {
            isMoving = false;
            movePath = null;
            SpriteAnimator.ChangeMotion(SpriteMotion.Idle);

            var targetPos = new Vector3(position.x + 0.5f, walkProvider.GetHeightForPosition(transform.position), position.y + 0.5f);

            LeanTween.cancel(gameObject);

            if ((transform.localPosition - targetPos).magnitude > 1f)
                LeanTween.move(gameObject, targetPos, 0.2f);
        }

        public void AttachEffect(Ragnarok3dEffect effect)
        {
#if UNITY_EDITOR
            if (EffectList.Contains(effect))
            {
                Debug.LogWarning($"Attempting to attach effect {effect} but it is already attached!");
                return;
            }
#endif

            EffectList.Add(effect);
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
            hitDelay = time;
        }

        private static Action<object> completeDestroy = (object go) =>
        {
            var go2 = go as GameObject;
            if (go2 == null)
                return;
            GameObject.Destroy(go2);
        };

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

            lt.setOnComplete(completeDestroy, gameObject);
        }

        private IEnumerator MonsterDeathCoroutine(int hitCount)
        {
            if (hitCount > 1)
            {
                var hitTiming = 0.2f; //SpriteAnimator.GetHitTiming();
                for (var i = 0; i < hitCount; i++)
                {
                    SpriteAnimator.ChangeMotion(SpriteMotion.Hit, true);
                    if (i == hitCount - 1)
                        hitTiming = SpriteAnimator.GetHitTiming();
                    yield return new WaitForSeconds(hitTiming);
                }
            }

            var deathTiming = SpriteAnimator.GetDeathTiming();
            SpriteAnimator.State = SpriteState.Dead;
            SpriteAnimator.ChangeMotion(SpriteMotion.Dead, true);

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

            StartCoroutine(MonsterDeathCoroutine(hitCount));
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
            if (PopupDialog != null)
            {
                dialogCountdown -= Time.deltaTime;
                if (dialogCountdown < 0)
                    GameObject.Destroy(PopupDialog);
                else
                    SnapDialog();
            }

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

            hitDelay -= Time.deltaTime;
            if (hitDelay >= 0f)
                return;

            if (isMoving)
            {
                movePauseTime -= Time.deltaTime;
                if (movePauseTime > 0f)
                    return;
                
                UpdateMove();

                if (SpriteAnimator.State != SpriteState.Walking)
                {
                    SpriteAnimator.AnimSpeed = moveSpeed / 0.2f;
                    SpriteAnimator.State = SpriteState.Walking;
                    SpriteAnimator.ChangeMotion(SpriteMotion.Walk);
                }
            }
            else
            {
                if (SpriteAnimator.State == SpriteState.Walking)
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