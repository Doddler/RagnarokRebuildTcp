using Antlr4.Runtime.Atn;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[MonsterSkillHandler(CharacterSkill.FirePillar, SkillClass.Magic, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.FirePillar, SkillClass.Magic, SkillTarget.Ground)]
public class FirePillarHandler : SkillHandlerTrap
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 3.3f - lvl * 0.3f;
    protected override string GroundUnitType() => nameof(FirePillarEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.FirePillar;
    public override int GetSkillRange(CombatEntity source, int lvl) => 9;
    protected override int Catalyst() => 717;

    //unlike regular traps fire pillar can be placed 1 tile overlapping, or directly next to each other for monsters
    protected override Map.AoEOverlapCheckType GetOverlapType(CombatEntity src) =>
        src.Character.Type == CharacterType.Player ? Map.AoEOverlapCheckType.TileInArea : Map.AoEOverlapCheckType.TileOverlapOnly;
}

public class FirePillarEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.FirePillar;
    protected override NpcEffectType EffectType() => NpcEffectType.FirePillar;

    protected override float Duration(int skillLevel) => 50f;

    public override bool OnNaturalExpiration(Npc npc) => true; //don't use HunterTrapExpiration as it drops a trap
    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => false;
    protected override bool BlockMultipleActivations => true;
    protected override bool InheritOwnerFacing => false;
    public override bool CanBeTriggered => true; //it's possible we don't want hunters to be able to trigger them, but maybe?
    public override bool CanBeRemoved => false;


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
                var distance = skillLevel;
                var multiplier = 0.4f + 0.2f * skillLevel;

                var flags = AttackFlags.Magical | AttackFlags.IgnoreDefense | AttackFlags.NoTriggerOnAttackEffects;

                var hitCount = int.Min(skillLevel + 2, 12);
                var atk = new AttackRequest(CharacterSkill.FirePillar, multiplier, hitCount, flags, AttackElement.Fire);
                var lvlBonus = 100 + 50 * skillLevel;
                (atk.MinAtk , atk.MaxAtk) = owner.CalculateAttackPowerRange(true);
                atk.MinAtk = (atk.MinAtk + lvlBonus) / hitCount;
                atk.MaxAtk = (atk.MaxAtk + lvlBonus) / hitCount;
                
                npc.Character.Map?.GatherEnemiesInArea(owner.Character, npc.Character.Position, distance, targetList, false, true);
                foreach (var e in targetList)
                {
                    if (!e.TryGet<CombatEntity>(out var ce) || ce.Character.Map == null)
                        continue;

                    var res = owner.CalculateCombatResultUsingSetAttackPower(ce, atk);
                    res.IsIndirect = true;
                    res.Time = Time.ElapsedTimeFloat;

                    CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, ce.Character, res);
                    owner.ExecuteCombatResult(res, false);
                }

                npc.TimerEnd = 0; //expire immediately
            }
        }

        base.OnTimer(npc, lastTime, newTime);
    }

    private AttackRequest PrepareAttack(Npc npc, CombatEntity owner)
    {
        var skillLevel = SkillLevel(npc);
        var multiplier = 0.4f + 0.2f * skillLevel;

        var flags = AttackFlags.Magical | AttackFlags.IgnoreDefense | AttackFlags.NoTriggerOnAttackEffects;

        var hitCount = int.Min(skillLevel + 2, 12);
        var atk = new AttackRequest(CharacterSkill.FirePillar, multiplier, hitCount, flags, AttackElement.Fire);
        var lvlBonus = 100 + 50 * skillLevel;
        (atk.MinAtk, atk.MaxAtk) = owner.CalculateAttackPowerRange(true);
        atk.MinAtk = (atk.MinAtk + lvlBonus) / hitCount;
        atk.MaxAtk = (atk.MaxAtk + lvlBonus) / hitCount;

        return atk;
    }

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity? target, int skillLevel)
    {
        if (target == null)
            return false;

        if (npc.Character.Map == null)
            return true;

        if (!npc.Owner.TryGet<CombatEntity>(out var owner))
            return false;

        npc.Character.Map.AddVisiblePlayersAsPacketRecipients(npc.Character);
        var id = DataManager.EffectIdForName["FirePillarExplosion"];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.Character.Position, 0);
        CommandBuilder.ClearRecipients();

        //monster fire pillar deals damage immediately and only hits 1 target (no explosion)
        if (src.Character.Type == CharacterType.Monster)
        {
            var atk = PrepareAttack(npc, owner);
            var res = owner.CalculateCombatResultUsingSetAttackPower(target, atk);
            res.IsIndirect = true;
            res.Time = Time.ElapsedTimeFloat;

            CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, target.Character, res);
            owner.ExecuteCombatResult(res, false);

            npc.EndEvent();

            return true;
        }

        ChangeToActivatedState(npc, 1f);

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