using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[SkillHandler(CharacterSkill.RemoveTrap, SkillClass.Physical, SkillTarget.Ground)]
public class RemoveTrapHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        return PreProcessValidation(source, target, position, lvl, isIndirect, isItemSource) ? SkillValidationResult.Success : SkillValidationResult.InvalidTarget;
    }

    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Map == null)
            return false;

        target ??= source.Character.Map?.FindTrapInArea(Area.CreateAroundPoint(position, 1));
        if (target == null || target.Character.Type != CharacterType.BattleNpc || target.Character.Npc.Behavior is not TrapBaseEvent trapObj)
            return false;

        return trapObj.CanBeRemoved;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        target ??= source.Character.Map?.FindTrapInArea(Area.CreateAroundPoint(position, 1));
        if (target == null || target.Character.Type != CharacterType.BattleNpc || target.Character.Npc.Behavior is not TrapBaseEvent trap || !trap.CanBeRemoved)
            return;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.RemoveTrap, lvl, DamageInfo.EmptyResult(source.Entity, target.Entity));
        
        if (source.Character.Type == CharacterType.Player 
            && trap.ReturnItemOnRemoval > 0 
            && target.Character.State != CharacterState.Activated
            && target.Character.Npc.Owner.TryGet<WorldObject>(out var trapOwner) 
            && trapOwner.Type == CharacterType.Player)
            source.Player.CreateItemInInventory(new ItemReference(trap.ReturnItemOnRemoval, 1));

        target.Character.Npc.EndEvent();
    }
}