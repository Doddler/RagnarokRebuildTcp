using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.TalkieBox, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.TalkieBox, SkillClass.Physical, SkillTarget.Ground)]
public class TalkieBoxHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(TalkieBoxEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.TalkieBox;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}

public class TalkieBoxEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.TalkieBox;
    protected override NpcEffectType EffectType() => NpcEffectType.TalkieBox;

    protected override float Duration(int skillLevel) => 50f;

    public override void OnNaturalExpiration(Npc npc) => HunterTrapExpiration(npc);
    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => false;
    protected override bool BlockMultipleActivations => true;
    protected override bool InheritOwnerFacing => false;

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity? target, int skillLevel)
    {
        if (target == null)
        {
            ChangeToActivatedState(npc, 1f);
            return true; //triggered by some other means, probably spring trap
        }

        return true;
    }
}