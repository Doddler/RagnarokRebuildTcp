using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Other
{
    [SkillHandler(CharacterSkill.FirstAid, SkillClass.None, SkillTarget.Self)]
    public class FirstAid : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            var maxHp = source.GetEffectiveStat(CharacterStat.MaxHp);
            var healValue = maxHp / 100;
            if (healValue < 5)
                healValue = 5;

            source.ApplyCooldownForSupportSkillAction(0.5f); //minimum cooldown time is 0.5s
            source.HealHp(healValue);

            var ch = source.Character;
            ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SkillExecuteSelfTargetedSkill(ch, CharacterSkill.FirstAid, lvl);
            CommandBuilder.SendHealMulti(source.Character, healValue, HealType.None);
            CommandBuilder.ClearRecipients();
        }
    }
}
