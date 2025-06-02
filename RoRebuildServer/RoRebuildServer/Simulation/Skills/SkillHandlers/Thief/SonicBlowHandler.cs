using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief;

[SkillHandler(CharacterSkill.SonicBlow, SkillClass.Physical, SkillTarget.Enemy)]
public class SonicBlowHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 1;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null || !target.IsValidTarget(source))
            return;

        var ch = source.Character;
        lvl = lvl.Clamp(1, 10);

        var attack = new AttackRequest(CharacterSkill.SonicBlow, 0.5f + 0.05f * lvl, 8, AttackFlags.Physical, AttackElement.None);
        attack.AccuracyRatio = 150;
        
        //var res = source.CalculateCombatResult(target, 0.5f + 0.05f * lvl, 1, AttackFlags.Physical, CharacterSkill.SonicBlow);
        var res = source.CalculateCombatResult(target, attack);
        source.ApplyCooldownForAttackAction(target);

        if (ch.Type == CharacterType.Player)
        {
            ch.AttackCooldown += 1f;
            ch.AddMoveLockTime(2f, true);
        }
        else
            ch.AddMoveLockTime(1f);

        if (ch.Type == CharacterType.Monster)
            ch.Monster.AddDelay(1f);


        //res.HitCount = 8;
        res.AttackMotionTime = 0.4f;
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.SonicBlow, lvl, res);        
    }
}