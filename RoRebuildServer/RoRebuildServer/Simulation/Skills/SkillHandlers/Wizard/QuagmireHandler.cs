using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Custom;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;
using static RoRebuildServer.EntityComponents.Npcs.NpcBehaviorBase;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[SkillHandler(CharacterSkill.Quagmire, SkillClass.Magic, SkillTarget.Ground)]
public class QuagmireHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Type == CharacterType.Player && source.Character.CountEventsOfType("QuagmireBaseEvent") >= 3)
            return SkillValidationResult.CannotCreateMore;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (source.Character.Map == null)
            return;

        if (target != null)
            position = target.Character.Position;
        if (position == Position.Invalid)
            position = source.Character.Position;

        var ch = source.Character;
        var map = ch.Map;

        var e = World.Instance.CreateEvent(source.Entity, map, "QuagmireBaseEvent", position, lvl, 0, 0, 0, null);
        ch.AttachEvent(e);

        if (!isIndirect)
        {
            source.ApplyAfterCastDelay(1f);
            source.ApplyCooldownForSupportSkillAction();
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.Quagmire, lvl);
        }
    }
}

public class QuagmireBaseEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.ValuesInt[0] = param1; //level
        npc.StartTimer(200);

        if (!npc.Owner.TryGet<WorldObject>(out var owner))
        {
            ServerLogger.LogWarning($"Npc {npc.Character} running Quagmire init but does not have an owner.");
            return;
        }

        var position = npc.SelfPosition;
        var map = npc.Character.Map;

        if (map == null)
            return;

        for (var x = -2; x <= 2; x++)
        {
            for (var y = -2; y <= 2; y++)
            {
                var newPos = new Position(position.X + x, position.Y + y);
                if (npc.Character.Map!.WalkData.IsCellWalkable(newPos))
                    npc.CreateEvent("QuagmireObjectEvent", newPos, param1, owner.Type != CharacterType.Player ? 1 : 0);
            }
        }
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        Debug.Assert(npc.ValuesInt != null && npc.ValuesInt.Length >= 4);

        npc.Character.Events?.ClearInactive();

        if (npc.EventsCount == 0)
        {
            npc.EndEvent();
            return;
        }

        if (newTime > npc.ValuesInt[0] * 5)
        {
            npc.EndAllEvents();
            return;
        }

        if (!npc.Owner.TryGet<CombatEntity>(out var owner)
            || !owner.Character.IsActive
            || owner.Character.Map != npc.Character.Map
            || owner.Character.State == CharacterState.Dead)
            npc.EndAllEvents();
    }

    public override EventOwnerDeathResult OnOwnerDeath(Npc npc, CombatEntity owner)
    {
        if (owner.Character.Type == CharacterType.Monster)
        {
            npc.EndAllEvents();
            return EventOwnerDeathResult.RemoveEvent;
        }

        return EventOwnerDeathResult.NoAction;
    }

    public override void OnTerminateAoE(Npc npc, AreaOfEffect aoe)
    {
        if (aoe.TouchingEntities == null || aoe.TouchingEntities.Count == 0)
            return;

        foreach (var touch in aoe.TouchingEntities)
            if (touch.TryGet<CombatEntity>(out var t))
            {
                if (t.Character.Type != CharacterType.Player)
                    continue;

                if (t.Character.Map != null && t.Character.Map.TryGetAreaOfEffectAtPosition(t.Character.Position, CharacterSkill.Quagmire, out var effect))
                    t.StatusContainer?.ExtendStatusEffectOfType(CharacterStatusEffect.Quagmire, (float)effect.Expiration);
                else
                    t.RemoveStatusOfTypeIfExists(CharacterStatusEffect.Quagmire);
            }
    }
}

public class QuagmireObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        if (!npc.Owner.TryGet<Npc>(out var parent) || !parent.Owner.TryGet<CombatEntity>(out var source))
        {
            ServerLogger.LogWarning($"Failed to init Quagmire object event as it has no owner or source entity!");
            npc.EndEvent();
            return;
        }

        var targeting = new TargetingInfo()
        {
            Faction = source.Character.Type == CharacterType.Player ? 1 : 0,
            Party = 0,
            IsPvp = false,
            SourceEntity = parent.Owner,
            TargetingType = TargetingType.Enemies
        };

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 0), AoeType.SpecialEffect, targeting, 5 * param1, 0.1f, 0, 0);
        aoe.TriggerOnFirstTouch = true;
        aoe.CheckStayTouching = true; //if they die or something in the aoe and lose their status we want to give it back promptly
        aoe.SkillSource = CharacterSkill.Quagmire;

        npc.ValuesInt[0] = param1;
        npc.ValuesInt[1] = param2; //1 if reduction is uncapped
        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
        npc.StartTimer(1000);

        npc.RevealAsEffect(NpcEffectType.Quagmire, "Quagmire");
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!aoe.TargetingInfo.SourceEntity.TryGet<CombatEntity>(out var src))
            return;

        if (src.Character.Map != npc.Character.Map || npc.AreaOfEffect == null)
            return;

        if (target != src && !target.IsValidTarget(src, true, true))
            return;

        if (!target.HasStatusEffectOfType(CharacterStatusEffect.Quagmire))
        {
            var subAgi = npc.ValuesInt[0] * 10;
            var subDex = npc.ValuesInt[0] * 10;

            if (npc.ValuesInt[1] == 0) //1 = uncapped reduction
            {
                var agi = target.GetStat(CharacterStat.Agi);
                var dex = target.GetStat(CharacterStat.Dex);

                if (agi / 2 < subAgi)
                    subAgi = agi / 2;
                if (dex / 2 < subDex)
                    subDex = dex / 2;
            }

            target.AddStatusEffect(StatusEffectState.NewStatusEffect(CharacterStatusEffect.Quagmire, npc.ValuesInt[0] * 5, subAgi, subDex));
        }
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > npc.ValuesInt[0] * 5)
            npc.EndEvent();
    }
}

public class NpcLoaderMagnusExorcismusEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("QuagmireBaseEvent", new QuagmireBaseEvent());
        DataManager.RegisterEvent("QuagmireObjectEvent", new QuagmireObjectEvent());
    }
}
