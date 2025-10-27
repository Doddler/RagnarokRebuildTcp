using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.LandMine, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.LandMine, SkillClass.Physical, SkillTarget.Ground)]
public class LandMineHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(LandMineEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.LandMine;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}

public class LandMineEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.LandMine;
    protected override NpcEffectType EffectType() => NpcEffectType.LandMine;

    protected override float Duration(int skillLevel) => 50f; //300f - skillLevel * 50f;

    public override void OnNaturalExpiration(Npc npc) => HunterTrapExpiration(npc);
    protected override bool AllowAutoAttackMove => true;
    protected override bool Attackable => true;
    protected override bool BlockMultipleActivations => false;

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity target, int skillLevel)
    {
        if (target.IsFlying() && ServerConfig.OperationConfig.FliersIgnoreTraps)
            return false;

        var srcLevel = src.GetStat(CharacterStat.Level);
        var statInt = src.GetEffectiveStat(CharacterStat.Int);
        var statDex = src.GetEffectiveStat(CharacterStat.Dex);

        var flags = AttackFlags.IgnoreEvasion | AttackFlags.IgnoreDefense | AttackFlags.IgnoreWeaponRefine | AttackFlags.NoTriggerOnAttackEffects;

        var atk = new AttackRequest(CharacterSkill.LandMine, 1f, 1, flags, AttackElement.Earth);
        atk.MinAtk = skillLevel * (srcLevel + statDex + statInt * 4);
        atk.MaxAtk = atk.MinAtk;

        var res = src.CalculateCombatResultUsingSetAttackPower(target, atk);
        res.IsIndirect = true;
        res.Time = 0;

        CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, target.Character, res);
        
        npc.ActivateAndHide(1f);

        src.ExecuteCombatResult(res, false);

        return true;
    }
}