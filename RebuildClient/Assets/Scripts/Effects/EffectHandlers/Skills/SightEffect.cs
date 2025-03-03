using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("Sight")]
    public class SightEffect : IEffectHandler
    {
        public static RoSpriteData SightEffectSprite;
        public static bool IsLoadingSprite;
        private static AsyncOperationHandle<RoSpriteData> spriteLoadTask;

        public static Ragnarok3dEffect LaunchSight(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Sight);
            effect.SetDurationByFrames(999999);
            effect.SourceEntity = target;
            effect.FollowTarget = target.gameObject;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.position = target.transform.position;
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SightEffect);

            if (SightEffectSprite == null && !IsLoadingSprite)
            {
                IsLoadingSprite = true;
                spriteLoadTask = Addressables.LoadAssetAsync<RoSpriteData>("Assets/Sprites/Effects/sight.spr");
            }

            return effect;
        }


        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (IsLoadingSprite)
            {
                if (spriteLoadTask.Status == AsyncOperationStatus.None)
                {
                    effect.ResetStep();
                    return true; //wait until the sprite loads
                }

                SightEffectSprite = spriteLoadTask.Result;
                effect.Material.mainTexture = SightEffectSprite.Sprites[0].texture;
                IsLoadingSprite = false;
            }

            if (effect.FollowTarget == null && effect.Flags[0] == 0)
            {
                effect.Flags[0] = 1;
                effect.SetRemainingDurationByFrames(20);
            }

            if (step % 2 == 0 && effect.Flags[0] != 1)
            {
                var angle = step * 5;
                var dist = 15f / 5f;

                {
                    var prim = effect.LaunchPrimitive(PrimitiveType.ParticleAnimatedSprite, effect.Material, 0.333f);
                    prim.SetBillboardMode(BillboardStyle.Normal);
                    var data = prim.GetPrimitiveData<SpriteParticleData>();

                    var offset = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 20 / 5f, Mathf.Cos(angle * Mathf.Deg2Rad) * dist);

                    data.SpriteData = SightEffectSprite;
                    data.Size = new Vector2(2.5f, 2.5f) * 1.5f;
                    data.ScalingSpeed = new Vector2(-0.1f, -0.1f) * (60f * 1.5f);
                    data.Alpha = 150;
                    data.AlphaSpeed = -3f * 60;
                    data.MinSize = Vector2.zero;
                    data.MaxSize = data.Size;
                    data.Frame = 0;
                    data.FrameSpeed = 3;
                    prim.Velocity = new Vector3(0f, 0.1f / 5f, 0f);
                    prim.transform.localPosition = offset;
                }
                
                {
                    var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.ShadowMaterial);
                    var prim = effect.LaunchPrimitive(PrimitiveType.Texture3D, mat, 0.333f);
                    prim.SetBillboardMode(BillboardStyle.Normal);
                    var data = prim.GetPrimitiveData<Texture3DData>();

                    var offset = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 0, Mathf.Cos(angle * Mathf.Deg2Rad) * dist);

                    data.Size = new Vector2(1.0f, 0.5f);
                    data.ScalingSpeed = -data.Size / 0.333f; //why is this scaling speed in speed per second and the other per frame? Who knows!
                    data.Alpha = 120;
                    data.AlphaSpeed = -3f * 60;
                    data.MinSize = Vector2.zero;
                    data.MaxSize = data.Size;
                    data.IsStandingQuad = true;
                    data.FadeOutTime = 0.1f;
                    data.Color = Color.white;
                    prim.Velocity = Vector3.zero;
                    prim.transform.localPosition = offset;
                }
            }

            return step < effect.DurationFrames;
        }
    }
}