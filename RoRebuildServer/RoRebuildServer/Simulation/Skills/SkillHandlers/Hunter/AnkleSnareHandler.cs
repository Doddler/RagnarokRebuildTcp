using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Simulation.Items;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.AnkleSnare, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.AnkleSnare, SkillClass.Physical, SkillTarget.Ground)]
public class AnkleSnareHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(AnkleSnareEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.AnkleSnare;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
}

public class AnkleSnareEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.AnkleSnare;
    protected override NpcEffectType EffectType() => NpcEffectType.AnkleSnare;

    protected override float Duration(int skillLevel) => 50f; //300f - skillLevel * 50f;
    
    public override void OnNaturalExpiration(Npc npc)
    {
        if (npc.Owner.TryGet<WorldObject>(out var owner) && owner.Type != CharacterType.Player)
            return;

        var item = new GroundItem(npc.Character.Position, 1065, 1);
        npc.Character.Map?.DropGroundItem(ref item);
    }

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity target, int skillLevel)
    {
        if (target.IsFlying() && ServerConfig.OperationConfig.FliersIgnoreTraps)
            return false;

        var srcLevel = src.GetStat(CharacterStat.Level);
        var targetLevel = target.GetStat(CharacterStat.Level);
        var agi = target.GetStat(CharacterStat.Agi);

        //Duration is 4s * skill level, reduced by 0.5% per point of agi the target has.
        //Monsters higher level than you have a 2% reduction in time per level difference.
        //There is a minimum duration though, which is 0.03s * base level.
        //Bosses don't have their current move stopped and have 1/5 the duration (after the minimum is applied).

        var duration = 4 * skillLevel * (1 - agi / 200f);
        
        var minLen = 0.03f * (srcLevel + 100);
        if (minLen > 6f)
            minLen = 6f;

        if (srcLevel < targetLevel)
            duration *= 1f - (0.02f * (targetLevel - srcLevel));

        duration = float.Max(duration, minLen);
        
        if (target.GetSpecialType() == CharacterSpecialType.Boss)
            duration /= 5f;
        else
        {
            target.Character.StopMovingImmediately();
            target.Character.Map?.ChangeEntityPosition3(target.Character, target.Character.WorldPosition, npc.Character.Position, false);
        }

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.AnkleSnare, duration);
        target.AddStatusEffect(status);

        npc.ActivateAndHide(duration);

        return true;
    }
}

public class NpcLoaderAnkleSnare : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent(nameof(AnkleSnareEvent), new AnkleSnareEvent());
    }
}
