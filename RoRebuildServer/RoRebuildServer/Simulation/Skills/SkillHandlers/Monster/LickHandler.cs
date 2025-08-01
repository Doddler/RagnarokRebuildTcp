using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Lick, SkillClass.Physical)]
public class LickHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 2;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        lvl = lvl.Clamp(1, 5);
        if (target == null || !target.IsValidTarget(source))
            return;

        source.TryStunTarget(target, -100 + 200 * lvl);

        if (target.Character.Type == CharacterType.Player)
        {
            var maxSp = target.GetStat(CharacterStat.MaxSp);

            var drainAmnt = 100;
            if(maxSp / 4 < drainAmnt)
                drainAmnt = maxSp / 4;

            target.Player.TakeSpValue(drainAmnt);
        }

        var di = source.PrepareTargetedSkillResult(target, CharacterSkill.Lick);

        source.ApplyCooldownForAttackAction(target);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.DoubleStrafe, lvl, di);
    }
}