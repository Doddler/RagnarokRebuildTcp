using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Knight;

[SkillHandler(CharacterSkill.Pierce, SkillClass.Physical)]
public class PierceHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        if (source.Character.Type == CharacterType.Player && (source.Player.WeaponClass < 4 || source.Player.WeaponClass > 5))
            return SkillValidationResult.IncorrectWeapon; //spear only

        return base.ValidateTarget(source, target, position, lvl, isIndirect, isItemSource);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        lvl = lvl.Clamp(1, 10);

        if (target == null || !target.IsValidTarget(source))
            return;

        var sizeMod = 2;
        if (target.Character.Type == CharacterType.Monster)
            sizeMod = (int)target.Character.Monster.MonsterBase.Size + 1;
        var req = new AttackRequest(CharacterSkill.Pierce, 1f + lvl * 0.1f, sizeMod, AttackFlags.Physical, AttackElement.None);
        req.AccuracyRatio = 100 + lvl * 5;
        var res = source.CalculateCombatResult(target, req);

        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Pierce, lvl, res);
    }
}