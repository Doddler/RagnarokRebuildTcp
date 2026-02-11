using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.ClaymoreTrap, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.ClaymoreTrap, SkillClass.Physical, SkillTarget.Ground)]
public class ClaymoreTrapHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(ClaymoreTrapEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.ClaymoreTrap;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
    protected override int CatalystCount() => 2;
}

public class ClaymoreTrapEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.ClaymoreTrap;
    protected override NpcEffectType EffectType() => NpcEffectType.ClaymoreTrap;

    protected override float Duration(int skillLevel) => 50f;

    public override void OnNaturalExpiration(Npc npc) => HunterTrapExpiration(npc);
    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => true;
    protected override bool BlockMultipleActivations => true;
    protected override bool InheritOwnerFacing => false;

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity? target, int skillLevel)
    {
        return true;
    }
}