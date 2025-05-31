using Assets.Scripts.Network;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    public class StormGustEffect: IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.RoSprite);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(999999);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            return effect.IsTimerActive;
        }
    }
}