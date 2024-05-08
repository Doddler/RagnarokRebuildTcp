using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Database.Domain;
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
    NpcInteract
}

[EntityComponent(new[] { EntityType.Player, EntityType.Monster, EntityType.Npc, EntityType.Effect })]
public class WorldObject : IEntityAutoReset
{
    public int Id { get; set; }
    public Entity Entity;
    public string Name = null!;
    public bool IsActive;
    public bool Hidden { get; set; }
    public int ClassId;
    public Direction FacingDirection;
    public CharacterState State;
    public CharacterType Type;
    public Position Position;
    public Position TargetPosition;
    public float MoveModifier;
    public float MoveModifierTime;

    public Position[]? WalkPath;

    public Entity LastAttacked { get; set; }

    private EntityList? visiblePlayers;

    public float SpawnImmunity { get; set; } //you aren't seen as a valid target while this is active
    public float AttackCooldown { get; set; } //delay until you can attack again
    public float MoveSpeed { get; set; } //time it takes to traverse one tile
    public float MoveCooldown { get; set; } //the amount of time that remains before you step to the next tile
    public float MoveLockTime { get; set; } //you cannot move while until this time is reached

    public bool InMoveLock => MoveLockTime > Time.ElapsedTimeFloat;
    public bool InAttackCooldown => AttackCooldown > Time.ElapsedTimeFloat;

    public int MoveStep { get; set; }
    public int TotalMoveSteps { get; set; }

    public float TimeToReachNextStep => FacingDirection.IsDiagonal() ? MoveCooldown * DiagonalSpeedPenalty : MoveCooldown;

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

    private const float DiagonalSpeedPenalty = 0.8f;

    public int StepsRemaining => TotalMoveSteps - MoveStep;
    public bool IsMoving => State == CharacterState.Moving;
    public void SetSpawnImmunity(float time = 5f) => SpawnImmunity = Time.ElapsedTimeFloat + time;
    public void ResetSpawnImmunity() => SpawnImmunity = -1f;
    public bool IsTargetImmune => SpawnImmunity > Time.ElapsedTimeFloat;


#if DEBUG
    public ulong LastUpdate;
#endif

    public Map? Map;

    //really silly to suppress null on these when they could actually be null but, well...
    private Player player = null!;
    private Monster monster = null!;
    private Npc npc = null!;
    private CombatEntity combatEntity = null!;

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
        Hidden = false;
        Map = null;
        Name = null!;
        State = CharacterState.Idle;
        MoveCooldown = 0;
        MoveSpeed = 0.15f;
        MoveStep = 0;
        Position = new Position();
        TargetPosition = new Position();
        FacingDirection = Direction.South;
        WalkPath = null;
        QueuedAction = QueuedAction.None;
        ClearVisiblePlayerList();
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

    public void ResetState(bool resetIfDead = false)
    {
        MoveCooldown = 0;
        QueuedAction = QueuedAction.None;

        if (State != CharacterState.Dead || resetIfDead)
            State = CharacterState.Idle;
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
        else if (visiblePlayers.Contains(e))
            ServerLogger.Debug($"WorldObject {Name} is attempting to add a visible player {obj2.Name}, but that player is already tagged as visible.");
        //else
        //ServerLogger.Log($"WorldObject {Name} is adding a visible player {obj2.Name} to it's visible list.\n{Environment.StackTrace}");
#endif

        visiblePlayers.Add(e);
    }

    public bool IsPlayerVisible(Entity e)
    {
        if (visiblePlayers == null)
            return false;

        return visiblePlayers.Contains(e);
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

    public void SitStand(bool isSitting)
    {
        Debug.Assert(Map != null);

        if (Type != CharacterType.Player)
            return;

        if (State == CharacterState.Moving || State == CharacterState.Dead)
            return;

        if (isSitting)
            State = CharacterState.Sitting;
        else
            State = CharacterState.Idle;

        var player = Entity.Get<Player>();
        player.UpdateSit(isSitting);

        Map.GatherPlayersForMultiCast(this);
        CommandBuilder.ChangeSittingMulti(this);
        CommandBuilder.ClearRecipients();
    }

    public void ChangeLookDirection(ref Entity entity, Direction direction, HeadFacing facing)
    {
        Debug.Assert(Map != null);

        if (State == CharacterState.Moving || State == CharacterState.Dead)
            return;

        FacingDirection = direction;

        var player = entity.Get<Player>();
        player.HeadFacing = facing;

        Map.GatherPlayersForMultiCast(this);
        CommandBuilder.ChangeFacingMulti(this);
        CommandBuilder.ClearRecipients();
    }
    public void LookAtEntity(ref Entity entity)
    {
        if (!entity.IsAlive())
            return;

        ChangeLookDirection(ref Entity, (entity.Get<WorldObject>().Position - Position).Normalize().GetDirectionForOffset(), HeadFacing.Center);
    }
    public void StopMovingImmediately()
    {
        Debug.Assert(Map != null);

        if (State == CharacterState.Moving)
        {
            Map.GatherPlayersForMultiCast(this);
            CommandBuilder.CharacterStopImmediateMulti(this);
            CommandBuilder.ClearRecipients();
            State = CharacterState.Idle;
        }
    }

    public bool AddMoveLockTime(float delay)
    {
        Debug.Assert(Map != null);

        if (MoveLockTime > Time.ElapsedTimeFloat)
            return false;

        MoveLockTime = Time.ElapsedTimeFloat + delay;

        return true;
    }

    public void ChangeToActionState()
    {
        ResetSpawnImmunity();

        if (Type != CharacterType.Player)
            return;

        var player = Entity.Get<Player>();
        player.HeadFacing = HeadFacing.Center; //don't need to send this to client, they will assume it resets
    }

    public bool CanMove()
    {
        if (State == CharacterState.Sitting || State == CharacterState.Dead)
            return false;

        if (MoveSpeed <= 0)
            return false;
        
        return true;
    }

    
    /// <summary>
    /// Attempt to move within a certain distance of the target.
    /// </summary>
    /// <param name="target">The target location.</param>
    /// <param name="range">The distance to the target you wish to enter within.</param>
    /// <param name="useOldNextStep">Should we force the first step of the next move to
    /// be the step the player is currently on, if they're moving?</param>
    /// <returns>True if a target position was set.</returns>
    public bool TryMove(Position target, int range, bool useOldNextStep = true)
    {
        Debug.Assert(Map != null);

        //if(MoveLockTime > Time.ElapsedTimeFloat)
        //    ServerLogger.Debug($"{Name} beginning a move while in hit lock state.");

        if (!CanMove())
            return false;

        if (!Map.WalkData.IsCellWalkable(target))
            return false;

        if (WalkPath == null)
            WalkPath = new Position[Pathfinder.MaxDistance+2];

        var hasOld = false;
        var oldNext = new Position();
        var oldCooldown = MoveCooldown;

        if (MoveStep + 1 < TotalMoveSteps && State == CharacterState.Moving && useOldNextStep)
        {
            oldNext = WalkPath[MoveStep + 1];
            if (oldNext.DistanceTo(Position) > 1)
                throw new Exception("Our next move is a tile more than 1 tile away!");
            hasOld = true;
        }

        int len;

        //var moveRange = DistanceCache.FitSquareRangeInCircle(range);

        if (range <= 1)
        {
            //we won't interrupt the next step we are currently taking, so append it to the start of our new path.
            if (hasOld)
                len = Map.Instance.Pathfinder.GetPathWithInitialStep(Map.WalkData, Position, oldNext, target, WalkPath, range);
            else
                len = Map.Instance.Pathfinder.GetPath(Map.WalkData, Position, target, WalkPath, range);
        }
        else
        {
            len = Map.Instance.Pathfinder.GetPathWithinAttackRange(Map.WalkData, Position, hasOld ? oldNext : Position.Invalid, target, WalkPath, range);
        }

        if (len == 0)
            return false;

        TargetPosition = WalkPath[len - 1]; //reset to last point in walkpath
        MoveCooldown = MoveSpeed;
        MoveStep = 0;
        TotalMoveSteps = len;
        FacingDirection = (WalkPath[1] - WalkPath[0]).GetDirectionForOffset();
        
        if (hasOld)
        {
            QueuedAction = QueuedAction.None;
            MoveCooldown = oldCooldown;
        }

        if (HasCombatEntity && CombatEntity.IsCasting)
        {
            QueuedAction = QueuedAction.Move;
            return true;
        }

        State = CharacterState.Moving;

        Map.StartMove(ref Entity, this);
        ChangeToActionState();

        return true;
    }

    //this shortens the move path of any moving character so they only finish the next tile movement and stop
    public void ShortenMovePath()
    {
        if (Map == null)
            return;

        var needsStop = false;

        //if it's not MoveStep + 2, that means the next step is already the last step.
        if (State == CharacterState.Moving && MoveStep + 2 < TotalMoveSteps)
        {
            if (WalkPath == null)
                throw new Exception($"Error stopping action of character {Name}, who is in the Moving state but it does not have a WalkPath object.");

            TotalMoveSteps = MoveStep + 2;
            TargetPosition = WalkPath[TotalMoveSteps - 1];

            needsStop = true;
        }

        QueuedAction = QueuedAction.None;

        if (!needsStop)
            return;

        //ServerLogger.Log($"Stopping {Name} at: " + TargetPosition);

        var time = MoveCooldown;
        if (FacingDirection.IsDiagonal())
            time *= 1 / DiagonalSpeedPenalty;

        Map.SendFixedWalkMove(ref Entity, this, TargetPosition, time);
    }

    private void PerformMoveUpdate()
    {
        if (MoveLockTime > Time.ElapsedTimeFloat)
            return;

        MoveModifierTime -= Time.DeltaTimeFloat;
        if (MoveModifierTime < 0)
            MoveModifier = 1f;

        if (FacingDirection.IsDiagonal())
            MoveCooldown -= Time.DeltaTimeFloat * DiagonalSpeedPenalty * MoveModifier;
        else
            MoveCooldown -= Time.DeltaTimeFloat * MoveModifier;

        if (MoveCooldown <= 0f)
        {
            Debug.Assert(WalkPath != null);

            FacingDirection = (WalkPath[MoveStep + 1] - WalkPath[MoveStep]).GetDirectionForOffset();

            MoveStep++;
            var startPos = Position;
            var nextPos = WalkPath[MoveStep];

            if (Map == null)
                return;

            Map.ChangeEntityPosition(ref Entity, this, nextPos, true);

            if (nextPos == TargetPosition)
            {
                Debug.Assert(CombatEntity != null);
                State = CharacterState.Idle;
                if (QueuedAction == QueuedAction.Cast && AttackCooldown < Time.ElapsedTimeFloat && CombatEntity.QueuedCastingSkill.IsValid)
                    CombatEntity?.ResumeQueuedSkillAction();
            }
            else
            {
                FacingDirection = (WalkPath[MoveStep + 1] - WalkPath[MoveStep]).GetDirectionForOffset();
                MoveCooldown += MoveSpeed;
            }

            if (Type == CharacterType.Player)
            {
                var player = Entity.Get<Player>();
                player.UpdatePosition();
                if (State == CharacterState.Idle && player.IsInNpcInteraction)
                    LookAtEntity(ref player.NpcInteractionState.NpcEntity); //the player is already interacting with an npc, so we should turn to look at it
            }

            if (Type == CharacterType.Monster)
            {
                var monster = Entity.Get<Monster>();
                monster.ResetDelay();
            }

            Map.TriggerAreaOfEffectForCharacter(this, startPos, nextPos);
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
            return;
        }

        if (visiblePlayers != null)
            visiblePlayers.ClearInactive();

        if (Entity.Type == EntityType.Player)
        {
            player.Update();
            combatEntity.Update();
        }

        if (Entity.Type == EntityType.Monster)
        {
            monster.Update();
            combatEntity.Update();
        }

        if (State == CharacterState.Idle)
            return;

        if (State == CharacterState.Moving)
            PerformMoveUpdate();

    }

}