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

        return 1.5f + lvl * 0.5f;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        using var targetList = EntityListPool.Get();

        if (position.IsValid())
        {
            //gather all players who can see the spell effect and make them packet recipients
            map.GatherPlayersInRange(position, ServerConfig.MaxViewDistance + 2, targetList, false, false);
            CommandBuilder.AddRecipients(targetList);
            targetList.Clear();

            //now gather all players getting hit
            map.GatherEnemiesInArea(source.Character, position, 2, targetList, !isIndirect, true);

            //deal damage to all enemies
            foreach (var e in targetList)
            {
                var res = source.CalculateCombatResult(e.Get<CombatEntity>(), 1, lvl, AttackFlags.Magical,
                    CharacterSkill.ThunderStorm, AttackElement.Wind);
                source.ExecuteCombatResult(res, false);

                if (e.TryGet<WorldObject>(out var blastTarget))
                    CommandBuilder.AttackMulti(source.Character, blastTarget, res, false);
            }

            //only add a cooldown if cast directly and not cast by an event on this entity's behalf
            if (!isIndirect)
                source.ApplyCooldownForAttackAction(position);

            //send the thunder aoe
            var id = DataManager.EffectIdForName["ThunderStorm"];
            CommandBuilder.SendEffectAtLocationMulti(id, position, 0);
            CommandBuilder.ClearRecipients();
        }

        //make the attacker execute the skill, switching to show the effect to those who can see the caster (can be different from aoe recipients)
        if (!isIndirect)
        {
            map?.AddVisiblePlayersAsPacketRecipients(source.Character);
            CommandBuilder.SkillExecuteAreaTargetedSkill(source.Character, position, CharacterSkill.ThunderStorm, lvl);
        }
    }
}