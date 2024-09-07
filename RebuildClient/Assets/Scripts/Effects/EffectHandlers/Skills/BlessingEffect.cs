using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("Blessing")]
    public class BlessingEffect : IEffectHandler
    {
        public static RoSpriteData BlessingEffectSprite;
        public static bool IsLoadingSprite;

        private static AsyncOperationHandle<RoSpriteData> spriteLoadTask;

        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Blessing);

            effect.transform.localScale = Vector3.one;
            effect.SourceEntity = target;
            effect.SetDurationByFrames(120);
            effect.FollowTarget = target.gameObject;
            effect.UpdateOnlyOnFrameChange = true;
            effect.SetSortingGroup("Default", 0); //appear in front of damage indicators

            if (BlessingEffectSprite == null && !IsLoadingSprite)
            {
                IsLoadingSprite = true;
                spriteLoadTask = Addressables.LoadAssetAsync<RoSpriteData>("Assets/Effects/Custom/blessingedited.spr");
            }

            return null;
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

                BlessingEffectSprite = spriteLoadTask.Result;
                IsLoadingSprite = false;
            }

            if (step == 0)
            {
                var circlePrim = effect.LaunchPrimitive(PrimitiveType.Circle2D, DefaultSkillCastEffect.GetCircleMaterial(), 2f);
                var cData = circlePrim.GetPrimitiveData<CircleData>();
                var scale = 1f;

                circlePrim.transform.localScale = new Vector3(scale, scale, scale);
                circlePrim.transform.localPosition += new Vector3(0f, 0f, -0f);
                circlePrim.transform.localRotation = Quaternion.Euler(-90, 0, 0);

                cData.Alpha = 60;
                cData.MaxAlpha = 120;
                cData.AlphaSpeed = cData.MaxAlpha / 0.5f;
                cData.FadeOutLength = 0.5f;
                cData.Radius = 1f;
                cData.FillCircle = true;
                cData.Color = new Color(0x20 / 255f, 0xb0 / 255f, 0xe8 / 255f);
            }

            if (step < 10 && step % 3 == 0)
            {
                var sprite = effect.LaunchSpriteEffect(BlessingEffectSprite, 2f, 2.666f);
                sprite.SpriteAnimator.BaseColor = new Color(1f, 1f, 1f, (200 - step * 12) / 255f);
                sprite.SpriteAnimator.AnimSpeed = 0.33f;
                sprite.SpriteAnimator.OverrideCurrentFrame(10);
                //sprite.SpriteAnimator.SpriteRenderer.SetOffset(6f); //pull offset towards camera as it's root is tilted away due to the billboard
                sprite.IsLoop = true;

                effect.AttachToBillboardGroup(BillboardStyle.Character, sprite.gameObject);

                sprite.transform.localPosition = new Vector3(0f, 0f, 0f);
                sprite.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                sprite.transform.localRotation = Quaternion.identity;
            }
            
            if (step <= 90 && step % 4 == 0)
            {
                var duration = 1f;
                var velocity = Random.Range(2.4f, 4.8f);
                var particle = new EffectParticle()
                {
                    Lifetime = duration,
                    FadeStartTime = duration - (duration / 5f),
                    Size = 0.3f,
                    AlphaSpeed = 6f,
                    AlphaMax = 1f,
                    Color = new Color32(255,255,255,255),
                    Position = Quaternion.Euler(0, Random.Range(0, 360), 0) * new Vector3(0, 3.6f, Random.Range(0.3f, 1.6f)),
                    Velocity = new Vector3(0, -velocity, 0),
                    Acceleration = -(velocity / duration) / 2f,
                    ParticleId = 5, //blue star
                    Mode = ParticleDisplayMode.Pulse,
                    RelativeTarget = effect.BillboardGroup.gameObject //we want the particles to move with the player
                };
                EffectParticleManager.Instance.AddParticle(ref particle);
            }

            return step < effect.DurationFrames + 60; //live a little extra longer so the particles can finish
        }
    }
}