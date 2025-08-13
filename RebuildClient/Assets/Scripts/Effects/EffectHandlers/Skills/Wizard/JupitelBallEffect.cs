using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("JupitelBall")]
    public class JupitelBallEffect : IEffectHandler
    {
        static string[] JupitelBallSprites = new[]{ "thunder_ball_a", "thunder_ball_b", "thunder_ball_c", "thunder_ball_d", "thunder_ball_e", "thunder_ball_f"};
        
        public static Ragnarok3dEffect Attach(ServerControllable src, ServerControllable target, float delayTime)
        {
            var travelTime = (src.transform.position - target.transform.position).magnitude * 0.03f;
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.JupitelBall);
            effect.FollowTarget = null;
            effect.SourceEntity = src;
            effect.AimTarget = target.gameObject;
            effect.SetDurationByTime(travelTime);
            effect.ActiveDelay = delayTime;
            effect.UpdateOnlyOnFrameChange = false;
            effect.DestroyOnTargetLost = false;
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillSpriteAlphaAdditiveNoZCheck);
            effect.SetBillboardMode(BillboardStyle.Normal);

            effect.transform.position = src.transform.position;
            
            AudioManager.Instance.AttachSoundToEntity(src.Id, "hunter_shockwavetrap.ogg", effect.gameObject);
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.SourceEntity == null || effect.AimTarget == null)
                return false;
            
            if (step == 0 && effect.IsStepFrame)
            {
                var prim1 = effect.LaunchPrimitive(PrimitiveType.Texture2D, effect.Material, 0.333f);
                var data1 = prim1.GetPrimitiveData<Texture2DData>();
                
                data1.Sprite = EffectSharedMaterialManager.SpriteAtlas.GetSprite("thunder_center");
                data1.Alpha = 170;
                data1.AlphaSpeed = 0;
                data1.Size = new Vector2(3.5f / 5f, 3.5f / 5f);
                data1.MinSize = Vector2.zero;
                data1.MaxSize = new Vector2(9999, 9999);
                data1.FadeOutLength = 0;
                data1.ScalingSpeed = Vector2.zero;
                data1.ScalingAccel = Vector2.zero;
                data1.Speed = Vector3.zero;
                data1.Acceleration = Vector2.zero;
                
                prim1.transform.localPosition = new Vector3(0f, 1f, 0f);
                prim1.transform.localRotation = Quaternion.identity;
                
                
                var prim = effect.LaunchPrimitive(PrimitiveType.AnimatedTexture2D, effect.Material, 0.333f);
                var data = prim.GetPrimitiveData<EffectSpriteData>();
                
                data.Atlas = EffectSharedMaterialManager.SpriteAtlas;
                data.AnimateTexture = true;
                data.FrameTime = 60;
                data.TextureCount = 5;
                data.Alpha = 255;
                data.Width = 4.5f / 5f;
                data.Height = 4.5f / 5f;
                data.Angle = 0;
                data.RotationSpeed = 5f;
                data.Style = BillboardStyle.None;
                data.SpriteList = JupitelBallSprites;
                
                prim.transform.localPosition = new Vector3(0f, 1f, 0f);
                prim.transform.localRotation = Quaternion.identity;
            }

            
            effect.transform.position = Vector3.Lerp(effect.SourceEntity.transform.position, effect.AimTarget.transform.position, pos / effect.Duration);
            
            return effect.IsTimerActive;
        }
    }
}