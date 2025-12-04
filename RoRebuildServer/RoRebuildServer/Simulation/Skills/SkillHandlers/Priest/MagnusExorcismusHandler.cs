using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using System.Diagnostics;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.MagnusExorcismus, SkillClass.Magic, SkillTarget.Ground)]
public class MagnusExorcismusHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 12f;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (!isIndirect && !CheckRequiredGemstone(source, BlueGemstone, false))
            return SkillValidationResult.MissingRequiredItem;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (source.Character.Map == null)
            return;

        if (!isIndirect && !ConsumeGemstoneForSkillWithFailMessage(source, BlueGemstone))
            return;

        if (target != null)
            position = target.Character.Position; //monsters and indirect casts will target self, so use that position
        if (position == Position.Invalid)
            position = source.Character.Position; //or self if there's no target (should always have a target though...)

        var ch = source.Character;
        var map = ch.Map;

        var e = World.Instance.CreateEvent(source.Entity, map, "MagnusExorcismusBaseEvent", position, lvl, 0, 0, 0, null);
        ch.AttachEvent(e);

        if (!isIndirect)
        {
            source.ApplyAfterCastDelay(3f);
            source.ApplyCooldownForSupportSkillAction();
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.MagnusExorcismus, lvl);
        }
        else
        {
            var id = DataManager.EffectIdForName["MagnusExorcismus"];
            CommandBuilder.SendEffectAtLocationMulti(id, position, 0);
        }
    }
}

public class MagnusExorcismusBaseEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.ValuesInt[0] = param1; //level
        npc.ValuesInt[1] = 3 + param1; //duration
        var tileMaxActivations = param1 switch
        {
            <= 3 => 2,
            <= 6 => 3,
            <= 9 => 4,
            _ => 5
        };

        npc.StartTimer(200);

        if (!npc.Owner.TryGet<WorldObject>(out var owner))
        {
            ServerLogger.LogWarning($"Npc {npc.Character} running MagnusExorcismusBaseEvent init but does not have an owner.");
            return;
        }

        var position = npc.SelfPosition;
        var map = npc.Character.Map;

        Span<Position> posList = stackalloc Position[7 * 7];
        var posCount = 0;

        for (var x = -3; x <= 3; x++)
        {
            for (var y = -3; y <= 3; y++)
            {
                if (int.Abs(x) >= 2 && int.Abs(y) >= 2)
                    continue; //cut out the corners to make a cross shape

                var pos = new Position(position.X + x, position.Y + y);

                if (!map.WalkData.IsCellWalkable(pos))
                    continue;

                posList[posCount++] = pos;
            }
        }

        for (var i = 0; i < posCount; i++)
        {
            if (npc.Character.Map!.WalkData.IsCellWalkable(posList[i]))
                npc.CreateEvent("MagnusExorcismusObjectEvent", posList[i], param1, tileMaxActivations);
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

        if (newTime > npc.ValuesInt[1])
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
}

public class MagnusExorcismusObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        if (!npc.Owner.TryGet<Npc>(out var parent) || !parent.Owner.TryGet<CombatEntity>(out var source))
        {
            ServerLogger.LogWarning($"Failed to init MagnusExorcismus object event as it has no owner or source entity!");
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
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 1), AoeType.DamageAoE, targeting, 1 + 3 * param1, 0.1f, 0, 0);
        aoe.TriggerOnFirstTouch = true;
        aoe.CheckStayTouching = true; //if they die or something in the aoe and lose their status we want to give it back promptly
        aoe.SkillSource = CharacterSkill.MagnusExorcismus;

        npc.ValuesInt[0] = param1;
        npc.ValuesInt[1] = param2;
        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
        npc.StartTimer(1000);

        npc.RevealAsEffect(NpcEffectType.MagnusExorcismus, "MagnusExorcismus");
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (target.IsInSkillDamageCooldown(CharacterSkill.MagnusExorcismus))
            return;

        if (!aoe.TargetingInfo.SourceEntity.TryGet<CombatEntity>(out var src))
            return;

        if (npc.ValuesInt[1] == 0 || src.Character.Map != npc.Character.Map || !src.CanPerformIndirectActions())
            return;

        if (target != src && !target.IsValidTarget(src, true, true))
            return;

        var power = npc.ValuesInt[1];

        if (target.IsElementBaseType(CharacterElement.Undead1) || target.GetRace() == CharacterRace.Demon)
        {
            var res = src.CalculateCombatResult(target, 1f, npc.ValuesInt[0], AttackFlags.Magical, CharacterSkill.MagnusExorcismus, AttackElement.Holy);
            res.AttackPosition = target.Character.Position.AddDirectionToPosition(target.Character.FacingDirection);
            res.AttackMotionTime = 0;
            res.Time = Time.ElapsedTimeFloat;
            res.IsIndirect = true;

            CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, target.Character, res);
            src.ExecuteCombatResult(res, false);

            npc.ValuesInt[1]--;
        }

        target.SetSkillDamageCooldown(CharacterSkill.MagnusExorcismus, 3f);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > 13 || npc.ValuesInt[1] <= 0)
            npc.EndEvent();
    }
}

public class NpcLoaderMagnusExorcismusEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("MagnusExorcismusBaseEvent", new MagnusExorcismusBaseEvent());
        DataManager.RegisterEvent("MagnusExorcismusObjectEvent", new MagnusExorcismusObjectEvent());
    }
}