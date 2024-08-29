using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief;

[SkillHandler(CharacterSkill.SonicBlow, SkillClass.Physical, SkillTarget.Enemy)]
public class SonicBlowHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (lvl < 0 || lvl > 10)
            lvl = 10;

        if (target == null || !target.IsValidTarget(source))
            return;

        var ch = source.Character;

        var res = source.CalculateCombatResult(target, 0.5f + 0.05f * lvl, 1, AttackFlags.Physical, CharacterSkill.SonicBlow);
        source.ApplyCooldownForAttackAction(target);

        if(ch.Type == CharacterType.Player)
            ch.AttackCooldown += 1f;

        if (ch.Type == CharacterType.Monster)
            ch.Monster.AddDelay(1f);

        ch.AddMoveLockTime(1f);
        res.HitCount = 8;
        res.AttackMotionTime = 0.4f;
        source.ExecuteCombatResult(res, false);

        ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.SonicBlow, lvl, res);
        CommandBuilder.ClearRecipients();
    }
}