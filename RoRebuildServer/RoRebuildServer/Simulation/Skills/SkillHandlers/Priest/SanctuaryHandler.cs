using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;
using System.Xml.Linq;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

//kinda a complex script, but basically:
//1. Player casts sanctuary, creates SanctuaryBaseEvent, setting duration and a maximum activation count.
//2. SanctuaryBaseEvent creates ~21 SanctuaryObjectEvents.
//3. SanctuaryObjectEvents reveal themselves to the player as individual sanctuary tiles.
//4. When something touches a SanctuaryObjectEvent, it queries it's parent and subtracts 1 hit. Damage hits subtract 2.
//5. When the parent's hit count reaches zero, touches stop healing and the parent will end next timer tick.
//6. If the owner dies or the SanctuaryBaseEvent timer ends, 

[MonsterSkillHandler(CharacterSkill.Sanctuary, SkillClass.Magic, SkillTarget.Self)]
[SkillHandler(CharacterSkill.Sanctuary, SkillClass.Magic, SkillTarget.Ground)]
public class SanctuaryHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 5f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (source.Character.Map == null)
            return;

        if (target != null)
            position = target.Character.Position; //monsters and indirect casts will target self, so use that position
        if (position == Position.Invalid)
            position = source.Character.Position; //or self if there's no target (should always have a target though...)

        var ch = source.Character;
        var map = ch.Map;

        var e = World.Instance.CreateEvent(source.Entity, map, "SanctuaryBaseEvent", position, lvl, 0, 0, 0, null);
        ch.AttachEvent(e);

        if (!isIndirect)
        {
            source.ApplyCooldownForSupportSkillAction();
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.Sanctuary, lvl);
        }
    }
}

public class SanctuaryBaseEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.ValuesInt[0] = param1; //level
        npc.ValuesInt[1] = 1 + 3 * param1; //duration
        npc.ValuesInt[2] = 6 + 2 * param1; //max activations
        npc.StartTimer(200);

        if (!npc.Owner.TryGet<WorldObject>(out var owner))
        {
            ServerLogger.LogWarning($"Npc {npc.Character} running SanctuaryBaseEvent init but does not have an owner.");
            return;
        }

        var power = param1 switch
        {
            1 => 100,
            2 => 200,
            3 => 300,
            4 => 400,
            5 => 500,
            6 => 600,
            _ => 777
        };

        var position = npc.SelfPosition;
        var map = npc.Character.Map;

        Span<Position> posList = stackalloc Position[25];
        var posCount = 0;

        for (var x = -2; x <= 2; x++)
        {
            for (var y = -2; y <= 2; y++)
            {
                if (int.Abs(x) == 2 && int.Abs(y) == 2)
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
                npc.CreateEvent("SanctuaryObjectEvent", posList[i], power, null, false);
        }
    }

    public override int OnQuery(Npc npc, Npc srcNpc, string signal, int value1, int value2, int value3, int value4)
    {
        if (signal != "HIT")
            return 0;
        var previous = npc.ValuesInt[2];
        npc.ValuesInt[2] -= value1;
        return previous;
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        Debug.Assert(npc.ValuesInt != null && npc.ValuesInt.Length >= 4);

        npc.Character.Events?.ClearInactive();

        if (npc.EventsCount == 0 || npc.ValuesInt[2] <= 0)
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

public class SanctuaryObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        if (!npc.Owner.TryGet<Npc>(out var parent) || !parent.Owner.TryGet<CombatEntity>(out var source))
        {
            ServerLogger.LogWarning($"Failed to init Sanctuary object event as it has no owner or source entity!");
            npc.EndEvent();
            return;
        }
        var targeting = new TargetingInfo()
        {
            Faction = source.Character.Type == CharacterType.Player ? 1 : 0,
            Party = 0,
            IsPvp = false,
            SourceEntity = parent.Owner,
            TargetingType = TargetingType.Everyone
        };

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 0), AoeType.SpecialEffect, targeting, 1 + 3 * param1, 0.1f, 0, 0);
        aoe.TriggerOnFirstTouch = true;
        aoe.CheckStayTouching = true; //if they die or something in the aoe and lose their status we want to give it back promptly
        aoe.SkillSource = CharacterSkill.Sanctuary;

        npc.ValuesInt[0] = param1;
        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
        npc.StartTimer(1000);

        npc.RevealAsEffect(NpcEffectType.Sanctuary, "Sanctuary");
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!aoe.TargetingInfo.SourceEntity.TryGet<CombatEntity>(out var src))
            return;

        if (src.Character.Map != npc.Character.Map || !src.CanPerformIndirectActions())
            return;

        if (target != src && !target.IsValidTarget(src, true, true))
            return;

        if (target.IsInSkillDamageCooldown(CharacterSkill.Sanctuary))
            return;

        if (!npc.Owner.TryGet<Npc>(out var owner))
            return;

        var power = npc.ValuesInt[0];

        if (target.IsElementBaseType(CharacterElement.Undead1) || target.GetRace() == CharacterRace.Demon)
        {
            if (owner.Behavior.OnQuery(owner, npc, "HIT", 2, 0, 0, 0) <= 0)
                return;

            var res = src.PrepareTargetedSkillResult(target, CharacterSkill.Heal);
            var mod = DataManager.ElementChart.GetAttackModifier(AttackElement.Holy, target.GetElement());
            res.Damage = power / 2 * mod / 100;
            res.HitCount = 1;
            res.AttackPosition = target.Character.Position.AddDirectionToPosition(target.Character.FacingDirection);
            res.KnockBack = 2;
            res.AttackMotionTime = 0f;
            res.Time = 0f;
            res.Result = AttackResult.NormalDamage;

            if (src.Character.Type == CharacterType.Player && target.Character.Type == CharacterType.Player && src != target)
                res.Damage = 1;

            CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, target.Character, res);
            src.ExecuteCombatResult(res, false);
        }
        else
        {
            if (target.GetStat(CharacterStat.Hp) >= target.GetStat(CharacterStat.MaxHp))
                return;

            if (owner.Behavior.OnQuery(owner, npc, "HIT", 1, 0, 0, 0) <= 0)
                return;

            target.HealHp(power, true, HealType.HealSkill);

        }

        target.SetSkillDamageCooldown(CharacterSkill.Sanctuary, 1f);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > 31)
            npc.EndEvent();
    }
}

public class NpcLoaderSanctuaryEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("SanctuaryBaseEvent", new SanctuaryBaseEvent());
        DataManager.RegisterEvent("SanctuaryObjectEvent", new SanctuaryObjectEvent());
    }
}
