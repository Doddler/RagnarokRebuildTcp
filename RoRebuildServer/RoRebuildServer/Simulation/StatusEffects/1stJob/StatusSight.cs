using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob;

[StatusEffectHandler(CharacterStatusEffect.Sight, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave)]
public class StatusSight : StatusEffectBase
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

        ch.Character.Map?.MoveAreaOfEffect(aoe, Area.CreateAroundPoint(ch.Character.Position, 3));
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
        var e = World.Instance.CreateEvent(ch.Entity, ch.Character.Map!, "SightObjectEvent", ch.Character.Position, 0, 0, 0, 0, null);
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

public class SightObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        //npc.RevealAsEffect(NpcEffectType.Sight, "Sight");

        if (!npc.Owner.TryGet<WorldObject>(out var owner))
        {
            ServerLogger.LogWarning($"Npc {npc.Character} running SightObjectEvent init but does not have an owner.");
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
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 3), AoeType.SpecialEffect, targeting, 10f, 0.25f, 0, 0);
        aoe.TriggerOnFirstTouch = true;
        aoe.CheckStayTouching = true; //if they die or something in the aoe and lose their status we want to give it back promptly
        aoe.SkillSource = CharacterSkill.Sight;

        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!npc.IsOwnerAlive)
        {
            npc.EndEvent();
            return;
        }

        target.RemoveStatusOfGroupIfExists("Hiding");
    }
}

public class NpcLoaderSightEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("SightObjectEvent", new SightObjectEvent());
    }
}