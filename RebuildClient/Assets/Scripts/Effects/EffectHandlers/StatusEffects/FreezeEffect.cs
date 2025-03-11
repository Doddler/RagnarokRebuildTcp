using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.StatusEffects
{
    [RoEffect("Freeze")]
    public class FreezeEffect : IEffectHandler
    {
        public static RoSpriteData EffectSprite;
        public static bool IsLoadingSprite;

        private static AsyncOperationHandle<RoSpriteData> spriteLoadTask;

        public static Ragnarok3dEffect AttachFreezeEffect(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Freeze);
            effect.SetDurationByTime(120f); //we'll manually end this early probably
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;

            if (EffectSprite == null && !IsLoadingSprite)
            {
                IsLoadingSprite = true;
                spriteLoadTask = Addressables.LoadAssetAsync<RoSpriteData>("Assets/Sprites/Effects/얼음땡.spr");
            }

            target.AttachEffect(effect);
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

                EffectSprite = spriteLoadTask.Result;
                IsLoadingSprite = false;
            }

            if (step == 0)
            {
                var sprite = effect.LaunchSpriteEffect(EffectSprite, 120f, 0f);
                sprite.SpriteAnimator.BaseColor = new Color(1f, 1f, 1f, 1f);
                sprite.SpriteAnimator.AnimSpeed = 1f;
                sprite.SpriteAnimator.ChangeActionExact(0);
                sprite.SpriteAnimator.SpriteOrder = 50;
                //((RoSpriteRendererStandard)sprite.SpriteAnimator.SpriteRenderer).SetOverrideMaterial(EffectHelpers.NoZTestMaterial);
                sprite.IsLoop = true;

                effect.AttachToBillboardGroup(BillboardStyle.Character, sprite.gameObject);

                sprite.transform.localPosition = new Vector3(0f, 0f, -0.3f);
                sprite.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                sprite.transform.localRotation = Quaternion.identity;

                //AudioManager.Instance.OneShotSoundEffect(-1, "_stun.ogg", effect.transform.position, 0.7f);
            }

            return pos < effect.Duration;
        }

        public void OnEvent(Ragnarok3dEffect effect, RagnarokPrimitive sender)
        {
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;

            if (effect.SpriteEffects == null || effect.SpriteEffects.Count < 1)
            {
                effect.EndEffect();
                return;
            }

            AudioManager.Instance.OneShotSoundEffect(-1, "_frozen_explosion.ogg", effect.transform.position, 0.8f);
            var spr = effect.SpriteEffects[0];
            spr.SpriteAnimator.ChangeActionExact(1);
            spr.SpriteAnimator.DisableLoop = true;
            effect.SetDurationByTime(effect.CurrentPos + 0.51f);
        }
    }
}