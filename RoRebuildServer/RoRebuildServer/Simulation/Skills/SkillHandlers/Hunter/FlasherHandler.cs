using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

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

    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => false;
    protected override bool BlockMultipleActivations => true;
    protected override bool InheritOwnerFacing => false;

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

                //var flags = AttackFlags.IgnoreEvasion | AttackFlags.IgnoreDefense | AttackFlags.IgnoreWeaponRefine | AttackFlags.NoTriggerOnAttackEffects | AttackFlags.NoDamageModifiers;

                //var atk = new AttackRequest(CharacterSkill.LandMine, 1f, 1, flags, AttackElement.Water);

                npc.Character.Map?.GatherEnemiesInArea(owner.Character, npc.Character.Position, 2, targetList, false, true);
                foreach (var e in targetList)
                {
                    if (!e.TryGet<CombatEntity>(out var ce) || ce.Character.Map == null)
                        continue;

                    owner.TryBlindTarget(ce, 500 + skillLevel * 100);
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
        var id = DataManager.EffectIdForName["FlasherTrapExplosion"];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.Character.Position, 0);
        CommandBuilder.ClearRecipients();

        ChangeToActivatedState(npc, 1f);

        return true;
    }
}