using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("Ruwach")]
    public class RuwachEffect : IEffectHandler
    {
        public static RoSpriteData SightEffectSprite;
        public static bool IsLoadingSprite;
        private static AsyncOperationHandle<RoSpriteData> spriteLoadTask;

        public static Ragnarok3dEffect LaunchRuwach(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Ruwach);
            effect.SetDurationByFrames(999999);
            effect.SourceEntity = target;
            effect.FollowTarget = target.gameObject;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.position = target.transform.position;
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.ParticleAlphaBlend);

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

            if (step % 3 == 0 && effect.Flags[0] != 1)
            {
                var angle = (step / 1.3f) * 5f;
                var dist = 15f / 5f;

                {
                    var prim = effect.LaunchPrimitive(PrimitiveType.Texture3D, effect.Material, 25 / 60f);
                    prim.SetBillboardMode(BillboardStyle.Normal);
                    var data = prim.GetPrimitiveData<Texture3DData>();

                    var offset = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 10 / 5f, Mathf.Cos(angle * Mathf.Deg2Rad) * dist);

                    data.Sprite = EffectSharedMaterialManager.GetParticleSpriteAtlas().GetSprite("particle2");
                    data.Size = new Vector2(3f, 3f);
                    data.ScalingSpeed = -new Vector2(6, 6);
                    data.Alpha = 250;
                    data.AlphaMax = 250;
                    data.AlphaSpeed = -3f * 60;
                    data.MinSize = Vector2.zero;
                    data.MaxSize = data.Size;
                    data.IsStandingQuad = true;
                    data.FadeOutTime = 0.1f;
                    data.Color = Color.white;
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
                    data.ScalingSpeed = -data.Size / (25 / 60f);
                    data.Alpha = 150;
                    data.AlphaMax = 150;
                    data.AlphaSpeed = -2.5f * 60;
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