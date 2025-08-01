using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Poison, SkillClass.Physical)]
public class SkillPoison : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        var res = source.CalculateCombatResult(target, 1.5f, 1, AttackFlags.Physical, CharacterSkill.Poison, AttackElement.Poison);
        var poisonBarrier = res.IsDamageResult && target.RemoveStatusOfTypeIfExists(CharacterStatusEffect.Detoxify);
        if (poisonBarrier)
            res.Damage /= 2;
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Poison, lvl, res);

        if (!res.IsDamageResult || poisonBarrier)
            return;

        var chance = lvl * 100; //10% at lvl 1, 100% at lvl 10
        var duration = 12.5f + lvl * 2; //highest level used by a monster is 5
        source.TryPoisonOnTarget(target, chance, true, res.Damage / 2, duration, res.AttackMotionTime);
    }
}