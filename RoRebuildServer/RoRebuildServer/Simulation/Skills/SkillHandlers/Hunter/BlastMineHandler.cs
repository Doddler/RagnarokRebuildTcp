using Antlr4.Runtime.Atn;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

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

    protected override float Duration(int skillLevel) => 15f; //the trap will detonate early at 8s
    protected override bool Attackable => true;
    protected override bool AllowAutoAttackMove => true;
    protected override bool BlockMultipleActivations => true;

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

                var srcLevel = owner.GetStat(CharacterStat.Level);
                var statInt = owner.GetEffectiveStat(CharacterStat.Int);
                var statDex = owner.GetEffectiveStat(CharacterStat.Dex);
                var skillLevel = SkillLevel(npc);

                var flags = AttackFlags.IgnoreEvasion | AttackFlags.IgnoreDefense | AttackFlags.IgnoreWeaponRefine | AttackFlags.NoTriggerOnAttackEffects;

                var atk = new AttackRequest(CharacterSkill.LandMine, 1f, 1, flags, AttackElement.Wind);
                atk.MinAtk = skillLevel * (int)((50 + statDex / 2f) * (1 + statInt / 30f));
                atk.MaxAtk = atk.MinAtk;

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
        var id = DataManager.EffectIdForName["BlastMineExplosion"];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.Character.Position, 0);
        CommandBuilder.ClearRecipients();

        ChangeToActivatedState(npc, 1f);

        return true;
    }
}