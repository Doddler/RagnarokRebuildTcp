using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.Blessing, SkillClass.Magic, SkillTarget.Ally)]
    public class BlessingHandler : SkillHandlerBase
    {
        public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl)
        {
            if (target == null)
                return SkillValidationResult.InvalidTarget;
            
            if (target.Character.Type == CharacterType.Player && target.IsElementBaseType(CharacterElement.Undead1))
                return SkillValidationResult.Failure;

            return base.ValidateTarget(source, target, position, lvl);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            if (target == null)
            {
                ServerLogger.LogWarning($"Entity {source.Character.Name} is attempting to cast Blessing without a target.");
                return;
            }

            var hasRemoval = target.CleanseStatusEffect(StatusCleanseTarget.Curse | StatusCleanseTarget.Petrify);
            if (!hasRemoval)
            {
                if (target.IsElementBaseType(CharacterElement.Undead1) || target.GetRace() == CharacterRace.Demon)
                {
                    var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Blessing, 40f + 20 * lvl, 0, 0, 0, 1);
                    target.AddStatusEffect(status);
                }
                else
                {
                    var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Blessing, 40f + 20 * lvl, lvl);
                    target.AddStatusEffect(status);
                }
            }

            source.ApplyAfterCastDelay(0.5f);
            source.ApplyCooldownForSupportSkillAction();
            var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.Blessing);
            GenericCastAndInformSupportSkill(source, target, CharacterSkill.Blessing, lvl, ref res);
        }
    }
}
