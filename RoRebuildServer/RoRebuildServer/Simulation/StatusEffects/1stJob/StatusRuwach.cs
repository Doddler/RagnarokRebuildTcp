using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.Ruwach, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave)]
public class StatusRuwach : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnMove;

    public override StatusUpdateResult OnMove(CombatEntity ch, ref StatusEffectState state, Position src, Position dest, bool isTeleport)
    {
        var e = World.Instance.GetEntityById(state.Value1);
        if (!e.IsAlive())
            return StatusUpdateResult.EndStatus;

        if (!e.TryGet<Npc>(out var npc))
            return StatusUpdateResult.EndStatus;

        if (npc.Character.Map != ch.Character.Map || !src.IsValid())
        {
            npc.EndEvent();
            return StatusUpdateResult.EndStatus;
        }

        if (!npc.TryGetAreaOfEffect(out var aoe))
            return StatusUpdateResult.EndStatus;

        ch.Character.Map?.MoveAreaOfEffect(aoe, Area.CreateAroundPoint(ch.Character.Position, 2));
        return StatusUpdateResult.Continue;
    }

    public override StatusUpdateResult OnChangeMaps(CombatEntity ch, ref StatusEffectState state)
    {
        var e = World.Instance.GetEntityById(state.Value1);
        if (e.TryGet<Npc>(out var npc))
            npc.EndEvent();

        return StatusUpdateResult.EndStatus;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        var e = World.Instance.CreateEvent(ch.Entity, ch.Character.Map!, "RuwachObjectEvent", ch.Character.Position, 0, 0, 0, 0, null);
        ch.Character.AttachEvent(e);
        var npc = e.Get<Npc>();
        state.Value1 = npc.Character.Id;
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        var e = World.Instance.GetEntityById(state.Value1);
        if (!e.IsAlive())
            return;
        if (!e.TryGet<Npc>(out var npc))
            return;
        npc.EndEvent();
    }
}

public class RuwachObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        //npc.RevealAsEffect(NpcEffectType.Sight, "Sight");

        if (!npc.Owner.TryGet<WorldObject>(out var owner))
        {
            ServerLogger.LogWarning($"Npc {npc.Character} running RuwachObjectEvent init but does not have an owner.");
            return;
        }

        var targeting = new TargetingInfo()
        {
            Faction = owner.Type == CharacterType.Player ? 1 : 0,
            Party = 0,
            IsPvp = false,
            SourceEntity = npc.Owner,
            TargetingType = TargetingType.Everyone
        };

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 2), AoeType.SpecialEffect, targeting, 10f, 0.25f, 0, 0);
        aoe.TriggerOnFirstTouch = true;
        aoe.CheckStayTouching = true; //if they die or something in the aoe and lose their status we want to give it back promptly
        aoe.SkillSource = CharacterSkill.Ruwach;

        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
    }
    
    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!npc.Owner.TryGet<CombatEntity>(out var owner) || owner.Character.State == CharacterState.Dead)
        {
            npc.EndEvent();
            return;
        }

        if (!target.HasBodyState(BodyStateFlags.AnyHiddenState))
            return;

        target.RemoveStatusOfGroupIfExists("Hiding");

        if (!target.IsValidTarget(owner, false, true))
            return;

        var attack = new AttackRequest(CharacterSkill.Ruwach, 1.5f, 1, AttackFlags.Magical | AttackFlags.CanAttackHidden, AttackElement.Holy);
        var res = owner.CalculateCombatResult(target, attack);
        res.Time = 0f;
        res.AttackMotionTime = 0f;
        res.IsIndirect = true;
        owner.ExecuteCombatResult(res, true, false);
    }
}

public class NpcLoaderRuwachEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("RuwachObjectEvent", new RuwachObjectEvent());
    }
}
