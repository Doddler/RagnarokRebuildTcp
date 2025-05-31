using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.HeavensDrive)]
    public class HeavensDriveHandler : SkillHandlerBase
    {
        public override bool DoesAttackTakeWeaponSound => false;

        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendElementalHitEffect(attack.Src, attack.MotionTime, AttackElement.Earth, attack.HitCount);
        }
        
        public override void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            CreateGroundTargetCircle(src, target, 3, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Earth));
        }

        public override void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();
        }
    }
}