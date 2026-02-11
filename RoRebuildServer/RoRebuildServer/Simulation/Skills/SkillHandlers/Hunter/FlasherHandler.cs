using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.Flasher, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.Flasher, SkillClass.Physical, SkillTarget.Ground)]
public class FlasherHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(FlasherEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.Flasher;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}

public class FlasherEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.Flasher;
    protected override NpcEffectType EffectType() => NpcEffectType.FlasherTrap;

    protected override float Duration(int skillLevel) => 50f;

    public override void OnNaturalExpiration(Npc npc) => HunterTrapExpiration(npc);
    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => false;
    protected override bool BlockMultipleActivations => true;
    protected override bool InheritOwnerFacing => false;

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity? target, int skillLevel)
    {
        return true;
    }
}