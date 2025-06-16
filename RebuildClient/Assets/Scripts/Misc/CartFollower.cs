using Assets.Scripts.Effects;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Misc
{
    public class CartFollower : MonoBehaviour
    {
        public ServerControllable TargetCharacter;
        public int CartStyle;
        private RoSpriteAnimator spriteAnimator;

        private AsyncOperationHandle<RoSpriteData> spriteLoadTask;
        private bool isLoaded;

        public void AttachCart(ServerControllable followTarget, int cartStyle)
        {
            TargetCharacter = followTarget;
            CartStyle = cartStyle;
            spriteLoadTask = Addressables.LoadAssetAsync<RoSpriteData>("Assets/Sprites/Effects/손수레.spr");
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
        
        
        public void AttachShadow(Sprite spriteObj)
        {
            if (gameObject == null)
                return;
            var go = new GameObject("Shadow");
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f) * 1.5f;

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = spriteObj;

            var shader = ShaderCache.Instance.SpriteShaderNoZWrite;
            var mat = new Material(shader);
            mat.SetFloat("_Offset", 0.4f);
            mat.color = new Color(1f, 1f, 1f, 0.5f);
            mat.renderQueue = 2999;
            sprite.material = mat;

            sprite.sortingOrder = -1;

            spriteAnimator.Shadow = go;
            spriteAnimator.ShadowSortingGroup = go.AddComponent<SortingGroup>();
            spriteAnimator.ShadowSortingGroup.sortingOrder = -20001;
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
            spriteAnimator.ChangeMotion(SpriteMotion.Walk);
            spriteAnimator.State = SpriteState.Walking;
            spriteAnimator.BaseColor = Color.white;
            spriteAnimator.OnSpriteDataLoadNoCollider(spriteLoadTask.Result);
            spriteAnimator.ChangeActionExact(0);
            //spriteAnimator.PauseAnimation();

            var angle = (TargetCharacter.SpriteAnimator.Angle + 180) * Mathf.Deg2Rad;
            var trackPos = new Vector2(TargetCharacter.transform.position.x, TargetCharacter.transform.position.z);
            var v = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) + trackPos;

            spriteAnimator.Angle = angle;
            transform.position = v.ToWorldPosition();
            LookAt(TargetCharacter.transform.position);
            
            AddressableUtility.LoadSprite(gameObject, "shadow", AttachShadow);
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

            var us = new Vector2(transform.position.x, transform.position.z);
            var them = new Vector2(TargetCharacter.transform.position.x, TargetCharacter.transform.position.z);
            if (Vector2.Distance(us, them) > 1f)
            {
                var dir = (us - them).normalized;
                var target = dir + them;
                var dist = Vector2.Distance(us, target);
                transform.position = target.ToWorldPosition();
                spriteAnimator.Angle = Vector2.Angle(Vector2.up, dir);

                spriteAnimator.MoveDistance += dist;

            }
            
            if(!TargetCharacter.IsMoving)
                spriteAnimator.PauseAnimation();
            else
            {
                
                spriteAnimator.Unpause();
                
            }
                
            LookAt(TargetCharacter.transform.position);
        }
    }
}