using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.StatusEffects
{
    [RoEffect("Stun")]
    public class StunEffect : IEffectHandler
    {
        public static RoSpriteData StunEffectSprite;
        public static bool IsLoadingSprite;

        private static AsyncOperationHandle<RoSpriteData> spriteLoadTask;

        public static Ragnarok3dEffect AttachStunEffect(ServerControllable stunnedTarget)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Stun);
            effect.SetDurationByTime(10f); //we'll manually end this early probably
            effect.FollowTarget = stunnedTarget.gameObject;
            effect.SourceEntity = stunnedTarget;

            if (StunEffectSprite == null && !IsLoadingSprite)
            {
                IsLoadingSprite = true;
                spriteLoadTask = Addressables.LoadAssetAsync<RoSpriteData>("Assets/Sprites/Effects/status-stun.spr");
            }
            
            stunnedTarget.AttachEffect(effect);
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (IsLoadingSprite)
            {
                // Debug.Log($"Status: {spriteLoadTask.Status}");
                if (spriteLoadTask.Status == AsyncOperationStatus.None)
                {
                    effect.ResetStep();
                    return true; //wait until the sprite loads
                }

                StunEffectSprite = spriteLoadTask.Result;
                IsLoadingSprite = false;
            }

            if (step == 0)
            {
                var target = effect.FollowTarget.GetComponent<ServerControllable>();
                if (target == null)
                    return false;
                var height = target.GetStandingHeight() * 0.667f;
                
                var sprite = effect.LaunchSpriteEffect(StunEffectSprite, 10f, height);
                sprite.SpriteAnimator.BaseColor = new Color(1f, 1f, 1f, 1f);
                sprite.SpriteAnimator.AnimSpeed = 1f;
                sprite.SpriteAnimator.ChangeActionExact(0);
                ((RoSpriteRendererStandard)sprite.SpriteAnimator.SpriteRenderer).SetOverrideMaterial(EffectHelpers.NoZTestMaterial);
                sprite.IsLoop = true;

                effect.AttachToBillboardGroup(BillboardStyle.Character, sprite.gameObject);

                sprite.transform.localPosition = new Vector3(0f, 0f, -0.3f);
                sprite.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                sprite.transform.localRotation = Quaternion.identity;
                
                AudioManager.Instance.OneShotSoundEffect(-1, "_stun.ogg", effect.transform.position, 0.7f);
            }

            return pos < effect.Duration;
        }
    }
}