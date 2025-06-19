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

    private const int GemstoneId = 716;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        if (target == null)
            return SkillValidationResult.Failure;

        if (target.GetSpecialType() == CharacterSpecialType.Boss)
            return SkillValidationResult.CannotTargetBossMonster;

        if (source.Character.Type == CharacterType.Player && (source.Player.Inventory == null || !source.Player.Inventory.HasItem(GemstoneId))) //blue gemstone
            return SkillValidationResult.MissingRequiredItem;

        return base.ValidateTarget(source, target, position, lvl);
    }

    //failing pre-validation prevents sp from being taken
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (source.Character.Type == CharacterType.Player && (source.Player.Inventory == null || !source.Player.Inventory.HasItem(GemstoneId)))
        {
            CommandBuilder.SkillFailed(source.Player, SkillValidationResult.MissingRequiredItem);
            return false;
        }

        return true;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        //if (lvl < 0 || lvl > 10)
        //    lvl = 10;

        if (target == null || !target.IsValidTarget(source) || source.Player.Inventory == null)
            return;

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.StoneCurse);

        if (source.TryPetrifyTarget(target, 350 + lvl * 30, 4f))
        {
            var success = true;
            var keepGemstone = 300 - lvl * 50;
            if (!source.CheckLuckModifiedRandomChanceVsTarget(source, keepGemstone, 1000))
            {
                if (!source.Player.TryRemoveItemFromInventory(GemstoneId, 1, true))
                {
                    CommandBuilder.SkillFailed(source.Player, SkillValidationResult.MissingRequiredItem);
                    success = false;
                }
            }

            if (success)
            {
                res.Result = AttackResult.Success;
                source.ApplyCooldownForSupportSkillAction();
            }
        }

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.StoneCurse, lvl, res, isIndirect);
    }
}