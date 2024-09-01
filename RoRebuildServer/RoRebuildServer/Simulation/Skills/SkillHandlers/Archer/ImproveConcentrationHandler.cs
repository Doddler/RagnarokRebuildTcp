using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Archer
{
    [SkillHandler(CharacterSkill.ImproveConcentration, SkillClass.None, SkillTarget.Self)]
    public class ImproveConcentrationHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            var ch = source.Character;

            source.ApplyCooldownForSupportSkillAction();

            var dex = (int)(source.GetStat(CharacterStat.Dex) * (0.02f + 0.01f * lvl) + 1);
            var agi = (int)(source.GetStat(CharacterStat.Agi) * (0.02f + 0.01f * lvl) + 1);
            
            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.ImproveConcentration, 40f + 20 * lvl, agi, dex);
            source.StatusContainer.AddNewStatusEffect(status);

            ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SkillExecuteSelfTargetedSkill(ch, CharacterSkill.ImproveConcentration, lvl);
            CommandBuilder.ClearRecipients();
        }
    }
}
