using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers
{
    public class SkillHandlerGenericCast : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl)
        {
            var ch = source.Character;

            ch.Map?.GatherPlayersForMultiCast(ch);
            CommandBuilder.SkillExecuteSelfTargetedSkill(source.Character, CharacterSkill.None, lvl);
            CommandBuilder.ClearRecipients();
        }
    }
}
