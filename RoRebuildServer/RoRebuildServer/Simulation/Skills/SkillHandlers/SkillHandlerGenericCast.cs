using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers
{
    public class SkillHandlerGenericCast : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            var ch = source.Character;
            
            source.ApplyCooldownForAttackAction();

            ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SkillExecuteSelfTargetedSkill(source.Character, CharacterSkill.None, lvl, isIndirect);
            CommandBuilder.ClearRecipients();
        }
    }
}
