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
    protected override bool Attackable => false;

    protected override float Duration(int skillLevel) => 50f; //300f - skillLevel * 50f;

    public override void OnEventEnd(Npc npc)
    {
        //ankle snare is unique in that removing the trap removes the status effect from whoever was trapped
        var trappedId = npc.ValuesInt[(int)TrapValue.TargetData];
        if (trappedId <= 0)
            return;

        //only remove the status if the last application is us (value1 on the status effect will be the trap's id if it us)
        var entity = World.Instance.GetEntityById(trappedId);
        if (entity.TryGet<CombatEntity>(out var ce) && ce.TryGetStatusEffect(CharacterStatusEffect.AnkleSnare, out var snare) && snare.Value1 == npc.Character.Id)
            ce.RemoveStatusOfTypeIfExists(CharacterStatusEffect.AnkleSnare);
    }

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity? target, int skillLevel)
    {
        if (npc.Character.Map == null)
            return false;

        if (target == null)
        {
            //we've been sprung using spring trap, there might still be enemies nearby (if they're flying for example) so catch one of them.
            target = npc.Character.Map.GetRandomEnemyInArea(src, npc.Character.Position, 1, true, true);
            if (target == null)
            {
                ChangeToActivatedState(npc, 3f);
                return true;
            }
        }
        else if (target.IsFlying() && ServerConfig.OperationConfig.FliersIgnoreTraps)
            return false; //flyers can't touch the trap to activate it

        var srcLevel = src.GetStat(CharacterStat.Level);
        var targetLevel = target.GetStat(CharacterStat.Level);
        var agi = target.GetEffectiveStat(CharacterStat.Agi);

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

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.AnkleSnare, duration, npc.Character.Id);
        target.AddStatusEffect(status);

        npc.ValuesInt[(int)TrapValue.TargetData] = target.Character.Id;

        ChangeToActivatedState(npc, duration);

        return true;
    }
}