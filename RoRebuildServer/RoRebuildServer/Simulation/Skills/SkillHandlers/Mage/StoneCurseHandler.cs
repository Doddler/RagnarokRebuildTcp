using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.StoneCurse, SkillClass.Magic)]
public class StoneCurseHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 4;

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;

    private const int GemstoneId = 716; //red gemstone

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null)
            return SkillValidationResult.Failure;

        if (target.GetSpecialType() == CharacterSpecialType.Boss)
            return SkillValidationResult.CannotTargetBossMonster;

        if (!isIndirect && !isItemSource && !CheckRequiredGemstone(source, GemstoneId, false))
            return SkillValidationResult.MissingRequiredItem;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    //failing pre-validation prevents sp from being taken
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource) =>
        isIndirect || isItemSource || CheckRequiredGemstone(source, GemstoneId, true);

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        //if (lvl < 0 || lvl > 10)
        //    lvl = 10;

        if (target == null || !target.IsValidTarget(source))
            return;

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.StoneCurse);

        if (source.TryPetrifyTarget(target, 350 + lvl * 30, 4f))
        {
            //var success = true;
            var keepGemstone = 200 + lvl * 50;
            if (source.Character.Type == CharacterType.Player && GameRandom.NextInclusive(0, 1000) > keepGemstone)
            {
                if (!isIndirect && !isItemSource)
                    ConsumeGemstone(source, GemstoneId);
            }

            res.Result = AttackResult.Success;
            source.ApplyCooldownForSupportSkillAction();
        }

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.StoneCurse, lvl, res, isIndirect);
    }
}