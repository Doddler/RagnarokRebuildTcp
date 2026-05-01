using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.IncreaseAgility, SkillClass.Magic, SkillTarget.Ally)]
    public class IncreaseAgiHandler : SkillHandlerBase
    {
        public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;

        public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target,
            Position position, int lvl, bool isIndirect, bool isItemSource)
        {
            if (target == null)
                return SkillValidationResult.InvalidTarget;

            if (target.Character.Type == CharacterType.Player && target.IsElementBaseType(CharacterElement.Undead1))
                return SkillValidationResult.Failure;

            return base.ValidateTarget(source, target, position, lvl, false, false);
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            if (target == null)
            {
                ServerLogger.LogWarning($"Entity {source.Character.Name} is attempting to cast Increase Agi without a target.");
                return;
            }

            var duration = 40 + 20 * lvl; //60s-240s

            if (lvl > 10)
                duration = 60;

            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.IncreaseAgi, duration, lvl);
            target.AddStatusEffect(status);

            if (!isIndirect)
            {
                source.ApplyCooldownForSupportSkillAction();
                source.ApplyAfterCastDelay(1f);
            }

            var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.IncreaseAgility);

            GenericCastAndInformSupportSkill(source, target, CharacterSkill.IncreaseAgility, lvl, ref res, isIndirect);
        }
    }
}