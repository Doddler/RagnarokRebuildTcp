using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Other
{
    [SkillHandler(CharacterSkill.FirstAid, SkillClass.None, SkillTarget.Self)]
    public class FirstAid : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl,
            bool isIndirect, bool isItemSource)
        {
            var maxHp = source.GetEffectiveStat(CharacterStat.MaxHp);
            var healValue = maxHp / 50;
            if (healValue < 5)
                healValue = 5;

            source.ApplyCooldownForSupportSkillAction(0.5f); //you can chain this skill at this speed even if your motion time is higher
            source.HealHp(healValue);

            var ch = source.Character;
            ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SkillExecuteSelfTargetedSkill(ch, CharacterSkill.FirstAid, lvl, isIndirect);
            CommandBuilder.SendHealMulti(source.Character, healValue, HealType.None);
            CommandBuilder.ClearRecipients();
        }
    }
}