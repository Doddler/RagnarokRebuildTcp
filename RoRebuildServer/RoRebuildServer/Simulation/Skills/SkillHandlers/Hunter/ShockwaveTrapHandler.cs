using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.ShockwaveTrap, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.ShockwaveTrap, SkillClass.Physical, SkillTarget.Ground)]
public class ShockwaveTrapHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(ShockwaveTrapEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.ShockwaveTrap;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}


public class ShockwaveTrapEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.ShockwaveTrap;
    protected override NpcEffectType EffectType() => NpcEffectType.ShockwaveTrap;

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