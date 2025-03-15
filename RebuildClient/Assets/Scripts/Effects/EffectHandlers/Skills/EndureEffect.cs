using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("Endure")]
    public class EndureEffect : IEffectHandler
    {
        public static void Launch(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Endure);
            effect.SetDurationByFrames(120);
            effect.SetBillboardMode(BillboardStyle.Normal);
            effect.UpdateOnlyOnFrameChange = true;
            effect.FollowTarget = target.gameObject;
            
            AudioManager.Instance.OneShotSoundEffect(target.Id, "ef_endure.ogg", target.transform.position, 0.7f);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0)
            {
                var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillSpriteAlphaBlendedNoZCheck);
                var duration = 1.2f;
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture2D, mat, duration);
                var data = prim.GetPrimitiveData<Texture2DData>();

                data.Alpha = 0;
                data.AlphaSpeed = 255 / 20f * 60f;
                data.Size = new Vector2(7.5f, 7.5f);
                data.MinSize = Vector2.zero;
                data.MaxSize = new Vector2(9999, 9999);
                data.FadeOutLength = duration / 3f;
                data.ScalingSpeed = new Vector2(-0.32f, -0.32f) * 60f;
                data.ChangedScalingSpeed = Vector2.zero;
                data.ScalingChangeStep = 35;
                data.ScalingAccel = -data.ScalingSpeed / 35f * 60f;
                data.Speed = Vector2.zero;
                data.Acceleration = Vector2.zero;
                data.Sprite = EffectSharedMaterialManager.GetSkillSpriteAtlas().GetSprite("endure");
                
                prim.transform.localPosition = new Vector3(0f, 1f, 0f);
                prim.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                prim.transform.localRotation = Quaternion.identity;
            }
            
            if (step < 40)
            {
                var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillSpriteAlphaBlendedNoZCheck);
                var duration = 0.666f;
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture2D, mat, duration);
                var data = prim.GetPrimitiveData<Texture2DData>();

                var angle = Random.Range(0, 360f);
                var rotation = Quaternion.Euler(0f, 0f, angle);
                var ySize = Random.Range(1.5f, 2.5f);
                var distance = (Random.Range(5f, 7f) - ySize) / 1.5f;
                
                data.Alpha = 0;
                data.AlphaSpeed = 255 / 20f * 60f;
                data.Size = new Vector2(Random.Range(1.5f, 2.5f), Random.Range(3, 8f) / 100f);
                data.MinSize = Vector2.zero;
                data.MaxSize = new Vector2(9999, 9999);
                data.FadeOutLength = duration / 3f;
                data.ScalingSpeed = Vector2.zero;
                data.ScalingAccel = Vector2.zero;
                data.Speed = rotation * new Vector2(0f, -(distance / 30f) * 60);
                data.Acceleration = -data.Speed / duration;
                data.Sprite = EffectSharedMaterialManager.GetSkillSpriteAtlas().GetSprite("alpha_down");
                
                prim.transform.localPosition = rotation * new Vector3(0f, distance - data.Size.y, 0f) + new Vector3(0f, 1f, 0f);
                prim.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                prim.transform.localRotation = Quaternion.Euler(0, 0, angle + -90f);
            }

            return step < effect.DurationFrames;
        }
    }
}