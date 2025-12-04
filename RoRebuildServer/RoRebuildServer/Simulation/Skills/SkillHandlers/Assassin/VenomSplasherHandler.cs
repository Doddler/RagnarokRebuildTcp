using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Assassin;

[SkillHandler(CharacterSkill.VenomSplasher, SkillClass.Physical)]
public class VenomSplasherHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (!isIndirect && !CheckRequiredGemstone(source, RedGemstone, false))
            return SkillValidationResult.MissingRequiredItem;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    //failing pre-validation prevents sp from being taken
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource) => isIndirect || CheckRequiredGemstone(source, RedGemstone);


    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null)
            return;

        if (!isIndirect && !ConsumeGemstoneForSkillWithFailMessage(source, RedGemstone))
            return;

        if (!isIndirect)
            source.ApplyAfterCastDelay(1f);

        var req = new AttackRequest(CharacterSkill.VenomSplasher, 1f, 1, AttackFlags.Physical, AttackElement.None);
        var res = source.CalculateCombatResult(target, req);

        if (res.IsDamageResult)
        {
            target.StatusContainer?.RemoveStatusEffectOfType(CharacterStatusEffect.VenomSplasher);

            var (_, atk) = source.CalculateAttackPowerRange(false);
            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.VenomSplasher, 12, source.Character.Id, (int)(atk * (6 + 1.5f * lvl)));
            target.AddStatusEffect(status, true, res.AttackMotionTime + 0.1f);
        }

        if (source.Character.Type == CharacterType.Player)
            source.Player.SetSkillSpecificCooldown(CharacterSkill.VenomSplasher, 3f);
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.VenomSplasher, lvl, res);
    }
}