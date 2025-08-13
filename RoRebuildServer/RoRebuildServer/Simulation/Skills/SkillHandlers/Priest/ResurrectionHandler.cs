using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.Resurrection, SkillClass.Magic, SkillTarget.Ally)]
public class ResurrectionHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        return lvl switch
        {
            1 => 6f,
            2 => 4f,
            3 => 2f,
            4 => 0f,
            _ => 6f
        };
    }
    
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null || source == target || target.Character.Type != CharacterType.Player || target.Character.State != CharacterState.Dead)
            return SkillValidationResult.Failure;

        if (!UsableWhileHidden && source.HasBodyState(BodyStateFlags.Hidden))
            return SkillValidationResult.Failure;

        if (!isIndirect && !isItemSource && !CheckRequiredGemstone(source, BlueGemstone, false))
            return SkillValidationResult.MissingRequiredItem;

        return SkillValidationResult.Success; //we skip the standard validation rules because it will check if the target is alive
    }

    
    //failing pre-validation prevents sp from being taken
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource) =>
        isIndirect || isItemSource || CheckRequiredGemstone(source, BlueGemstone, true);

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null || target.Character.State != CharacterState.Dead)
            return;

        var ch = source.Character;
        if (ch.Map == null || ch.Map != target.Character.Map)
            return;

        if (!isIndirect && !isItemSource && !ConsumeGemstoneForSkillWithFailMessage(source, BlueGemstone))
            return;

        source.ApplyCooldownForSupportSkillAction();
        
        target.ClearDamageQueue();
        var hpPercent = lvl switch
        {
            1 => 10,
            2 => 30,
            3 => 50,
            4 => 80,
            _ => 10,
        };

        target.Player.Revive(hpPercent);

        var di = DamageInfo.EmptyResult(source.Entity, target.Entity);

        ch.Map.AddVisiblePlayersAsPacketRecipients(ch);
        CommandBuilder.EnsureRecipient(source.Entity);
        CommandBuilder.SendPlayerResurrection(target.Character);
        CommandBuilder.SkillExecuteTargetedSkill(ch, target.Character, CharacterSkill.Resurrection, lvl, di);
        CommandBuilder.ClearRecipients();
    }
}