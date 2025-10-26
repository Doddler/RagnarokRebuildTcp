using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.Sandman, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.Sandman, SkillClass.Physical, SkillTarget.Ground)]
public class SandmanTrapHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(SandmanTrapEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.Sandman;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}


public class SandmanTrapEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.Sandman;
    protected override NpcEffectType EffectType() => NpcEffectType.SandmanTrap;

    protected override float Duration(int skillLevel) => 50f;

    public override void OnNaturalExpiration(Npc npc) => HunterTrapExpiration(npc);
    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => false;
    protected override bool BlockMultipleActivations => true;
    protected override bool InheritOwnerFacing => false;

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity target, int skillLevel)
    {
        return true;
    }
}