using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.FreezingTrap, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.FreezingTrap, SkillClass.Physical, SkillTarget.Ground)]
public class FreezingTrapHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(FreezingTrapEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.FreezingTrap;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}

public class FreezingTrapEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.FreezingTrap;
    protected override NpcEffectType EffectType() => NpcEffectType.FreezingTrap;

    protected override float Duration(int skillLevel) => 50f;

    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => true;
    protected override bool BlockMultipleActivations => true;
    protected override bool InheritOwnerFacing => false;

    public override bool OnNaturalExpiration(Npc npc) => ActivateTrapWithoutTouchEvent(npc);

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (npc.Character.State == CharacterState.Activated)
        {
            if (lastTime < 0.15f && newTime >= 0.15f)
            {
                using var targetList = EntityListPool.Get();

                if (!npc.Owner.TryGet<CombatEntity>(out var owner))
                    return;

                var skillLevel = SkillLevel(npc);

                var flags = AttackFlags.IgnoreEvasion | AttackFlags.IgnoreDefense | AttackFlags.IgnoreWeaponRefine | AttackFlags.NoTriggerOnAttackEffects | AttackFlags.NoDamageModifiers;

                var atk = new AttackRequest(CharacterSkill.FreezingTrap, 1f, 1, flags, AttackElement.Water);

                npc.Character.Map?.GatherEnemiesInArea(owner.Character, npc.Character.Position, 2, targetList, false, true);
                foreach (var e in targetList)
                {
                    if (!e.TryGet<CombatEntity>(out var ce) || ce.Character.Map == null)
                        continue;

                    var res = owner.CalculateCombatResultUsingSetAttackPower(ce, atk);
                    res.IsIndirect = true;
                    res.Time = 0;

                    CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, ce.Character, res);
                    owner.ExecuteCombatResult(res, false);

                    if (res.IsDamageResult)
                    {
                        if(owner.TryFreezeTarget(ce, 2000, baseDuration: skillLevel * 3f))
                            ce.Character.StopMovingImmediately();
                    }
                }

                npc.TimerEnd = 0; //expire immediately
            }
        }

        base.OnTimer(npc, lastTime, newTime);
    }

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity? target, int skillLevel)
    {
        if (target != null && target.IsFlying() && ServerConfig.OperationConfig.FliersIgnoreTraps)
            return false;

        if (npc.Character.Map == null)
            return true;

        npc.Character.Map.AddVisiblePlayersAsPacketRecipients(npc.Character);
        var id = DataManager.EffectIdForName["FreezeTrapExplosion"];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.Character.Position, 0);
        CommandBuilder.ClearRecipients();

        ChangeToActivatedState(npc, 1f);

        return true;
    }
}