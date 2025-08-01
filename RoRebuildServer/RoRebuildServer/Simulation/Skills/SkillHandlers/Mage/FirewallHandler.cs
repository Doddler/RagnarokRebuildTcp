using System.Diagnostics;
using JetBrains.Annotations;
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
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[MonsterSkillHandler(CharacterSkill.FireWall, SkillClass.Magic, SkillTarget.Enemy)] //firewall from monsters is targeted
[SkillHandler(CharacterSkill.FireWall, SkillClass.Magic, SkillTarget.Ground)]
public class FirewallHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Type == CharacterType.Player && source.Character.CountEventsOfType("FirewallBaseEvent") >= 3)
            return SkillValidationResult.CannotCreateMore;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        if (lvl < 0 || lvl > 10)
            lvl = 10;

        return 2.15f - lvl * 0.15f;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        Debug.Assert(source.Character.Map != null);

        if (target != null)
        {
            position = target.Character.Position;
            if (target.Character.IsMoving)
            {
                //monsters will lead the player 1 tile with firewall if they're moving in order to block them
                var forwardPosition = position.AddDirectionToPosition(target.Character.FacingDirection);
                if (source.Character.Map.WalkData.IsCellWalkable(forwardPosition))
                    position = forwardPosition;
            }
        }

        if(!isIndirect)
            source.ApplyCooldownForSupportSkillAction();

        var ch = source.Character;
        var map = ch.Map;

        var e = World.Instance.CreateEvent(source.Entity, map, "FirewallBaseEvent", position, lvl, 0, 0, 0, null);
        ch.AttachEvent(e);

        if (!isIndirect)
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.FireWall, lvl);
    }
}

//So the player casts firewall and it creates a FirewallBase event at the target cell.
//That FirewallBase creates 3 or 5 FirewallObjects that reveal themselves to the player.
//Those FirewallObjects all register themselves as aoes on the map.
//Touching (or staying) in an aoe will create an indirect attack event from the source player.
//The FirewallBase ends when all FirewallObjects expire or the max skill duration is reached.
//The player can have a max of 3 FirewallBase events at once.

public class FirewallBaseEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.ValuesInt[0] = param1; //level
        npc.ValuesInt[1] = 5 + param1; //duration
        npc.StartTimer(200);

        if (!npc.Owner.TryGet<WorldObject>(out var owner))
        {
            ServerLogger.LogWarning($"Npc {npc.Character} running FirewallBaseEvent init but does not have an owner.");
            return;
        }

        var pos = npc.Character.Position;
        var angle = owner.Position.Angle(npc.SelfPosition);
        var facing = Directions.GetFacingForAngle(angle);

        void Wall(int x, int y) { CreateWallPiece(npc, pos + new Position(x, y), 2 + param1); }

        Wall(0, 0);

        switch (facing)
        {
            case Direction.North:
            case Direction.South:
                Wall(1, 0); Wall(-1, 0);
                break;
            case Direction.East:
            case Direction.West:
                Wall(0, 1); Wall(0, -1);
                break;
            case Direction.NorthWest:
            case Direction.SouthEast:
                Wall(1, 0); Wall(1, 1); Wall(0, -1); Wall(-1, -1);
                break;
            case Direction.SouthWest:
            case Direction.NorthEast:
                Wall(-1, 0); Wall(-1, 1); Wall(0, -1); Wall(1, -1);
                break;
        }
    }

    private void CreateWallPiece(Npc npc, Position pos, int hitCount)
    {
        if(npc.Character.Map!.WalkData.IsCellWalkable(pos))
            npc.CreateEvent("FirewallObjectEvent", pos, hitCount); //param is max hit count
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

        if(newTime > npc.ValuesInt[1])
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

public class FirewallObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.ValuesInt[0] = param1; //hitCount

        npc.RevealAsEffect(NpcEffectType.Firewall, "Firewall");

        if (!npc.Owner.TryGet<Npc>(out var parent) || !parent.Owner.TryGet<CombatEntity>(out var source))
        {
            ServerLogger.LogWarning($"Failed to init Firewall object event as it has no owner or source entity!");
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
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 0), AoeType.DamageAoE, targeting, 15f, 0.05f, 0, 0);
        aoe.CheckStayTouching = true;
        aoe.SkillSource = CharacterSkill.FireWall;
        
        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!aoe.TargetingInfo.SourceEntity.TryGet<CombatEntity>(out var src))
            return;

        if (src.Character.Map != npc.Character.Map)
            return;

        if (!target.IsValidTarget(src) || target.IsInSkillDamageCooldown(CharacterSkill.FireWall))
            return;

        void DoAttack(float delay = 0f)
        {
            var res = src.CalculateCombatResult(target, 0.5f, 1, AttackFlags.Magical, CharacterSkill.FireWall, AttackElement.Fire);
            res.KnockBack = 2;
            res.AttackPosition = target.Character.Position.AddDirectionToPosition(target.Character.FacingDirection);
            res.AttackMotionTime = delay;
            res.Time = Time.ElapsedTimeFloat + delay;
            res.IsIndirect = true;

            if (target.IsElementBaseType(CharacterElement.Undead1))
            {
                res.KnockBack = 0;
                res.Flags = DamageApplicationFlags.NoHitLock | DamageApplicationFlags.UpdatePosition;
            }

            CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, target.Character, res);

            npc.ValuesInt[0]--;
            if (npc.ValuesInt[0] <= 0)
                npc.EndEvent();

            target.SetSkillDamageCooldown(CharacterSkill.FireWall, 0.01f); //make it so they can't get hit by firewall again this frame
            src.ExecuteCombatResult(res, false);
        }

        DoAttack();
        if(target.GetSpecialType() == CharacterSpecialType.Boss && npc.ValuesInt[0] > 0)
            DoAttack(0.025f); //eat tiles faster on boss monsters 2 per update tick, artificially delaying the damage by half the update rate (it becomes 40 hits per second)
    }
}

public class NpcLoaderFirewallEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("FirewallBaseEvent", new FirewallBaseEvent());
        DataManager.RegisterEvent("FirewallObjectEvent", new FirewallObjectEvent());
    }
}
