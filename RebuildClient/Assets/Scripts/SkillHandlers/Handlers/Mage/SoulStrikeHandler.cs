using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Effects.EffectHandlers.Skills;
using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.SoulStrike)]
    public class SoulStrikeHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target?.Messages.SendHitEffect(attack.Src, attack.DamageTiming, 2, 1); //only once
            attack.Target?.Messages.SendHitEffect(attack.Src, attack.DamageTiming, 1, attack.HitCount);
        }
        
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Ghost));
            target?.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void ExecuteSkillTargeted([CanBeNull] ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion(true);
            src?.LookAtOrDefault(attack.Target);
            if (src != null && attack.Target != null && attack.Result != AttackResult.Invisible)
                SoulStrikeEffect.LaunchEffect(src, attack.Target.gameObject, attack.HitCount, false);
        }
    }
}