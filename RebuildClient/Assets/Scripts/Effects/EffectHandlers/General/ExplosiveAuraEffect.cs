using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("ExplosiveAura")]
    public class ExplosiveAuraEffect : IEffectHandler
    {
        public static Ragnarok3dEffect AttachExplosiveAura(GameObject target, int density, Color color)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.ExplosiveAura);
            effect.SetDurationByTime(300f);
            effect.FollowTarget = target;
            effect.DestroyOnTargetLost = true;
            effect.transform.position = target.transform.position;
            effect.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            
            AudioManager.Instance.OneShotSoundEffect(-1, "mon_폭기.ogg", effect.transform.position, 1.2f);
            AudioManager.Instance.OneShotSoundEffect(-1, "mon_폭기.ogg", effect.transform.position, 1.2f);

            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillSpriteAlphaBlended);
            var prim = effect.LaunchPrimitive(PrimitiveType.ExplosiveAura, mat, 300f);
            
            var data = prim.GetPrimitiveData<SimpleSpriteData>();
            data.Atlas = EffectSharedMaterialManager.SpriteAtlas;
            data.Color = color;
            //density = 1;
            
            prim.CreateParts(density);
            for (var i = 0; i < density; i++)
            {
                var p = prim.Parts[i];
                
                p.Active = true;
                for (var j = 0; j < 4; j++)
                {
                    var s = i * 4; //spark #
                    p.Flags[i] = Random.Range(0, 5); //sprite id
                    p.Heights[s] = 10; //start in fade phase
                    p.Heights[s + 1] = 70 + 60 * j; //starting alpha
                    p.Heights[s + 2] = Random.Range(0, 360f); //angle around character
                    p.Heights[s + 3] = Random.Range(3, 8); //height off ground
                }
            }
            
            return effect;
        }
    }
}