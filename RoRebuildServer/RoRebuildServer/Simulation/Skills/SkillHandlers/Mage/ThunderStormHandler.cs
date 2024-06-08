using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.Util;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Data;
using RoRebuildServer.Networking;
using System;
using System.Diagnostics;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.ThunderStorm, SkillClass.Magic, SkillTarget.Ground)]
public class ThunderStormHandler : SkillHandlerBase
{
    public override int GetAreaOfEffect(CombatEntity source, Position position, int lvl) => 2; //range 2 = 5x5

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        if (lvl < 0 || lvl > 10)
            lvl = 10;

        return 1f + lvl * 0.4f;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);
        
        var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(source.Character, position, 2, targetList, !isIndirect, true);
        map?.GatherPlayersForMultiCast(source.Character);

        foreach (var e in targetList)
        {
            var res = source.CalculateCombatResult(e.Get<CombatEntity>(), 1, lvl, AttackFlags.Magical, AttackElement.Wind);
            source.ExecuteCombatResult(res, false);
            
            if(e.TryGet<WorldObject>(out var blastTarget))
                CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
        }
        
        EntityListPool.Return(targetList);

        if (!isIndirect)
            source.ApplyCooldownForAttackAction(position);
        var id = DataManager.EffectIdForName["ThunderStorm"];

        if(!isIndirect)
            CommandBuilder.SkillExecuteAreaTargetedSkill(source.Character, position, CharacterSkill.ThunderStorm, lvl);
        CommandBuilder.SendEffectAtLocationMulti(id, position, 0);
        CommandBuilder.ClearRecipients();
    }
}