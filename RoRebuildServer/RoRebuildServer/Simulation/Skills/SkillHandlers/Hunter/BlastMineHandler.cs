using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.BlastMine, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.BlastMine, SkillClass.Physical, SkillTarget.Ground)]
public class BlastMineHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(BlastMineEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.BlastMine;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}

public class BlastMineEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.BlastMine;
    protected override NpcEffectType EffectType() => NpcEffectType.BlastMine;

    protected override float Duration(int skillLevel) => 50f; //300f - skillLevel * 50f;

    public override void OnNaturalExpiration(Npc npc) => HunterTrapExpiration(npc);

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity target, int skillLevel)
    {
        if (target.IsFlying() && ServerConfig.OperationConfig.FliersIgnoreTraps)
            return false;

        npc.ActivateAndHide(1f);

        return true;
    }
}