using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("JupitelHit")]
    public class JupitelHitEffect : IEffectHandler
    {
        static string[] JupitelHitSprites = new[]{ "thunder_plazma_blast_a", "thunder_plazma_blast_b", "thunder_ball_d", "thunder_ball_e", "thunder_ball_f"};
        
        public static Ragnarok3dEffect Attach(ServerControllable target, float duration, float delayTime)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.JupitelHit);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(duration);
            effect.ActiveDelay = delayTime;
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillSpriteAlphaAdditiveNoZCheck);
            effect.SetBillboardMode(BillboardStyle.Normal);
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step % 20 == 0)
            {
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture2D, effect.Material, 0.166f);
                var data = prim.GetPrimitiveData<Texture2DData>();
                
                data.Sprite = EffectSharedMaterialManager.SpriteAtlas.GetSprite("thunder_pang");
                data.Alpha = 255;
                data.AlphaSpeed = 0;
                data.Size = Vector2.zero;
                data.MinSize = Vector2.zero;
                data.MaxSize = new Vector2(9999, 9999);
                data.FadeOutLength = 0;
                data.ScalingSpeed = new Vector2(2.5f, 2.5f) / 5f * 60f;
                data.ScalingAccel = -data.ScalingSpeed / 50f;
                data.Speed = Vector3.zero;
                data.Acceleration = Vector2.zero;
                
                prim.transform.localPosition = new Vector3(0f, 1f, 0f);
                prim.transform.localRotation = Quaternion.identity;
            }

            if (step == 10)
            {
                var prim = effect.LaunchPrimitive(PrimitiveType.AnimatedTexture2D, effect.Material, effect.Duration);
                var data = prim.GetPrimitiveData<EffectSpriteData>();
                
                data.Atlas = EffectSharedMaterialManager.SpriteAtlas;
                data.AnimateTexture = true;
                data.FrameTime = 60;
                data.TextureCount = 5;
                data.Alpha = 255;
                data.Width = 7.5f / 5f;
                data.Height = 7.5f / 5f;
                data.Style = BillboardStyle.None;
                data.SpriteList = JupitelHitSprites;
                
                prim.transform.localPosition = new Vector3(0f, 1f, 0f);
                prim.transform.localRotation = Quaternion.identity;
            }
            
            return effect.IsTimerActive;
        }
    }
}