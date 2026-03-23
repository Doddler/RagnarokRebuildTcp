using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[SkillHandler(CharacterSkill.SpringTrap, SkillClass.Physical, SkillTarget.Ground)]
public class SpringTrapHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 4 + lvl;

    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Map == null)
            return false;

        target ??= source.Character.Map?.FindTrapInArea(Area.CreateAroundPoint(position, 1));
        if (target == null || target.Character.Type != CharacterType.BattleNpc || target.Character.Npc.Behavior is not TrapBaseEvent trapObj)
            return false;

        return target.Character.State != CharacterState.Activated && trapObj.CanBeTriggered;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        target ??= source.Character.Map?.FindTrapInArea(Area.CreateAroundPoint(position, 1));
        if (target == null || target.Character.Type != CharacterType.BattleNpc || target.Character.Npc.Behavior is not TrapBaseEvent trap || !trap.CanBeTriggered)
            return;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.SpringTrap, lvl, DamageInfo.EmptyResult(source.Entity, target.Entity));

        trap.ActivateTrapWithoutTouchEvent(target.Character.Npc);
    }
}