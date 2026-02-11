using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[MonsterSkillHandler(CharacterSkill.FirePillar, SkillClass.Magic, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.FirePillar, SkillClass.Magic, SkillTarget.Ground)]
public class FirePillarHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(FirePillarEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.FirePillar;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}

public class FirePillarEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.FirePillar;
    protected override NpcEffectType EffectType() => NpcEffectType.FirePillar;

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

public class FirePillarLoader : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent(nameof(FirePillarEvent), new FirePillarEvent());
    }
}