using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman
{
    [SkillHandler(CharacterSkill.Bash, SkillClass.Physical)]
    public class BashHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            if (lvl < 0 || lvl > 10)
                lvl = 10;

            if (target == null || !target.IsValidTarget(source))
                return;

            var res = source.CalculateCombatResult(target, 1f + lvl * 0.3f, 1, AttackFlags.Physical | AttackFlags.IgnoreEvasion, CharacterSkill.Bash);

            //test evasion separately as we want to test it with a +20 to hit
            if(!source.TestHitVsEvasion(target, 20))
                res.SetAttackToMiss();

            source.ApplyCooldownForAttackAction(target);
            source.ExecuteCombatResult(res, false);
            
            var ch = source.Character;

            ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.Bash, lvl, res);
            CommandBuilder.ClearRecipients();
        }
    }
}
