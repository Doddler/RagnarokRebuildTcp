using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.StatusEffects
{
    [RoEffect("Hide")]
    public class HideEffect : IEffectHandler
    {
        public static RoSpriteData HideEffectSprite;
        public static bool IsLoadingSprite;

        private static AsyncOperationHandle<RoSpriteData> spriteLoadTask;

        public static void AttachHideEffect(GameObject hideTarget)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Hide);
            effect.Duration = 2f; //we'll manually end this early probably
            effect.FollowTarget = hideTarget;

            if (HideEffectSprite == null && !IsLoadingSprite)
            {
                IsLoadingSprite = true;
                spriteLoadTask = Addressables.LoadAssetAsync<RoSpriteData>("Assets/Sprites/Effects/smoke.spr");
            }
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

                HideEffectSprite = spriteLoadTask.Result;
                IsLoadingSprite = false;
            }

            if (step == 0)
            {
                var sprite = effect.LaunchSpriteEffect(HideEffectSprite, 0.35f, 0f);
                sprite.SpriteAnimator.BaseColor = new Color(1f, 1f, 1f, 1f);
                sprite.SpriteAnimator.AnimSpeed = 0.21f;
                sprite.SpriteAnimator.ChangeActionExact(0);
                ((RoSpriteRendererStandard)sprite.SpriteAnimator.SpriteRenderer).SetOverrideMaterial(EffectHelpers.NoZTestMaterial);
                sprite.IsLoop = false;

                effect.AttachToBillboardGroup(BillboardStyle.Character, sprite.gameObject);

                sprite.transform.localPosition = new Vector3(0f, 0f, -0.3f);
                sprite.transform.localScale = new Vector3(2f, 2f, 2f);
                sprite.transform.localRotation = Quaternion.identity;
                
                //AudioManager.Instance.OneShotSoundEffect(-1, "_stun.ogg", effect.transform.position, 0.7f);
            }

            return pos < effect.Duration;
        }
    }
}