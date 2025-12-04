using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Blacksmith;

[SkillHandler(CharacterSkill.WeaponPerfection, SkillClass.Physical, SkillTarget.Self)]
public class WeaponPerfectionHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
    }
}