using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte
{
    [SkillHandler(CharacterSkill.IncreaseAgility, SkillClass.Magic, SkillTarget.Any)]
    public class IncreaseAgiHandler : SkillHandlerBase
    {
        public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
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

            var res = DamageInfo.EmptyResult(source.Entity, target.Entity);
            res.AttackSkill = CharacterSkill.IncreaseAgility;
            res.Result = AttackResult.Invisible;
            source.ApplyAfterCastDelay(0.5f);

            source.Character.Map?.AddVisiblePlayersAsPacketRecipients(source.Character);
            CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.IncreaseAgility, lvl, res);
            CommandBuilder.ClearRecipients();

            if (source.Character.Type == CharacterType.Player)
                source.ApplyCooldownForSupportSkillAction();
            else
                source.ApplyCooldownForAttackAction();
        }
    }
}
