using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.StoneCurse, SkillClass.Magic)]
public class StoneCurseHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 4;

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        if (target.GetSpecialType() == CharacterSpecialType.Boss)
            return SkillValidationResult.CannotTargetBossMonster;

        return base.ValidateTarget(source, target, position, lvl);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        //if (lvl < 0 || lvl > 10)
        //    lvl = 10;

        if (target == null || !target.IsValidTarget(source))
            return;

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.StoneCurse);

        if (source.TryPetrifyTarget(target, 350 + lvl * 30, 4f))
        {
            res.Result = AttackResult.Success;
            source.ApplyCooldownForSupportSkillAction();
        }

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.StoneCurse, lvl, res);
    }
}