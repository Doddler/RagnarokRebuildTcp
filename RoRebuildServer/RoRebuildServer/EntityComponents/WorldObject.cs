using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Util;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RoRebuildServer.EntityComponents;

public enum QueuedAction
{
    None,
    Cast,
    Move,
    NpcInteract,
    Attack,
    PickUpItem
}

[UsedImplicitly]
[EntityComponent([EntityType.Player, EntityType.Monster, EntityType.Npc, EntityType.Effect])]
public class WorldObject : IEntityAutoReset
{
    public int Id { get; set; }
    public Entity Entity;
    public string Name = null!;
    public bool IsActive;
    public bool AdminHidden { get; set; }
    public bool IsImportant { get; set; }
    public int ClassId;
    public Direction FacingDirection { get; set; }
    public CharacterState State { get; set; }
    public CharacterType Type;
    public CharacterDisplayType DisplayType; //used for minimap icons if they're flagged as map important
    public PlayerLikeAppearanceState? OverrideAppearanceState;
    public Position TargetPosition;
    //public float MoveModifier;
    //public float MoveModifierTime;
    public int StepCount;
    public int ItemTarget;

    public override string ToString() => $"WorldObject {Type}-{Id}({Name})";

    public Position[]? WalkPath;

    public Entity LastAttacked { get; set; }

    private EntityList? visiblePlayers;

    public FloatPosition MoveStartPosition;
    private Position cellPosition;
    public Position Position
    {
        get => cellPosition;
        set
        {
            cellPosition = value;
            WorldPosition = value; //cast to FloatPosition
//#if DEBUG
//            if (Map != null && !Map.GetChunkForPosition(cellPosition).AllEntities.Contains(Entity))
//                throw new Exception("OHNO!");
//#endif
        }
    }

    private FloatPosition worldPosition;
    public FloatPosition WorldPosition
    {
        get => worldPosition;
        set
        {
            cellPosition = value; //cast to regular Position
            worldPosition = value;
        }
    }

    public float SpawnImmunity { get; set; } //you aren't seen as a valid target while this is active
    public double AttackCooldown { get; set; } //delay until you can attack again
    public double MoveLockTime { get; set; } //you cannot move while until this time is reached
    public float MoveSpeed { get; set; } //time it takes to traverse one tile
    public float MoveProgress { get; set; } //the amount of time that remains before you step to the next tile
    public float NextStepDuration { get; set; }
    

    public bool InMoveLock { get; set; }

    public bool UpdateAndCheckMoveLock() => InMoveLock = MoveLockTime > Time.ElapsedTime;

    public bool InAttackCooldown => AttackCooldown > Time.ElapsedTime;

    public int MoveStep { get; set; }
    public int TotalMoveSteps { get; set; }

    public float TimeToReachNextStep => NextStepDuration - MoveProgress;
    public double TimeOfLastMove;
    public float TimeSinceStoppedMoving => (float)(Time.ElapsedTime - TimeOfLastMove);

    public QueuedAction QueuedAction { get; set; }

    //private QueuedAction queuedAction;
    //public QueuedAction QueuedAction
    //{
    //    get => queuedAction;
    //    set
    //    {
    //        if(queuedAction == QueuedAction.Cast && value == QueuedAction.Move)
    //            ServerLogger.Debug("Unexpected!");
    //        queuedAction = value;
    //    }
    //}

    private const float DiagonalSpeedPenalty = 0.7f;
    
    public int StepsRemaining => TotalMoveSteps - (MoveStep + 1);
    public bool IsMoving => State == CharacterState.Moving;
    public bool HasMoveInProgress => WalkPath != null && WalkPath.Length > 0;
    public void SetSpawnImmunity(float time = 5f) => SpawnImmunity = Time.ElapsedTimeFloat + time;
    public void ResetSpawnImmunity() => SpawnImmunity = -1f;
    public bool IsTargetImmune => SpawnImmunity > Time.ElapsedTimeFloat;
    public bool IsAtDestination => !IsMoving || TargetPosition == Position;

    public bool StateCanSit => State == CharacterState.Idle || State == CharacterState.Sitting;
    public bool StateCanAttack => State == CharacterState.Idle || State == CharacterState.Moving;


#if DEBUG
    public ulong LastUpdate;
#endif

    public Map? Map;
    
    //really silly to suppress null on these when they could actually be null but, well...
    private Player player = null!;
    private Monster monster = null!;
    private Npc npc = null!;
    private CombatEntity combatEntity = null!;

    public EntityList? Events { get; set; }
    public int EventsCount => Events?.Count ?? 0;

    public bool HasCombatEntity => Entity.IsAlive() && (Entity.Type == EntityType.Player || Entity.Type == EntityType.Monster);

    public Player Player
    {
        get
        {
#if DEBUG
            if (!Entity.IsAlive() || Entity.Type != EntityType.Player)
                throw new Exception($"Cannot get player type from world object {Entity} id {Id} as entity is either expired or not a player.");
#endif
            return player;
        }
    }
    public Monster Monster
    {
        get
        {
#if DEBUG
            if (!Entity.IsAlive() || Entity.Type != EntityType.Monster)
                throw new Exception($"Cannot get monster type from world object {Entity} id {Id} as entity is either expired or not a monster.");
#endif
            return monster;
        }
    }
    public Npc Npc
    {
        get
        {
#if DEBUG
            if (!Entity.IsAlive() || Entity.Type != EntityType.Npc)
                throw new Exception($"Cannot get npc type from world object {Entity} id {Id} as entity is either expired or not a npc.");
#endif
            return npc;
        }
    }
    public CombatEntity CombatEntity
    {
        get
        {
#if DEBUG
            if (!Entity.IsAlive() && (Entity.Type != EntityType.Player && Entity.Type != EntityType.Monster))
                throw new Exception($"Cannot get combat entity from world object {Entity} id {Id} as entity is either expired or not a monster or player.");
#endif
            return combatEntity;
        }
    }

    public void Reset()
    {
        Id = -1;
        Entity = Entity.Null;
        LastAttacked = Entity.Null;
        IsActive = true;
        AdminHidden = false;
        Map = null;
        Name = null!;
        State = CharacterState.Idle;
        MoveProgress = 0;
        MoveSpeed = 0.15f;
        MoveStep = 0;
        NextStepDuration = 0;
        Position = new Position();
        TargetPosition = new Position();
        FacingDirection = Direction.South;
        WalkPath = null;
        QueuedAction = QueuedAction.None;
        DisplayType = CharacterDisplayType.None;
        StepCount = 0;
        IsImportant = false;
        OverrideAppearanceState = null;
        AttackCooldown = 0f;
        SpawnImmunity = 0f;
        ClearVisiblePlayerList();

        if (Events != null)
        {
            Events.Clear();
            EntityListPool.Return(Events);
            Events = null;
        }
    }

    public void RollBackTimers(float time)
    {
        AttackCooldown -= time;
        SpawnImmunity -= time;
        MoveLockTime -= time;
        TimeOfLastMove -= time;
    }

    public void Init(ref Entity entity)
    {
        Entity = entity;
        switch (Entity.Type)
        {
            case EntityType.Player:
                combatEntity = Entity.Get<CombatEntity>();
                player = Entity.Get<Player>();
                break;
            case EntityType.Monster:
                combatEntity = Entity.Get<CombatEntity>();
                monster = Entity.Get<Monster>();
                break;
            case EntityType.Npc:
                npc = Entity.Get<Npc>();
                break;
        }
    }

    public void DebugMessage(string message, bool gatherPlayers = false)
    {
        if (gatherPlayers)
        {
            var list = EntityListPool.Get();
            CommandBuilder.AddAllPlayersAsRecipients();
            CommandBuilder.SendServerMessage($"{message}");
            CommandBuilder.ClearRecipients(); //if we don't clear it can cause issues
            EntityListPool.Return(list);
        }
        else
            CommandBuilder.SendServerMessage($"{message}");
    }
    
    public void ResetState(bool resetIfDead = false)
    {
        MoveProgress = 0;
        QueuedAction = QueuedAction.None;
        
        if (State != CharacterState.Dead || resetIfDead)
            State = CharacterState.Idle;
        if(Type == CharacterType.Player)
            Player.ClearTarget();
    }

    public bool IsAbleToBeSeenBy(WorldObject target)
    {
        if (!IsActive)
            return false;
        if (this == target)
            return true;
        return true; // !AdminHidden;
    }

    public void AddVisiblePlayer(Entity e)
    {
        if (visiblePlayers == null)
            visiblePlayers = EntityListPool.Get();

        //sanity check
        var obj2 = e.Get<WorldObject>();

#if DEBUG
        if (e.Type != EntityType.Player)
            ServerLogger.LogWarning($"WorldObject {Name} is attempting to add {obj2.Name} to it's visible players list, but that object is not a player.");
        //if (visiblePlayers.Contains(e))
        //    ServerLogger.LogWarning($"WorldObject {Name} is attempting to add a visible player {obj2.Name}, but that player is already tagged as visible.");
        //else
        //ServerLogger.Log($"WorldObject {Name} is adding a visible player {obj2.Name} to it's visible list.\n{Environment.StackTrace}");
#endif

        visiblePlayers.AddIfNotExists(ref e);
    }

    public bool HasVisiblePlayers() => visiblePlayers != null && visiblePlayers.Count > 0;
    public bool IsPlayerVisible(Entity e) => visiblePlayers?.Contains(e) ?? false;

    public int CountVisiblePlayers()
    {
        if (visiblePlayers == null)
            return 0;
        visiblePlayers.ClearInactive();
        return visiblePlayers.Count;
    }

    public void RemoveVisiblePlayer(Entity e)
    {
#if DEBUG
        //sanity check
        var obj2 = e.Get<WorldObject>();

        if (visiblePlayers == null)
        {
            ServerLogger.LogWarning($"WorldObject {Name} is attempting to remove {obj2.Name} from it's visible players list, but it currently has no visible players.");
            return;
        }

        if (e.Type != EntityType.Player)
            ServerLogger.LogWarning($"WorldObject {Name} is attempting to remove {obj2.Name} from it's visible players list, but that object is not a player.");
        else if (!visiblePlayers.Contains(e))
            ServerLogger.Debug($"WorldObject {Name} is attempting to remove visible entity {obj2.Name}, but that player is not on the visibility list.");
#endif

        visiblePlayers?.Remove(ref e);
    }

    public void ClearVisiblePlayerList()
    {
        if (visiblePlayers == null)
            return;

        //ServerLogger.Log($"WorldObject {Name} is clearing it's visible player list.");

        visiblePlayers.Clear();
        EntityListPool.Return(visiblePlayers);
        visiblePlayers = null;
    }

    public EntityList? GetVisiblePlayerList() => visiblePlayers;

    public bool TryGetVisiblePlayerList([NotNullWhen(true)] out EntityList? entityList)
    {
        if (visiblePlayers == null || visiblePlayers.Count == 0)
        {
            entityList = null;
            return false;
        }

        entityList = visiblePlayers;
        return true;
    }

    public void AttachEvent(Entity eventEntity)
    {
        Events ??= EntityListPool.Get();

        Events.Add(eventEntity);
    }

    public int CountEventsOfType(string eventType)
    {
        if (Events == null)
            return 0;

        var count = 0;
        foreach (var e in Events)
        {
            if(e.TryGet<Npc>(out var npc) && npc.EventType == eventType)
                count++;
        }

        return count;
    }

    public void OnDeathCleanupEvents()
    {
        if (Events == null)
            return;

        for (var i = 0; i < Events.Count; i++)
        {
            if (Events[i].TryGet<Npc>(out var npc))
            {
                var result = npc.Behavior.OnOwnerDeath(npc, CombatEntity);
                switch (result)
                {
                    case NpcBehaviorBase.EventOwnerDeathResult.DetachEvent:
                        npc.Owner = Entity.Null;
                        break;
                    case NpcBehaviorBase.EventOwnerDeathResult.RemoveEvent:
                        npc.EndEvent();
                        Events.SwapFromBack(i);
                        i--;
                        break;
                }
            }
        }

        Events.ClearInactive();
        
        if(Events.Count <= 0)
        {
            Events.Clear();
            EntityListPool.Return(Events);
            Events = null;
            return;
        }
    }
    
    public void SitStand(bool isSitting)
    {
        Debug.Assert(Map != null);
        
        if (Type != CharacterType.Player)
            return;

        if (State == CharacterState.Moving || State == CharacterState.Dead)
            return;

        if (CombatEntity.HasBodyState(BodyStateFlags.AnyHiddenState) || Player.GetStat(CharacterStat.Disabled) > 0)
            return;

        if (isSitting)
            State = CharacterState.Sitting;
        else
            State = CharacterState.Idle;

        var player = Entity.Get<Player>();
        //player.UpdateSit(isSitting);

        Map.AddVisiblePlayersAsPacketRecipients(this);
        CommandBuilder.ChangeSittingMulti(this);
        CommandBuilder.ClearRecipients();
    }

    public void ChangeLookDirection(Position lookAt, HeadFacing facing = HeadFacing.Center)
    {
        Debug.Assert(Map != null);

        if (State == CharacterState.Moving || State == CharacterState.Dead)
            return;

        FacingDirection = (lookAt - Position).Normalize().GetDirectionForOffset();

        if (Type == CharacterType.Player)
            Player.HeadFacing = facing;
        if (Type == CharacterType.NPC && OverrideAppearanceState != null)
            OverrideAppearanceState.HeadFacing = facing;
        
        Map.AddVisiblePlayersAsPacketRecipients(this);
        CommandBuilder.ChangeFacingMulti(this, lookAt);
        CommandBuilder.ClearRecipients();
    }
    public void LookAtEntity(ref Entity entity)
    {
        if(entity.TryGet<WorldObject>(out var chara))
            ChangeLookDirection(chara.Position);
    }
    public void StopMovingImmediately(bool resetState = true)
    {
        Debug.Assert(Map != null);

        if (State == CharacterState.Moving)
        {
            Map.AddVisiblePlayersAsPacketRecipients(this);
            CommandBuilder.CharacterStopImmediateMulti(this);
            CommandBuilder.ClearRecipients();
            if(resetState)
                State = CharacterState.Idle;
        }
    }

    public bool AddMoveLockTime(float delay, bool force = false)
    {
        Debug.Assert(Map != null);

        if (delay <= 0f)
            return false;

        if (InMoveLock && MoveLockTime < Time.ElapsedTimeFloat && !force)
            return false;
        
        if (!InMoveLock && State == CharacterState.Moving)
            StopMovingImmediately(false); //tell the client we stop, but we don't want to leave move state

        if(!force || Time.ElapsedTimeFloat + delay > MoveLockTime)
            MoveLockTime = Time.ElapsedTimeFloat + delay;
        InMoveLock = true;

        if(Type == CharacterType.Monster)
            Monster.AdjustAiUpdateIfShorter(MoveLockTime);

        return true;
    }

    public void ChangeToActionState()
    {
        ResetSpawnImmunity();

        if (Type == CharacterType.NPC && OverrideAppearanceState != null)
            OverrideAppearanceState.HeadFacing = HeadFacing.Center;


        if (Type == CharacterType.Player)
            player.HeadFacing = HeadFacing.Center; //don't need to send this to client, they will assume it resets
    }

    public bool CanMove()
    {
        if (State == CharacterState.Sitting || State == CharacterState.Dead)
            return false;
        
        if (MoveSpeed <= 0)
            return false;

        if (Type != CharacterType.NPC)
        {
            if (CombatEntity.HasBodyState(BodyStateFlags.Stopped))
                return false;
            if(CombatEntity.GetStat(CharacterStat.MoveSpeedBonus) < -100)
                return false;
            if (CombatEntity.HasBodyState(BodyStateFlags.Hidden))
                return false;
        }

        return true;
    }

    public float GetFirstStepDuration(FloatPosition nextTile)
    {
        var xDistance = WorldPosition.X - nextTile.X;
        var yDistance = WorldPosition.Y - nextTile.Y;
        var distance = MathF.Sqrt(xDistance * xDistance + yDistance * yDistance);
        return MoveSpeed * distance;
    }

    //updated TryMove function using float position instead
    public bool TryMove(Position target, int desiredDistanceToTarget)
    {
        Debug.Assert(Map != null);
        //Debug.Assert(desiredDistanceToTarget <= 1); //generally you will only move exactly onto the tile (0) or directly next to it (1)

        if (!CanMove())
            return false;

        if (!Map.WalkData.IsCellWalkable(target))
            return false;

        if (WalkPath == null)
            WalkPath = new Position[PathFinder.MaxDistance + 2];

        var len = Map.Instance.Pathfinder.GetPath(Map.WalkData, Position, target, WalkPath, desiredDistanceToTarget);
        if (len == 0)
            return false;

        TargetPosition = WalkPath[len - 1];
        NextStepDuration = GetFirstStepDuration(WalkPath[1]);
        MoveStartPosition = WorldPosition;
        MoveProgress = 0;
        MoveStep = 0;
        TotalMoveSteps = len;
        FacingDirection = (WalkPath[1] - WalkPath[0]).Normalize().GetDirectionForOffset();
        //MoveLockTime = Time.ElapsedTimeFloat;

        //ServerLogger.Log($"{Name} TryMove: World:{WorldPosition} NextStepDuration: {NextStepDuration}");

        if (HasCombatEntity && CombatEntity.IsCasting)
        {
            QueuedAction = QueuedAction.Move; //if we're casting we need to wait until we finish before we can move
            return true;
        }

        QueuedAction = QueuedAction.None;

        State = CharacterState.Moving;

        if(!InMoveLock)
            Map.StartMove(ref Entity, this);

        ChangeToActionState(); //does stuff like recenter the player's head and removes spawn immunity

        return true;
    }

    public void RecenterOnTile()
    {

    }

    //this shortens the move path of any moving character so they only finish the next tile movement and stop
    public void ShortenMovePath()
    {
        if (Map == null)
            return;

        //if it's not MoveStep + 2, that means the next step is already the last step.
        if (State == CharacterState.Moving && MoveStep + 2 < TotalMoveSteps)
        {
            if (WalkPath == null)
                throw new Exception($"Error stopping action of character {Name}, who is in the Moving state but it does not have a WalkPath object.");

            var target = WalkPath[MoveStep + 1];
            if (Position == target) //we might already be inside the next cell, in which case we'll stop at the next one over
            {
                MoveStep++;
                if (MoveStep + 2 >= TotalMoveSteps)
                    return;
                target = WalkPath[MoveStep + 1];
            }

            if(!TryMove(target, 0))
                ServerLogger.LogWarning($"We can't shorten our move path because the shorter path is... invalid?");

            //TotalMoveSteps = MoveStep + 2;
            //TargetPosition = WalkPath[TotalMoveSteps - 1];
        }

        //QueuedAction = QueuedAction.None;

        //Map.StartMove(ref Entity, this);

    }

    //replacement for PerformMoveUpdate that uses floating point positions
    private void PerformMoveUpdate2()
    {
        Debug.Assert(WalkPath != null);
        Debug.Assert(Map != null);

        if (InMoveLock)
        {
            if(MoveLockTime >= Time.ElapsedTimeFloat)
                return;
            InMoveLock = false;
            Map.StartMove(ref Entity, this);
        }

        MoveProgress += Time.DeltaTimeFloat;
        var lastPosition = WorldPosition;
        var lastCell = cellPosition;

        var startPosition = MoveStep == 0 ? MoveStartPosition : (FloatPosition)WalkPath[MoveStep];
        var endPosition = (FloatPosition)WalkPath[MoveStep + 1];
        var dir = FacingDirection;
        var newPosition = WorldPosition;

        while (MoveProgress > NextStepDuration)
        {
            MoveStep++;
            if (MoveStep >= TotalMoveSteps - 1)
            {
                //we've reached our destination
                State = CharacterState.Idle;
                Position = WalkPath[MoveStep];
                newPosition = Position;
                TotalMoveSteps = 0;
                break;
            }

            MoveProgress -= NextStepDuration;
            startPosition = WalkPath[MoveStep];
            endPosition = WalkPath[MoveStep + 1];
            dir = (WalkPath[MoveStep + 1] - WalkPath[MoveStep]).GetDirectionForOffset();
            if (dir.IsDiagonal())
                NextStepDuration = MoveSpeed * 1.4142f;
            else
                NextStepDuration = MoveSpeed;
        }

        if (State == CharacterState.Moving)
            newPosition = startPosition.Lerp(endPosition, MoveProgress / NextStepDuration);

        var newCell = (Position)newPosition;

        //ServerLogger.Log($"{Name}: MoveUpdate2: {InMoveLock} {MoveStep} {MoveProgress}");

        if (lastCell != newCell)
        {
            //Map.ChangeEntityPosition(ref Entity, this, lastCell, newCell, newPosition, true);
            FacingDirection = (newCell - lastCell).Normalize().GetDirectionForOffset();
            //FacingDirection = dir;
            StepCount++;
            Map.ChangeEntityPosition3(this, lastPosition, newPosition, true);
            if(Type == CharacterType.Player || Type == CharacterType.Monster)
                CombatEntity.StatusContainer?.OnMove(lastCell, newCell, false);
            Map.TriggerAreaOfEffectForCharacter(this, lastCell, newCell);
            TimeOfLastMove = Time.ElapsedTimeFloat;

            if (!IsActive || State == CharacterState.Dead) //we died after triggering an aoe
                return;
        }
        else
            WorldPosition = newPosition; //we still need to update our world position if we aren't doing a full cell move

        if (State == CharacterState.Idle)
        {
            if (!Map.IsEntityStacked(this))
                return;

            if (Map.FindUnoccupiedAdjacentTile(Position, out var newMove))
                TryMove(newMove, 0);
        }
    }

    public void Update()
    {
#if DEBUG
        if (LastUpdate == Time.UpdateCount)
            ServerLogger.LogError($"Entity {Entity} name {Name} is updating twice in one frame! Current update tick is: {Time.UpdateCount}");

        LastUpdate = Time.UpdateCount;
#endif

        if (Type == CharacterType.NPC)
        {
            npc.Update();
            if (IsActive && State == CharacterState.Moving)
                PerformMoveUpdate2();
            return;
        }

        if (visiblePlayers != null)
            visiblePlayers.ClearInactive();

        if (State == CharacterState.Moving)
            PerformMoveUpdate2();
        
        if (Entity.Type == EntityType.Player)
        {
#if DEBUG
            if (Map == null)
                throw new Exception($"Update called on player entity {Name} ({Type}) while not attached to any map!");
#endif

            player.Update();
            combatEntity.Update();
        }

        if (Entity.Type == EntityType.Monster)
        {
            monster.Update();
            combatEntity.Update();
        }
    }
}