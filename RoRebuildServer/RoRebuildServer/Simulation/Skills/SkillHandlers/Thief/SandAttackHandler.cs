using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief;

[SkillHandler(CharacterSkill.SandAttack, SkillClass.Physical)]
public class SandAttackHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 2;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
            return;

        var res = source.CalculateCombatResult(target, 1.3f, 1, AttackFlags.Physical, CharacterSkill.SandAttack, AttackElement.Earth);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.SandAttack, lvl, res);

        if (!res.IsDamageResult)
            return;

        var chance = 200;
        if (source.Character.Type == CharacterType.Monster)
            chance = 150;

        source.TryBlindTarget(target, chance, res.AttackMotionTime);
    }
}