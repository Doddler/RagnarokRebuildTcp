using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("AgiUp")]
    public class AgiUpEffect : IEffectHandler
    {
        public static void LaunchAgiUp(ServerControllable source)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.AgiUp);
            effect.SourceEntity = source;
            effect.UpdateOnlyOnFrameChange = true;
            effect.SetDurationByFrames(100);
            effect.FollowTarget = source.gameObject;
            effect.DestroyOnTargetLost = true;
            effect.transform.position = source.transform.position;
            effect.transform.localScale = Vector3.one;
        }

        private static readonly string[] SpriteList = new[] { "ac_center2" };
        
        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step % 2 == 0 && step < 60)
            {
                var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillSpriteAlphaBlended);
                var prim = effect.LaunchPrimitive(PrimitiveType.DirectionalBillboard, mat, 0.833f);
                var data = prim.GetPrimitiveData<EffectSpriteData>();

                data.Atlas = EffectSharedMaterialManager.GetSkillSpriteAtlas();
                data.Style = BillboardStyle.AxisAligned;
                data.Width = Random.Range(3f, 9f) / 5f;
                data.Height = 0.16f / 5f;
                data.SpriteList = SpriteList;
                data.BaseRotation = new Vector3(0, 0, -90);
                data.MaxAlpha = 200;
                data.Alpha = 0;
                data.AlphaSpeed = data.MaxAlpha / 20f;
                data.FadeOutLength = 0.33f;
                
                var angle = Random.Range(0, 360f);
                var dist = Random.Range(2, 9) / 5f;
                var offset = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 0, Mathf.Cos(angle * Mathf.Deg2Rad) * dist);

                var speed = Random.Range(0.2f, 0.7f) * 10;
                prim.Velocity = new Vector3(0f, speed, 0f);
                prim.transform.localPosition = offset;
            }
                

            return step < effect.DurationFrames;
        }
    }
}