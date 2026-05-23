using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.MentalBreaker, SkillClass.Physical)]
public class MentalBreakerHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        lvl = lvl.Clamp(1, 10);

        if (target == null || !target.IsValidTarget(source))
            return;

        var req = new AttackRequest(CharacterSkill.MentalBreaker, 1f, 1, AttackFlags.Physical | AttackFlags.IgnoreNullifyingGroundMagic, AttackElement.Ghost);
        req.AccuracyRatio = 120;
        var res = source.CalculateCombatResult(target, req);

        if(!isIndirect)
            source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.GhostAttack, lvl, res);

        if (!res.IsDamageResult || target.Character.Type != CharacterType.Player)
            return;

        var player = target.Player;
        var curSp = player.GetStat(CharacterStat.Sp);

        player.TakeSpValue(curSp * (10 * lvl) / 100);
    }
}