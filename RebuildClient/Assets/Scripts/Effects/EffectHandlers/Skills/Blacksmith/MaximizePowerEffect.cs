using Assets.Scripts.Network;
using Assets.Scripts.Objects;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Blacksmith
{
    [RoEffect("MaximizePower")]
    public class MaximizePowerEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.MaximizePower);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(1.5f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;
            
            
            CameraFollower.Instance.AttachEffectToEntity("MaximizePower", target.gameObject, target.Id);

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(step == 0)
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntity.Id, "black_maximize_power_circle.ogg", effect.transform.position, 0.7f);
            if(step == 40)
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntity.Id, "black_maximize_power_sword.ogg", effect.transform.position, 0.7f);
            if(step == 47)
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntity.Id, "black_maximize_power_sword.ogg", effect.transform.position, 0.7f);
            if(step == 65)
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntity.Id, "black_maximize_power_sword_bic.ogg", effect.transform.position, 0.7f);
            
            return effect.IsTimerActive;
        }
    }
}