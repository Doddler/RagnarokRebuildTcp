using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("AgiDown")]
    public class AgiDownEffect : IEffectHandler
    {
        public static void LaunchEffect(ServerControllable source)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.AgiDown);
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
            for (var i = 0; i < effect.Primitives.Count; i++)
            {
                var p = effect.Primitives[i];
                if (!p.IsActive)
                    continue;
                p.Velocity -= new Vector3(0f, 0.2f, 0f); 
            }
            
            if (step % 2 == 0 && step < 60)
            {
                var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillSpriteAlphaBlendedNoZCheck);
                var prim = effect.LaunchPrimitive(PrimitiveType.DirectionalBillboard, mat, 0.833f);
                var data = prim.GetPrimitiveData<EffectSpriteData>();

                data.Atlas = EffectSharedMaterialManager.SpriteAtlas;
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
                var offset = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 4, Mathf.Cos(angle * Mathf.Deg2Rad) * dist);

                prim.Velocity = new Vector3(0f, -0.001f, 0f); //this direction normalized is used for billboard heading, so make it not zero
                prim.transform.localPosition = offset;
            }
                

            return step < effect.DurationFrames;
        }
    }
}