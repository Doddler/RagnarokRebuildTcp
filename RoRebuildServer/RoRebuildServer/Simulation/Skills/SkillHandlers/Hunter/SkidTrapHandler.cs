using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

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
    protected override float Duration(int skillLevel) => 50f + 10 * skillLevel; //300f - skillLevel * 50f;
    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => true;
    protected override bool BlockMultipleActivations => false;
    protected override bool InheritOwnerFacing => true;

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (npc.Character.State == CharacterState.Activated)
        {
            if (lastTime < 0.1f && newTime >= 0.1f)
            {
                using var targetList = EntityListPool.Get();

                if (!npc.Owner.TryGet<WorldObject>(out var owner))
                    owner = npc.Character;

                npc.Character.Map?.GatherEnemiesInArea(owner, npc.Character.Position, 1, targetList, true, true);
                foreach (var e in targetList)
                {
                    if (!e.TryGet<CombatEntity>(out var ce) || ce.Character.Map == null)
                        continue;

                    if (ce.GetSpecialType() == CharacterSpecialType.Boss)
                        continue;

                    var ch = ce.Character;
                    
                    var oldPos = ch.Position;
                    var pos = ch.Map.WalkData.CalcKnockbackFromPosition(ch.Position, ch.Position.AddDirectionToPosition(npc.Character.FacingDirection.Flip()), 7);
                    if (ch.Position != pos)
                    {
                        ch.Map.ChangeEntityPosition3(ch, ch.WorldPosition, pos, false);

                        ch.StopMovingImmediately();
                        ch.Map.TriggerAreaOfEffectForCharacter(ch, oldPos, ch.Position);
                    }
                }
            }
        }

        base.OnTimer(npc, lastTime, newTime);
    }

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity? target, int skillLevel)
    {
        if (target == null)
        {
            ChangeToActivatedState(npc, 2f);
            return true; //triggered by some other means, probably spring trap
        }

        if (target.IsFlying() && ServerConfig.OperationConfig.FliersIgnoreTraps || npc.Character.Map == null)
            return false;

        //var ch = target.Character;

        //if (ch.Map == null || ch.Map != npc.Character.Map)
        //    return false;

        //var pos = ch.Map.WalkData.CalcKnockbackFromPosition(ch.Position, ch.Position.AddDirectionToPosition(src.Character.FacingDirection), 5);
        //if (ch.Position != pos)
        //    ch.Map.ChangeEntityPosition3(ch, ch.WorldPosition, pos, false);

        //ch.StopMovingImmediately();

        npc.Character.Map.AddVisiblePlayersAsPacketRecipients(npc.Character);
        var id = DataManager.EffectIdForName["SkidTrapActivation"];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.Character.Position, 0);
        CommandBuilder.ClearRecipients();

        ChangeToActivatedState(npc, 2f);

        return true;
    }
}