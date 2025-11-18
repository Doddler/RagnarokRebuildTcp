using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.SkillHandlers;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Knight
{
    [RoEffect("SpearBoomerang")]
    public class SpearBoomerangEffect: IEffectHandler
    {
        public static Ragnarok3dEffect CreateSpearBoomerang(ServerControllable source, GameObject target, float delayTime)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.SpearBoomerang);
            effect.SourceEntity = source;
            effect.AimTarget = target;
            effect.SetDurationByTime(60);
            effect.UpdateOnlyOnFrameChange = true;
            effect.ActiveDelay = delayTime;
            effect.PositionOffset = target.transform.position;
            
            EffectSharedMaterialManager.PrepareEffectSprite("Assets/Sprites/Effects/창.spr");

            return effect;
        }
        
        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0 || step == 4 || step == 8 || step == 12)
            {
                if (!EffectSharedMaterialManager.TryGetEffectSprite("Assets/Sprites/Effects/창.spr", out _))
                {
                    effect.ResetStep();
                    return true; //wait until the sprite loads
                }

                var c = step switch
                {
                    4 => new Color32(255, 255, 255, 180),
                    8 => new Color32(255, 255, 255, 130),
                    12 => new Color32(255, 255, 255, 80),
                    _ => new Color32(255, 255, 255, 255)
                };

                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntity.Id, "ef_fireball.ogg", effect.SourceEntity.transform.position, 0.5f);
                RoSpriteProjectileEffect.CreateProjectile(effect.SourceEntity, effect.AimTarget, "Assets/Sprites/Effects/창.spr", c, 0);
            }

            return step < effect.DurationFrames;
        }
    }
}