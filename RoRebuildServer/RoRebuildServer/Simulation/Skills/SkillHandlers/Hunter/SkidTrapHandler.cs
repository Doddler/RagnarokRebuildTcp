using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.SkidTrap, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.SkidTrap, SkillClass.Physical, SkillTarget.Ground)]
public class SkidTrapHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(SkidTrapEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.SkidTrap;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}

public class SkidTrapEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.SkidTrap;
    protected override NpcEffectType EffectType() => NpcEffectType.SkidTrap;

    protected override float Duration(int skillLevel) => 50f; //300f - skillLevel * 50f;

    public override void OnNaturalExpiration(Npc npc) => HunterTrapExpiration(npc);
    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => true;
    protected override bool BlockMultipleActivations => false;
    protected override bool InheritOwnerFacing => true;

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity target, int skillLevel)
    {
        if (target.IsFlying() && ServerConfig.OperationConfig.FliersIgnoreTraps)
            return false;

        var ch = target.Character;

        if (ch.Map == null || ch.Map != npc.Character.Map)
            return false;

        var pos = ch.Map.WalkData.CalcKnockbackFromPosition(ch.Position, ch.Position.AddDirectionToPosition(src.Character.FacingDirection), 5);
        if (ch.Position != pos)
            ch.Map.ChangeEntityPosition3(ch, ch.WorldPosition, pos, false);

        ch.StopMovingImmediately();

        npc.Character.Map.AddVisiblePlayersAsPacketRecipients(ch);
        var id = DataManager.EffectIdForName["SkidTrapActivation"];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.Character.Position, 0);
        CommandBuilder.ClearRecipients();

        return true;
    }
}