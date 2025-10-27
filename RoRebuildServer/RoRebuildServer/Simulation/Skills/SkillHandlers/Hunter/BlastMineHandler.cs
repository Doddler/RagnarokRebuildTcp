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

    protected override float Duration(int skillLevel) => 50f; //300f - skillLevel * 50f;
    protected override bool Attackable => true;
    protected override bool AllowAutoAttackMove => true;
    protected override bool BlockMultipleActivations => true;
    
    public override void OnNaturalExpiration(Npc npc) => HunterTrapExpiration(npc);

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity target, int skillLevel)
    {
        if (target.IsFlying() && ServerConfig.OperationConfig.FliersIgnoreTraps)
            return false;

        var srcLevel = src.GetStat(CharacterStat.Level);
        var statInt = src.GetEffectiveStat(CharacterStat.Int);
        var statDex = src.GetEffectiveStat(CharacterStat.Dex);

        var flags = AttackFlags.IgnoreEvasion | AttackFlags.IgnoreDefense | AttackFlags.IgnoreWeaponRefine | AttackFlags.NoTriggerOnAttackEffects;

        var atk = new AttackRequest(CharacterSkill.LandMine, 1f, 1, flags, AttackElement.Wind);
        atk.MinAtk = skillLevel * (srcLevel + statDex + statInt * 4);
        atk.MaxAtk = atk.MinAtk;

        using var targetList = EntityListPool.Get();
        src.Character.Map?.GatherEnemiesInArea(npc.Character, npc.Character.Position, 1, targetList, true, true);

        foreach (var e in targetList)
        {
            if (!e.TryGet<CombatEntity>(out var blastTarget))
                continue;

            var res = src.CalculateCombatResultUsingSetAttackPower(blastTarget, atk);
            res.IsIndirect = true;
            res.Time = 0;

            CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, blastTarget.Character, res);
            src.ExecuteCombatResult(res, false);
        }

        npc.ActivateAndHide(1f);

        return true;
    }
}