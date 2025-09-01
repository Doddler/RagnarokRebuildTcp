using System;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Misc
{
    public class BirdFollower : MonoBehaviour
    {
        public ServerControllable TargetCharacter;
        public int BirdStyle;
        private RoSpriteAnimator spriteAnimator;

        private AsyncOperationHandle<RoSpriteData> spriteLoadTask;
        private bool isLoaded;

        private bool isAttacking;
        private bool isAnimating;
        private bool inRegainHeight;

        private float timeOfStartAttack;
        private Vector3 attackVector;

        public void AttachBird(ServerControllable followTarget, int birdStyle)
        {
            TargetCharacter = followTarget;
            BirdStyle = birdStyle;

            var birdSprite = "Assets/Sprites/Effects/매.spr";


            spriteLoadTask = Addressables.LoadAssetAsync<RoSpriteData>(birdSprite);
            isLoaded = false;
        }

        public void LookAt(Vector3 lookAt)
        {
            var pos1 = new Vector2(transform.position.x, transform.position.z);
            var pos2 = new Vector2(lookAt.x, lookAt.z);
            var dir = (pos2 - pos1).normalized;
            var angle = Vector2.SignedAngle(dir, Vector2.up);
            if (angle < 0)
                angle += 360f;

            if (spriteAnimator != null)
                spriteAnimator.ChangeAngle(angle);
        }

        public void LoadSprite()
        {
            isLoaded = true;

            var billboard = gameObject.AddComponent<BillboardObject>();
            billboard.Style = BillboardStyle.Character;

            var child = new GameObject("Sprite");
            child.layer = LayerMask.NameToLayer("Characters");
            child.transform.SetParent(gameObject.transform, false);
            child.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            child.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            spriteAnimator = child.AddComponent<RoSpriteAnimator>();
            spriteAnimator.Type = SpriteType.Npc;
            // sprite.State = SpriteState.Dead;
            // sprite.SpriteRenderer = sr;
            spriteAnimator.RaycastForShadow = false;
            spriteAnimator.ChangeMotion(SpriteMotion.Idle);
            spriteAnimator.State = SpriteState.Idle;
            spriteAnimator.BaseColor = Color.white;
            spriteAnimator.OnSpriteDataLoadNoCollider(spriteLoadTask.Result);
            spriteAnimator.ChangeActionExact(0);
            spriteAnimator.OverrideDelay = 100;
            //spriteAnimator.PauseAnimation();

            transform.position = TargetCharacter.transform.position + new Vector3(0f, 5f, 0f);
            spriteAnimator.Angle = TargetCharacter.SpriteAnimator.Angle;
        }

        public void LaunchAttackTarget(ServerControllable target)
        {
            isAnimating = false;
            isAttacking = true;
            timeOfStartAttack = Time.timeSinceLevelLoad;

            SwitchToAttack();
            var targetPos = target.transform.position + new Vector3(0f, 0.6f, 0f);
            attackVector = targetPos - transform.position;
            LookAt(target.transform.position);
            
            Debug.Log(attackVector.magnitude);

            if (attackVector.magnitude < 6f)
            {
                var oldY = attackVector.y;
                attackVector.Normalize();
                attackVector *= 6f;
                attackVector.y = oldY;
            }
        }

        private void SwitchToIdle()
        {
            spriteAnimator.ChangeMotion(SpriteMotion.Idle);
            spriteAnimator.State = SpriteState.Idle;
            spriteAnimator.ChangeActionExact(0);
            spriteAnimator.OverrideDelay = 100;
        }

        private void SwitchToAttack()
        {
            spriteAnimator.ChangeMotion(SpriteMotion.Walk);
            spriteAnimator.State = SpriteState.Walking;
            spriteAnimator.ChangeActionExact(8);
            spriteAnimator.PauseAnimation();
        }

        public void LateUpdate()
        {
            if (TargetCharacter == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!isLoaded)
            {
                if (!spriteLoadTask.IsDone)
                    return;
                LoadSprite();
            }

            if (!isAttacking)
            {
                var targetPos2d = new Vector2(TargetCharacter.transform.position.x, TargetCharacter.transform.position.z);
                var ourPos2D = new Vector2(transform.position.x, transform.position.z);

                var distance = Vector2.Distance(targetPos2d, ourPos2D);
                var targetPos = TargetCharacter.transform.position + new Vector3(0f, 5f, 0f);
                var difference = transform.position - targetPos;
                var velocity = Vector3.zero;

                if (distance > 5f && !isAnimating)
                {
                    if (spriteAnimator.State != SpriteState.Idle)
                    {
                        SwitchToIdle();
                        spriteAnimator.PauseAnimation();
                    }

                    velocity = difference / 25 * 60f;
                    LookAt(TargetCharacter.transform.position);
                }
                else if (distance <= 5f && !isAnimating)
                {
                    if (spriteAnimator.State != SpriteState.Idle)
                        SwitchToIdle();

                    velocity = difference / 25 * 60f;
                    LookAt(TargetCharacter.transform.position);
                    isAnimating = true;
                    spriteAnimator.Unpause();
                }
                else if (distance > 2.4f)
                {
                    velocity = difference / distance / 5 * 60f;
                    LookAt(TargetCharacter.transform.position);
                }
                else if (distance > 0.6f)
                {
                    velocity = difference / 20 * 60f;
                    LookAt(TargetCharacter.transform.position);
                }
                else
                {
                    if(Mathf.Approximately(difference.y, 0))
                        return;
                }

                velocity.y = difference.y / 5 * 60f;

                var moveDistance = -velocity * Mathf.Clamp(Time.deltaTime, 0, 0.1f); //if you get less than 10fps the bird will be slow, but better than overshooting
                
                transform.position += moveDistance;
            }
            else
            {
                var timeSinceAttack = Time.timeSinceLevelLoad - timeOfStartAttack;

                var move = attackVector / 10 * 60f;
                if (timeSinceAttack > 0.167f && timeSinceAttack < 0.3f)
                {
                    move.y = 0;
                    inRegainHeight = false;
                }

                if (timeSinceAttack >= 0.3f && !inRegainHeight)
                {
                    attackVector.y = Mathf.Abs(transform.position.y - (TargetCharacter.transform.position.y + 5f));
                    move = attackVector / 10 * 60f;
                    inRegainHeight = true;
                }

                if (timeSinceAttack > 0.467f)
                    isAttacking = false;
                
                var moveDistance = move * Mathf.Clamp(Time.deltaTime, 0, 0.1f); //if you get less than 10fps the bird will be slow, but better than overshooting
                transform.position += moveDistance;
            }
        }
    }
}