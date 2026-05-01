using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers.Assassin
{
    [SkillHandler(CharacterSkill.VenomDust)]
    public class VenomDustHandler : SkillHandlerBase
    {
        public override int GetSkillAoESize(ServerControllable src, int lvl) => 2;

        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Poison));
            
            if(target != null)
                target.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.LookAt(attack.TargetAoE.ToWorldPosition());
            src.PerformSkillMotion();
            CameraFollower.Instance.CreateEffectAtLocation("VenomDust", attack.TargetAoE.ToWorldPosition(), new Vector3(1.5f, 1.5f, 1.5f), 0);
            AudioManager.Instance.OneShotSoundEffect(src.Id, "assasin_poisonreact.ogg", attack.TargetAoE.ToWorldPosition(), 1f);
        }
    }
}