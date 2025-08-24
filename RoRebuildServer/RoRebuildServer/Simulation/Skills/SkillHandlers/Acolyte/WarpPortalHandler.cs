using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

//how warp portal works:
//When a player initially cast warp portal, we set the player's SpecialActionState to WaitingOnPortalDestination which triggers the destination dialog on the client.
//There is no formal skill success sent to the client here, all players will just see the cast bar finishing and that's it.
//When the player selects a destination, the client sends another skill cast, this time with the level equal to the destination chosen and no target position.
//A cast request marked as self targeted while in WaitingOnPortalDestination state is recognized as an activation request, and costs no sp and has no cast time.
//We then do one final check of the target coordinates and validate the portal itself is valid, then open the portal.
//Even if they have WaitingOnPortalDestination state, we can tell if they've dismissed the dialog and cast a second time based on the skill target (ground or self).
//A player's SpecialActionState gets reset if they try to cast a different skill, die, or change maps. This should close the destination dialog on the client.

[SkillHandler(CharacterSkill.WarpPortal, SkillClass.Unique, SkillTarget.Ground)]
public class WarpPortalHandler : SkillHandlerBase
{
    public override int GetAreaOfEffect(CombatEntity source, Position position, int lvl) => 1;
    public override int GetSkillRange(CombatEntity source, int lvl) => 9;

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        if (source.Character.Type == CharacterType.Player &&
            source.Player.SpecialState == SpecialPlayerActionState.WaitingOnPortalDestination)
            return 0;

        return 1f;
    }

    public override bool ShouldSkillCostSp(CombatEntity source)
    {
        if (source.Character.Type == CharacterType.Player &&
            source.Player.SpecialState == SpecialPlayerActionState.WaitingOnPortalDestination)
            return false;

        return base.ShouldSkillCostSp(source);
    }

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Type == CharacterType.Player)
        {
            if (source.Player.SpecialState == SpecialPlayerActionState.WaitingOnPortalDestination)
            {
                if (!position.IsValid())
                    return SkillValidationResult.Success;
                source.Player.SpecialState = SpecialPlayerActionState.None;
            }

            if (!isIndirect && !CheckRequiredGemstone(source, BlueGemstone, false))
                return SkillValidationResult.MissingRequiredItem;

            if (source.Character.Map == null || !position.IsValid() 
                                             || !source.Character.Map.WalkData.IsCellWalkable(position) 
                                             || !position.InRange(source.Character.Position, 12))
                return SkillValidationResult.InvalidTarget;
        }

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }
    
    //failing pre-validation prevents sp from being taken
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource) => !isIndirect && CheckRequiredGemstone(source, BlueGemstone);

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        //monster and npc cast warp portal does nothing on its own
        if (source.Character.Type != CharacterType.Player)
        {
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(source.Character, position, CharacterSkill.WarpPortal, lvl);
            return;
        }
        
        var player = source.Player;
        var ch = source.Character;
        var map = source.Character.Map!;

        if (player.SpecialState == SpecialPlayerActionState.WaitingOnPortalDestination)
        {
            if (map.IsTileOccupied(player.SpecialStateTarget))
            {
                CommandBuilder.SkillFailed(player, SkillValidationResult.TargetAreaOccupied);
                return;
            }

            if (!isIndirect && !ConsumeGemstoneForSkillWithFailMessage(source, BlueGemstone))
                return;
        
            //the player has selected a destination, which is a second cast of warp portal
            var memo = player.MemoLocations[lvl - 1];
            if (string.IsNullOrWhiteSpace(memo.MapName) || !World.Instance.IsValidMap(memo.MapName))
            {
                CommandBuilder.ErrorMessage(player, "Selected warp portal destination is not currently available.");
                return;
            }

            //if the target cell is now blocked, or they've moved too far away, we fail
            if (!map.WalkData.IsCellWalkable(player.SpecialStateTarget) || !player.SpecialStateTarget.InRange(ch.Position, 12))
            {
                CommandBuilder.SkillFailed(player, SkillValidationResult.TooFarAway);
                return;
            }

            var maxLevel = player.MaxLearnedLevelOfSkill(CharacterSkill.WarpPortal);
            var lifeTime = 5 + maxLevel * 5;
            var e = World.Instance.CreateEvent(source.Entity, map, "WarpPortalBaseEvent", player.SpecialStateTarget,
                lifeTime, memo.Position.X, memo.Position.Y, 0, memo.MapName);
            ch.AttachEvent(e);
            
            player.SpecialState = SpecialPlayerActionState.None;
            player.SpecialStateTarget = Position.Invalid;
            //CommandBuilder.ChangePlayerSpecialActionState(player, SpecialPlayerActionState.None);
        }
        else
        {
            //we have cast warp portal on the ground, if the location for the portal is valid we need to ask the player to pick a destination
            if (!map.WalkData.IsCellWalkable(position) || !position.InRange(ch.Position, 12))
            {
                CommandBuilder.SkillFailed(player, SkillValidationResult.InvalidTarget);
                return;
            }

            if (!position.IsValid())
                return; //do nothing

            player.SpecialState = SpecialPlayerActionState.WaitingOnPortalDestination;
            player.SpecialStateTarget = position;
            CommandBuilder.ChangePlayerSpecialActionState(player, SpecialPlayerActionState.WaitingOnPortalDestination);

            if (!isIndirect)
                CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.WarpPortal, lvl);
        }
    }
}

public class WarpPortalBaseEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        if(paramString != null)
            ServerLogger.LogErrorWithStackTrace($"Attempting to create WarpPortalBaseEvent without a string parameter!");

        npc.ValuesInt[0] = param1;
        npc.ValuesInt[1] = param2;
        npc.ValuesInt[2] = param3;
        npc.ValuesInt[3] = param4;
        npc.ValuesString[0] = paramString!;
        npc.RevealAsEffect(NpcEffectType.WarpPortalOpening, "WarpPortal");
        npc.StartTimer(1000);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > npc.ValuesInt[0])
        {
            npc.EndEvent();
            return;
        }

        if (newTime < 3 || lastTime > 3)
            return;

        //start portal
        //npc.HideNpc();
        //npc.RevealAsEffect(NpcEffectType.WarpPortal, "WarpPortal");
        npc.ChangeEffectType(NpcEffectType.WarpPortal);

        if (!npc.Owner.TryGet<WorldObject>(out var ch))
        {
            npc.EndEvent();
            return;
        }

        var targeting = new TargetingInfo()
        {
            SourceEntity = npc.Owner,
            TargetingType = TargetingType.Player
        };
        

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 0), AoeType.SpecialEffect, targeting, npc.ValuesInt[0], 0.05f, 0, 0);
        aoe.CheckStayTouching = true;
        aoe.TriggerOnFirstTouch = false;
        aoe.TriggerOnLeaveArea = false;
        aoe.SkillSource = CharacterSkill.WarpPortal;

        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);

        //npc.StopTimer();
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (target.Character.Type != CharacterType.Player)
            return;
        if (target.Character.IsMoving || target.Character.IsTargetImmune || target.Character.State == CharacterState.Dead)
            return;
        if (string.IsNullOrWhiteSpace(npc.ValuesString[0]))
            return;

        var p = target.Player;
        p.Character.StopMovingImmediately();
        p.ClearTarget();
        target.CancelCast();

        if (!p.WarpPlayer(npc.ValuesString[0], npc.ValuesInt[1], npc.ValuesInt[2], 1, 1, false))
        {
            ServerLogger.LogWarning($"Failed to move player to {npc.ValuesString[0]}!");
            return;
        }
        npc.ValuesInt[3] += 1;

        if (npc.ValuesInt[3] >= 8)
            npc.EndEvent();
    }
}

public class NpcLoaderWarpPortalEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("WarpPortalBaseEvent", new WarpPortalBaseEvent());
    }
}