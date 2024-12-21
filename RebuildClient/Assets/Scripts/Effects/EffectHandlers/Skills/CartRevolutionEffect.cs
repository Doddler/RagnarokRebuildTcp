using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.SkillHandlers;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("CartRevolution")]
    public class CartRevolutionEffect : IEffectHandler
    {
        public static void CreateCartRevolution(ServerControllable caster, ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.CartRevolution);
            effect.SetDurationByFrames(60);
            effect.FollowTarget = target != null ? target.gameObject : caster.gameObject;
            effect.SetBillboardMode(BillboardStyle.Character);
            
            CameraFollower.Instance.AttachEffectToEntity("CartRevolution", caster.gameObject, caster.Id);
            target.Messages.SendHitEffect(caster, 0.5f, 1);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 7 || step == 20)
                AudioManager.Instance.OneShotSoundEffect(-1, "ef_magnumbreak.ogg", effect.transform.position, 0.7f);

            if (step == 30)
            {
                effect.FollowTarget = null;
                CameraFollower.Instance.AttachEffectToEntity("CartRevolution", effect.gameObject);
            }
            
            return step < effect.DurationFrames;
        }
    }
}