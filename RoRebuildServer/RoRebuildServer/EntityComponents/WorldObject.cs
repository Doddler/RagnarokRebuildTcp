using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(new [] { EntityType.Player , EntityType.Monster, EntityType.Npc, EntityType.Effect})]
public class WorldObject : IEntityAutoReset
{
    public int Id { get; set; }
    public Entity Entity;
    public string Name;
    public bool IsActive;
    public int ClassId;
    public Direction FacingDirection;
    public CharacterState State;
    public CharacterType Type;
    public Position Position;
    public Position TargetPosition;

    public Position[]? WalkPath;

    public Entity LastAttacked { get; set; }

    private EntityList? visiblePlayers;

    public float SpawnImmunity;
    public float AttackCooldown;
    public float MoveSpeed;
    public float MoveCooldown;
    public float HitDelay;
    public int MoveStep;
    public int TotalMoveSteps;

    
    
#if DEBUG
    public ulong LastUpdate;
#endif

    public Map? Map;

    private Player player;
    private Monster monster;
    private Npc npc;
    private CombatEntity combatEntity;
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
        Map = null;
        Name = null;
        State = CharacterState.Idle;
        MoveCooldown = 0;
        MoveSpeed = 0.15f;
        MoveStep = 0;
        Position = new Position();
        TargetPosition = new Position();
        FacingDirection = Direction.South;
        WalkPath = null;
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


	public void ResetState()
    {
        MoveCooldown = 0;
        State = CharacterState.Idle;
    }

    public void AddVisiblePlayer(Entity e)
    {
        if (visiblePlayers == null)
            visiblePlayers = EntityListPool.Get();


#if DEBUG
        //sanity check
        var obj2 = e.Get<WorldObject>();

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
        if (visiblePlayers == null)
            return;

#if DEBUG
        //sanity check
        var obj2 = e.Get<WorldObject>();
        
        if (e.Type != EntityType.Player)
            ServerLogger.LogWarning($"WorldObject {Name} is attempting to remove {obj2.Name} from it's visible players list, but that object is not a player.");
        else if (!visiblePlayers.Contains(e))
            ServerLogger.Debug($"WorldObject {Name} is attempting to remove visible entity {obj2.Name}, but that player is not on the visibility list.");
        //else
            //ServerLogger.Log($"WorldObject {Name} is removing a player {obj2.Name} from it's visible list.\n{Environment.StackTrace}");
#endif

        visiblePlayers.Remove(ref e);
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

    public bool TryGetVisiblePlayerList(out EntityList entityList)
    {
        if (visiblePlayers == null)
        {
            entityList = null!;
            return false;
        }

        entityList = visiblePlayers;
        return true;
    }
    
    public void SitStand(bool isSitting)
    {
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

        Map.GatherPlayersForMultiCast(ref Entity, this);
        CommandBuilder.ChangeSittingMulti(this);
        CommandBuilder.ClearRecipients();
    }

    public void ChangeLookDirection(ref Entity entity, Direction direction, HeadFacing facing)
    {
        if (State == CharacterState.Moving || State == CharacterState.Dead)
            return;

        FacingDirection = direction;

        var player = entity.Get<Player>();
        if (player != null)
            player.HeadFacing = facing;

        Map.GatherPlayersForMultiCast(ref entity, this);
        CommandBuilder.ChangeFacingMulti(this);
        CommandBuilder.ClearRecipients();
    }

    public void StopMovingImmediately()
    {
        if (State == CharacterState.Moving)
        {
            Map.GatherPlayersForMultiCast(ref Entity, this);
            CommandBuilder.CharacterStopImmediateMulti(this);
            CommandBuilder.ClearRecipients();
            State = CharacterState.Idle;
        }
    }

    public bool AddMoveDelay(float delay)
    {
        if (HitDelay > Time.ElapsedTimeFloat)
            return false;

        HitDelay = Time.ElapsedTimeFloat + delay;

        return true;
    }

    private void ChangeToActionState()
    {
        SpawnImmunity = -1f;

        if (Type != CharacterType.Player)
            return;

        var player = Entity.Get<Player>();
        player.HeadFacing = HeadFacing.Center; //don't need to send this to client, they will assume it resets
    }

    public bool TryMove(ref Entity entity, Position target, int range)
    {
        if (State == CharacterState.Sitting || State == CharacterState.Dead)
            return false;

        if (MoveSpeed <= 0)
            return false;

        if (!Map.WalkData.IsCellWalkable(target))
            return false;
        
        if (WalkPath == null)
            WalkPath = new Position[17];

        var hasOld = false;
        var oldNext = new Position();
        var oldCooldown = MoveCooldown;

        if (MoveStep + 1 < TotalMoveSteps && State == CharacterState.Moving)
        {
            oldNext = WalkPath[MoveStep + 1];
            hasOld = true;
        }

        int len;

        //we won't interrupt the next step we are currently taking, so append it to the start of our new path.
        if (hasOld)
            len = Map.Instance.Pathfinder.GetPathWithInitialStep(Map.WalkData, Position, oldNext, target, WalkPath, range);
        else
            len = Map.Instance.Pathfinder.GetPath(Map.WalkData, Position, target, WalkPath, range);

        if (len == 0)
            return false;

        TargetPosition = WalkPath[len - 1]; //reset to last point in walkpath
        MoveCooldown = MoveSpeed;
        MoveStep = 0;
        TotalMoveSteps = len;
        FacingDirection = (WalkPath[1] - WalkPath[0]).GetDirectionForOffset();

        State = CharacterState.Moving;

        if (hasOld)
            MoveCooldown = oldCooldown;

        //if (Type == CharacterType.Player)
        //    ServerLogger.Log("Player moving! Starting from : " + WalkPath[0] + " speed is " + MoveSpeed);

        Map.StartMove(ref entity, this);
        ChangeToActionState();

        return true;
    }


    public void StopAction()
    {
        var needsStop = false;

        //if it's not MoveStep + 2, that means the next step is already the last step.
        if (State == CharacterState.Moving && MoveStep + 2 < TotalMoveSteps)
        {
            TotalMoveSteps = MoveStep + 2;
            TargetPosition = WalkPath[TotalMoveSteps - 1];

            //ServerLogger.Log("Stopping player at: " + TargetPosition);
            needsStop = true;
        }

        if (!needsStop)
            return;

        Map.StartMove(ref Entity, this);
    }
    
    public void Update()
    {
#if DEBUG
        if(Time.UpdateCount == LastUpdate)
            ServerLogger.LogError($"Entity {Entity} name {Name} is updating twice in one frame! Current update tick is: {Time.UpdateCount}");

        LastUpdate = Time.UpdateCount;
#endif

        if (Type == CharacterType.NPC)
            return;

        SpawnImmunity -= Time.DeltaTimeFloat;

        if(visiblePlayers != null)
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
        {
            if (HitDelay > Time.ElapsedTimeFloat)
                return;
            
            if (FacingDirection.IsDiagonal())
                MoveCooldown -= Time.DeltaTimeFloat * 0.8f;
            else
                MoveCooldown -= Time.DeltaTimeFloat;
            
            if (MoveCooldown <= 0f)
            {
                FacingDirection = (WalkPath[MoveStep + 1] - WalkPath[MoveStep]).GetDirectionForOffset();

                MoveStep++;
                var startPos = Position;
                var nextPos = WalkPath[MoveStep];

                if (Map == null)
                    return;

                Map.MoveEntity(ref Entity, this, nextPos, true);

                if (nextPos == TargetPosition)
                    State = CharacterState.Idle;
                else
                {
                    FacingDirection = (WalkPath[MoveStep + 1] - WalkPath[MoveStep]).GetDirectionForOffset();
                    MoveCooldown += MoveSpeed;
                }

                if (Type == CharacterType.Player)
                {
                    var player = Entity.Get<Player>();
                    player.UpdatePosition();
                }

                if (Type == CharacterType.Monster)
                {
                    var monster = Entity.Get<Monster>();
                    monster.ResetDelay();
                }

                Map.TriggerAreaOfEffectForCharacter(this, startPos, nextPos);
            }
        }
    }

}