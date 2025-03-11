using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.StatusEffects
{
    [RoEffect("Silence")]
    public class SilenceEffect : IEffectHandler
    {
        public static Ragnarok3dEffect LaunchSilenceEffect(ServerControllable controllable, float length)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Silence);
            effect.SetDurationByTime(length); //we'll manually end this early probably
            effect.FollowTarget = controllable.gameObject;
            effect.SourceEntity = controllable;
            effect.DataValue = Time.timeSinceLevelLoad;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.SourceEntity == null || effect.SourceEntity.SpriteAnimator == null)
                return false;

            if(step % 90 == 0)
                ClientDataLoader.Instance.AttachEmote(effect.SourceEntity, 9);

            return true; //stay until we are removed
        }
    }
}