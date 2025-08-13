using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.StatusEffects
{
    [RoEffect("Sleep")]
    public class SleepEffect : IEffectHandler
    {
        public static RoSpriteData EffectSprite;
        public static bool IsLoadingSprite;

        private static AsyncOperationHandle<RoSpriteData> spriteLoadTask;

        public static Ragnarok3dEffect AttachSleepEffect(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Sleep);
            effect.SetDurationByTime(40f); //we'll manually end this early probably
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;

            if (EffectSprite == null && !IsLoadingSprite)
            {
                IsLoadingSprite = true;
                spriteLoadTask = Addressables.LoadAssetAsync<RoSpriteData>("Assets/Sprites/Effects/status-sleep.spr");
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
                var target = effect.FollowTarget.GetComponent<ServerControllable>();
                if (target == null)
                    return false;
                var height = 1.5f;
                if (target.SpriteAnimator?.SpriteData != null)
                    height = target.SpriteAnimator.SpriteData.StandingHeight / 50f;
                if (target.CharacterType == CharacterType.Player || target.CharacterType == CharacterType.PlayerLikeNpc)
                    height += 0.1f; //head height
                var sprite = effect.LaunchSpriteEffect(EffectSprite, 30f, height - height / 6f);
                sprite.SpriteAnimator.BaseColor = new Color(1f, 1f, 1f, 1f);
                sprite.SpriteAnimator.AnimSpeed = 1f;
                sprite.SpriteAnimator.ChangeActionExact(0);
                ((RoSpriteRendererStandard)sprite.SpriteAnimator.SpriteRenderer).SetOverrideMaterial(EffectHelpers.NoZTestMaterial);
                sprite.IsLoop = true;

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