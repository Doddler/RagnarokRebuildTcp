using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

//special considerations: 
//Baphomet card's splash damage is handled by autocasting this skill 100%. Doing so will cause the skill to execute as indirect.
//As such, an indirect splash attack will exclude the primary target from taking damage.

[SkillHandler(CharacterSkill.SplashAttack, SkillClass.Physical, SkillTarget.Enemy)]
public class SplashAttackHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        var map = source.Character.Map;
        Debug.Assert(map != null);

        map.AddVisiblePlayersAsPacketRecipients(source.Character);

        var range = isIndirect ? 1 : 3;

        using var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, target.Character.Position, range, targetList, true, true);

        var hasTarget = !isIndirect;
        
        var attack = new AttackRequest(CharacterSkill.SplashAttack, 1, 1, AttackFlags.Physical, AttackElement.None);
        if (isIndirect)
            attack.Flags |= AttackFlags.NoDamageModifiers | AttackFlags.NoTriggers;

        foreach (var e in targetList)
        {
            if (!e.TryGet<WorldObject>(out var blastTarget))
                continue;

            var ce = e.Get<CombatEntity>();

            if (isIndirect && ce == target)
                continue; //indirect SplashAttack

            var res = source.CalculateCombatResult(ce, attack);
            res.IsIndirect = true;
            hasTarget = true;

            //if (isIndirect)
            //{
            //    res.AttackMotionTime = 0;
            //    res.Time = Time.ElapsedTimeFloat;
            //}

            source.ExecuteCombatResult(res, false);

            CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
        }

        if (isIndirect)
        {
            if (!hasTarget)
                return;
        }
        else
            source.ApplyCooldownForAttackAction(position);
        
        var skillResult = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.SplashAttack);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.SplashAttack, lvl, skillResult, isIndirect);
    }
}