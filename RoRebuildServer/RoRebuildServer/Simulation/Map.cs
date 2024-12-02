using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Numerics;
using System.Threading;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Items;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation;

public class Map
{
    public int Id;
    public int Width;
    public int Height;
    public Chunk[] Chunks;
    public Instance Instance;
    public World World;

    public ServerMapConfig MapConfig;

    public Area MapBounds;
    public Area ChunkBounds;
    public string Name;

    public MapWalkData WalkData;

    private readonly int chunkWidth;
    private readonly int chunkHeight;

    private int chunkCheckId;

    public int PlayerCount { get; set; }
    public EntityList Players { get; set; } = new EntityList(8);
    public EntityList MapImportantEntities { get; set; } = new EntityList(8);
    public Dictionary<int, int> ItemChunkLookup = new();
    
    //private int playerCount;
    //public int PlayerCount
    //{
    //    get => playerCount;
    //    set
    //    {
    //        if (value != playerCount)
    //            ServerLogger.Debug($"Map {Name} changed player count to {value}.");
    //        playerCount = value;
    //    }
    //}

    private int entityCount = 0;

    private int ChunkSize { get; set; } = 8;

    public bool CanTeleport;

    public void AddPlayerVisibility(WorldObject player, WorldObject observer)
    {
        observer.AddVisiblePlayer(player.Entity);
        //if (other.Type == CharacterType.Player && player != other)
        //    player.AddVisiblePlayer(other.Entity);
    }

    public void RemovePlayerVisibility(WorldObject player, WorldObject observer)
    {
        observer.RemoveVisiblePlayer(player.Entity);
        //if (other.Type == CharacterType.Player && player != other && !other.Hidden)
        //    player.RemoveVisiblePlayer(other.Entity);
        //else
        //{
        //    ServerLogger.LogWarning($"Attempted to remove player {player.Name} from visibility list of entity {other.Name}:{other.Id}, but it can't actually see this player.");
        //}
    }
    
    /// <summary>
    /// Simplified variation of MoveEntity for any move where the entity is removed from it's old location and
    /// appears in a new one. Takes a move reason so the client can play the appropriate effect.
    /// </summary>
    public void TeleportEntity(ref Entity entity, WorldObject ch, Position newPosition, CharacterRemovalReason reason = CharacterRemovalReason.Teleport)
    {
        var oldPosition = ch.Position;

        //ch.StopMovingImmediately();

        SendRemoveEntityAroundCharacter(ref entity, ch, reason);
        ch.Position = newPosition;
        ch.ClearVisiblePlayerList();

        //check if the move puts them over to a new chunk, and if so, move them to the new one
        var cOld = GetChunkForPosition(oldPosition);
        var cNew = GetChunkForPosition(newPosition);

        if (cOld != cNew)
        {
            if (!cOld.RemoveEntity(ref entity, ch.Type))
                throw new Exception($"For some reason the entity doesn't exist in the old chunk when moving chunks.");
            cNew.AddEntity(ref entity, ch.Type);
        }

        //if the moving entity is a player, he needs to know of the new/removed entities from his sight
        if (ch.Type == CharacterType.Player)
        {
            var movingPlayer = entity.Get<Player>();
            CommandBuilder.SendRemoveAllEntities(movingPlayer);
            ch.IsActive = false; //we should set this after removing all entities from any given player
            ActivatePlayerAndNotifyNearby(movingPlayer);

            //SendAllEntitiesToPlayer(ref entity);

            if (ch.Hidden)
                CommandBuilder.SendAdminHideStatus(ch.Player, ch.Hidden);
        }
        else
        {
            //monsters and npcs
            SendAddEntityAroundCharacter(ref entity, ch); //if we are a monster, tell all nearby players about us
        }
    }


    public void RefreshEntity(WorldObject ch)
    {
        if (ch.TryGetVisiblePlayerList(out var list))
        {
            CommandBuilder.AddRecipients(list);
            CommandBuilder.SendRemoveEntityMulti(ch, CharacterRemovalReason.Refresh);
            CommandBuilder.SendCreateEntityMulti(ch);
            CommandBuilder.ClearRecipients();
        }

        //SendRemoveEntityAroundCharacter(ref entity, ch, reason);
        //SendAddEntityAroundCharacter(ref entity, ch);
        //if(entity.Type == EntityType.Player)
        //    CommandBuilder.SendCreateEntity(ch, entity.Get<Player>());
    }

    /// <summary>
    /// A character is moving!! We need to update the visibility between our entity and other players that we could not see before that we can see now,
    /// as well as those we could see us before that we are now out of range of.
    /// </summary>
    /// <param name="movingCharacter">The moving character.</param>
    /// <param name="otherCharacter">A character that the player can or could see.</param>
    /// <param name="removeList">The batch of players to be informed that our character is going out of range.</param>
    /// <param name="addList">The batch of players to be informed that our character is now visible.</param>
    /// <returns></returns>
    private bool ResolveVisibilityForMovingCharacter(WorldObject movingCharacter, WorldObject otherCharacter, EntityList removeList, EntityList addList)
    {
        var inRange = movingCharacter.Position.InRange(otherCharacter.Position, ServerConfig.MaxViewDistance);
        var hasChange = false;

        //if the moving entity is a player, update the other entity's visible player list and update them on our client
        if (movingCharacter.Type == CharacterType.Player)
        {
            var shouldBeSeen = movingCharacter.IsAbleToBeSeenBy(otherCharacter) && inRange;
            var canBeSeen = otherCharacter.IsPlayerVisible(movingCharacter.Entity);

            if (canBeSeen && !shouldBeSeen)
            {
                otherCharacter.RemoveVisiblePlayer(movingCharacter.Entity);
                if(otherCharacter.IsAbleToBeSeenBy(movingCharacter))
                    CommandBuilder.SendRemoveEntity(otherCharacter, movingCharacter.Player, CharacterRemovalReason.OutOfSight);
                hasChange = true;
            }

            if (!canBeSeen && shouldBeSeen)
            {
                otherCharacter.AddVisiblePlayer(movingCharacter.Entity);
                if (otherCharacter.IsAbleToBeSeenBy(movingCharacter))
                    CommandBuilder.SendCreateEntity(otherCharacter, movingCharacter.Player);
                hasChange = true;
            }
        }

        //if the other character is a player, update our character's visibility set and update their client
        if (otherCharacter.Type == CharacterType.Player)
        {
            var shouldSee = otherCharacter.IsAbleToBeSeenBy(movingCharacter) && inRange;
            var canSee = movingCharacter.IsPlayerVisible(otherCharacter.Entity);

            if (canSee && !shouldSee)
            {
                movingCharacter.RemoveVisiblePlayer(otherCharacter.Entity);
                if (movingCharacter.IsAbleToBeSeenBy(otherCharacter))
                    removeList.Add(otherCharacter.Entity);
                hasChange = true;
            }

            if (!canSee && shouldSee)
            {
                movingCharacter.AddVisiblePlayer(otherCharacter.Entity);
                if (movingCharacter.IsAbleToBeSeenBy(otherCharacter))
                    addList.Add(otherCharacter.Entity);
                hasChange = true;
            }
        }

        return hasChange;
    }

    //yes I rewrote this 3 times
    public void ChangeEntityPosition3(WorldObject movingCharacter, FloatPosition oldWorldPosition, FloatPosition newWorldPosition, bool isWalkUpdate)
    {
        var oldPosition = (Position)oldWorldPosition;
        var newPosition = (Position)newWorldPosition;
        var distance = movingCharacter.Position.SquareDistance(newPosition);

        movingCharacter.WorldPosition = newWorldPosition;

        CommandBuilder.ClearRecipients();

        //check if the move puts us over to a new chunk, and if so, move us to the new one
        var cOld = GetChunkForPosition(oldPosition);
        var cNew = GetChunkForPosition(newPosition);

        var movingEntity = movingCharacter.Entity;

        if (cOld != cNew)
        {
            if (!cOld.RemoveEntity(ref movingEntity, movingCharacter.Type))
                throw new Exception($"For some reason the entity doesn't exist in the old chunk when moving chunks.");
            cNew.AddEntity(ref movingCharacter.Entity, movingCharacter.Type);
        }

        //Case 1: The easy case. We've moved so far away from our old position that we are guaranteed that no entity that could
        //        see us before can see us after moving.
        if (distance > ServerConfig.MaxViewDistance * 2 + 1)
        {
            //tell all nearby players and monsters we're gone (including ourselves!)
            SendRemoveEntityAroundCharacter(ref movingEntity, movingCharacter, CharacterRemovalReason.OutOfSight);
            movingCharacter.ClearVisiblePlayerList();

            //let's tell everyone we can now see that we're here now
            if (movingCharacter.Type == CharacterType.Monster)
                SendAddEntityAroundCharacter(ref movingEntity, movingCharacter); //if we are a monster, tell all nearby players about us
            else
            {
                movingCharacter.IsActive = false; //Needed to not have ActivatePlayerAndNotifyNearby complain about activating an active player
                ActivatePlayerAndNotifyNearby(movingCharacter.Player); //tell everyone we're here and tell us everyone we can see
            }

            return;
        }

        //Case 2: The hard case. We've moved a short distance. We need to resolve visibility with those we can no longer see,
        //        update those who can see us, and inform those we can now see that we couldn't before.
        using var addList = EntityListPool.Get();
        using var removeList = EntityListPool.Get();
        CommandBuilder.ClearRecipients();

        if(removeList.Count > 0)
            ServerLogger.LogWarning("AAAA");

        var midPoint = (oldPosition + newPosition) / 2;
        var dist2 = ServerConfig.MaxViewDistance + (distance / 2) + 1;
        foreach (var chunk in GetChunkEnumeratorAroundPosition(midPoint, dist2))
        {
            //a monster moving only needs to notify players but a player moving needs to notify everyone
            var list = movingCharacter.Type == CharacterType.Player ? chunk.AllEntities : chunk.Players;

            foreach (var entity in list)
            {
                if (!entity.TryGet<WorldObject>(out var otherCharacter))
                    continue;
                
                var addedOrRemoved = ResolveVisibilityForMovingCharacter(movingCharacter, otherCharacter, removeList, addList);

                if (!isWalkUpdate && !addedOrRemoved && movingCharacter.Position.InRange(otherCharacter.Position, ServerConfig.MaxViewDistance))
                {
                    //the player is in range and was in range before we moved, so if it's not a walk update we need to send a move event
                    if(movingCharacter.Type == CharacterType.Player)
                        CommandBuilder.AddRecipient(movingCharacter.Entity);
                    if(otherCharacter.Type == CharacterType.Player && movingCharacter.IsAbleToBeSeenBy(otherCharacter))
                        CommandBuilder.AddRecipient(otherCharacter.Entity);
                }
            }

            if (movingCharacter.Type == CharacterType.Player)
            {
                foreach (var item in chunk.GroundItems)
                {
                    var pos = (Position)item.Position;

                    var couldSeeItem = pos.InRange(oldPosition, ServerConfig.MaxViewDistance);
                    var canSeeItem = pos.InRange(newPosition, ServerConfig.MaxViewDistance);

                    if(canSeeItem && !couldSeeItem)
                        CommandBuilder.RevealDropItemForPlayer(item, false, movingCharacter.Player);
                    if (couldSeeItem && !canSeeItem)
                        CommandBuilder.RemoveDropItemForSinglePlayer(item, movingCharacter.Player);
                }
            }
        }

        if (CommandBuilder.HasRecipients()) //happens if it's not a walk update and the player could see the moving character before and after
        {
            CommandBuilder.SendMoveEntityMulti(movingCharacter);
            CommandBuilder.ClearRecipients();
        }

        if (addList.Count > 0) //happens when a player can see a moving entity that it could not see before
        {
            CommandBuilder.AddRecipients(addList);
            CommandBuilder.SendCreateEntityMulti(movingCharacter);
            CommandBuilder.ClearRecipients();
        }

        if (removeList.Count > 0) //happens when a player can no longer see the moving entity when it used to be able to
        {
            CommandBuilder.AddRecipients(removeList);
            CommandBuilder.SendRemoveEntityMulti(movingCharacter, CharacterRemovalReason.OutOfSight);
            CommandBuilder.ClearRecipients();
        }

        if (movingCharacter.IsImportant)
        {
            if(!isWalkUpdate || movingCharacter.StepCount % 4 == 0)
                UpdateImportantEntity(movingCharacter);
        }
    }

    public void AddEntity(ref Entity entity, bool addToInstance = true)
    {
        var ch = entity.Get<WorldObject>();
#if DEBUG
        if (ch == null)
            throw new Exception("Entity was added to map without Character object!");
#endif

        ch.Map = this;
        if (ch.IsActive)
            SendAddEntityAroundCharacter(ref entity, ch);

        var c = GetChunkForPosition(ch.Position);
        c.AddEntity(ref entity, ch.Type);


        if (addToInstance)
            Instance.Entities.Add(ref entity);

        if (ch.Type == CharacterType.Player)
        {
            Debug.Assert(!Players.Contains(ref entity));
            PlayerCount++;
            Players.Add(ref entity);
            ServerLogger.Debug($"Map {Name} changed player count to {PlayerCount}.");

        }

        entityCount++;
    }

    public void AddEntityWithEvent(ref Entity entity, CreateEntityEventType eventType, Position pos, bool addToInstance = true)
    {
        var ch = entity.Get<WorldObject>();
#if DEBUG
        if (ch == null)
            throw new Exception("Entity was added to map without Character object!");
#endif

        ch.Map = this;
        if (ch.IsActive)
            SendAddEntityAroundCharacterWithEvent(ref entity, ch, eventType, pos);

        var c = GetChunkForPosition(ch.Position);
        c.AddEntity(ref entity, ch.Type);


        if (addToInstance)
            Instance.Entities.Add(ref entity);

        if (ch.Type == CharacterType.Player)
        {
            Debug.Assert(!Players.Contains(ref entity));
            PlayerCount++;
            Players.Add(ref entity);
            ServerLogger.Debug($"Map {Name} changed player count to {PlayerCount}.");

        }

        entityCount++;
    }

    public void PrepareAddEntityAroundCharacter(ref Entity entity, WorldObject ch)
    {
        CommandBuilder.ClearRecipients();
        foreach (Chunk chunk in GetChunkEnumeratorAroundPosition(ch.Position, ServerConfig.MaxViewDistance))
        {
            foreach (var player in chunk.Players)
            {
                var targetCharacter = player.Get<WorldObject>();
                if (!targetCharacter.IsActive)
                    continue;
                if (!targetCharacter.Position.InRange(ch.Position, ServerConfig.MaxViewDistance))
                    continue;

                if (targetCharacter != ch && !ch.Hidden)
                    CommandBuilder.AddRecipient(player);

                AddPlayerVisibility(targetCharacter, ch);
            }
        }
    }

    public void SendAddEntityAroundCharacter(ref Entity entity, WorldObject ch)
    {
        PrepareAddEntityAroundCharacter(ref entity, ch);

        if (CommandBuilder.HasRecipients())
        {
            CommandBuilder.SendCreateEntityMulti(ch);
            CommandBuilder.ClearRecipients();
        }
    }

    public void SendAddEntityAroundCharacterWithEvent(ref Entity entity, WorldObject ch, CreateEntityEventType eventType, Position pos)
    {
        PrepareAddEntityAroundCharacter(ref entity, ch);

        if (CommandBuilder.HasRecipients())
        {
            CommandBuilder.SendCreateEntityWithEventMulti(ch, eventType, pos);
            CommandBuilder.ClearRecipients();
        }
    }

    public void ActivatePlayerAndNotifyNearby(Player p)
    {
        var ch = p.Character;
        var entities = EntityListPool.Get();

        if (ch.IsActive)
            ServerLogger.LogWarning($"Attempting to call ActivatePlayerAndNotifyNearby on player {p.Name} but they are already active!");
        ch.IsActive = true; //activate
        ch.IsImportant = true;

        CommandBuilder.ClearRecipients();

        foreach (Chunk chunk in GetChunkEnumeratorAroundPosition(ch.Position, ServerConfig.MaxViewDistance))
        {
            foreach (var nearbyEntity in chunk.AllEntities)
            {
                var nearbyCharacter = nearbyEntity.Get<WorldObject>();
                if (!nearbyCharacter.IsActive)
                    continue;
                if (!nearbyCharacter.Position.InRange(ch.Position, ServerConfig.MaxViewDistance))
                    continue;

                //if a nearby character is a player, we should track their visibility ourselves
                if (nearbyEntity.Type == EntityType.Player)
                {
                    if (p.Entity == nearbyEntity || !nearbyCharacter.Hidden)
                    {
                        AddPlayerVisibility(nearbyCharacter, ch); //We see nearbyCharacter
                        CommandBuilder.AddRecipient(nearbyEntity);
                    }
                }

                //code for notifying this player of other existing entities
                if (!ch.Hidden && ch != nearbyCharacter)
                {
                    AddPlayerVisibility(ch, nearbyCharacter); //nearbyCharacter sees us
                    if (!nearbyCharacter.Hidden)
                        entities.Add(nearbyEntity); //we only want to notify of this character's existence if it is not hidden
                }
            }

            foreach (var item in chunk.GroundItems)
            {
                var pos = (Position)item.Position;
                if (!pos.InRange(ch.Position, ServerConfig.MaxViewDistance))
                    continue;
                CommandBuilder.RevealDropItemForPlayer(item, false, p);
            }
        }

        //first notify all nearby players of this player's activation
        if (CommandBuilder.HasRecipients())
        {
            CommandBuilder.SendCreateEntityMulti(ch);
            CommandBuilder.ClearRecipients();
        }

        //now add all the nearby entities to the player in question
        foreach (var e in entities)
            CommandBuilder.SendCreateEntity(e.Get<WorldObject>(), p);



        EntityListPool.Return(entities);

        RegisterImportantEntity(ch);
        CommandBuilder.SendAllMapImportantEntities(p, MapImportantEntities);

        if (ch.Hidden)
            CommandBuilder.SendAdminHideStatus(ch.Player, ch.Hidden);
    }

    private void SendRemoveEntityAroundCharacter(ref Entity entity, WorldObject ch, CharacterRemovalReason reason)
    {
        if (ch.TryGetVisiblePlayerList(out var list))
        {
            CommandBuilder.AddRecipients(list);
            CommandBuilder.SendRemoveEntityMulti(ch, reason);
            CommandBuilder.ClearRecipients();
        }

        if (ch.Type != CharacterType.Player || ch.Hidden)
            return;

        //players must have themselves removed from visibility from nearby entities

        foreach (Chunk chunk in GetChunkEnumeratorAroundPosition(ch.Position, ServerConfig.MaxViewDistance))
        {

            foreach (var target in chunk.AllEntities)
            {
                if (!target.IsAlive())
                    ServerLogger.LogError(@"Whoa! Why is a dead entity in the chunk list?");
                var targetCharacter = target.Get<WorldObject>();
                if (!targetCharacter.IsActive)
                    continue;
                if (targetCharacter.Position.InRange(ch.Position, ServerConfig.MaxViewDistance))
                    RemovePlayerVisibility(ch, targetCharacter);
            }
        }
    }

    public void StartMove(ref Entity entity, WorldObject ch)
    {
        if (!entity.IsAlive())
            return;

        if (ch.TryGetVisiblePlayerList(out var list))
        {
            CommandBuilder.AddRecipients(list);
            CommandBuilder.SendStartMoveEntityMulti(ch);
            CommandBuilder.ClearRecipients();
        }
    }

    //public void SendFixedWalkMove(ref Entity entity, WorldObject ch, Position dest, float time)
    //{
    //    if (!entity.IsAlive())
    //        return;

    //    if (ch.TryGetVisiblePlayerList(out var list))
    //    {
    //        CommandBuilder.AddRecipients(list);
    //        CommandBuilder.SendMoveToFixedPositionMulti(ch, dest, time);
    //        CommandBuilder.ClearRecipients();
    //    }
    //}

    public void SendAllEntitiesToPlayer(ref Entity target)
    {
        var playerChar = target.Get<WorldObject>();
        var playerObj = target.Get<Player>();

        foreach (Chunk c in GetChunkEnumeratorAroundPosition(playerChar.Position, ServerConfig.MaxViewDistance))
        {
            //ServerLogger.Log("Sending entities around " + playerChar.Position);
            foreach (var m in c.AllEntities)
            {
                var ch = m.Get<WorldObject>();
                var distance = ch.Position.SquareDistance(playerChar.Position);
#if DEBUG
                if (distance > ServerConfig.MaxViewDistance + ChunkSize)
                    throw new Exception("Unexpected chunk check distance!");
#endif
                if (ch.IsActive && ch.Position.InRange(playerChar.Position, ServerConfig.MaxViewDistance))
                {
                    if (!ch.Hidden || m == target)
                        CommandBuilder.SendCreateEntity(ch, playerObj);
                    AddPlayerVisibility(playerChar, ch);
                }

            }
        }
    }

    public void AddVisiblePlayersAsPacketRecipients(WorldObject character)
    {
        if (character.TryGetVisiblePlayerList(out var list))
        {
            CommandBuilder.AddRecipients(list);
        }

        //foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, ServerConfig.MaxViewDistance))
        //{
        //    foreach (var p in c.Players)
        //    {
        //        var ch = p.Get<WorldObject>();
        //        if (!ch.Position.InRange(character.Position, ServerConfig.MaxViewDistance))
        //            continue;
        //        if (ch.IsActive)
        //            CommandBuilder.AddRecipient(p);
        //    }
        //}
    }

    public bool FindOldestGroundItemInRange(Position center, int range, ref GroundItem itemOut)
    {
        var hasItem = false;
        var lowestId = int.MaxValue;

        foreach (Chunk c in GetChunkEnumeratorAroundPosition(center, range))
        {
            foreach (var item in c.GroundItems)
            {
                var pos = (Position)item.Position;

                if (pos.InRange(center, 9) && item.Id < lowestId)
                {
                    hasItem = true;
                    lowestId = item.Id;
                    itemOut = item;
                }
            }
        }

        return hasItem;
    }

    public bool FindRandomGroundItemInRange(Position center, int range, ref GroundItem itemOut)
    {
        var count = 0;
        const int maxCount = 20;

        Span<GroundItem> items = stackalloc GroundItem[maxCount];

        foreach (Chunk c in GetChunkEnumeratorAroundPosition(center, range))
        {
            foreach (var item in c.GroundItems)
            {
                items[count] = item;
                count++;
                if (count >= maxCount)
                    break;
            }

            if (count >= maxCount)
                break;
        }

        if (count == 0)
        {
            itemOut = default;
            return false;
        }

        itemOut = items[GameRandom.Next(0, count)];
        return true;
    }

    public void GatherMonstersOfTypeInRange(Position position, int distance, EntityList list, MonsterDatabaseInfo monsterType)
    {
        foreach (Chunk c in GetChunkEnumeratorAroundPosition(position, distance))
        {
            foreach (var m in c.Monsters)
            {
                var ch = m.Get<WorldObject>();
                if (!ch.IsActive)
                    continue;

                var monster = m.Get<Monster>();

                if (monster.MonsterBase != monsterType)
                    continue;

                if (position.InRange(ch.Position, distance))
                    list.Add(m);
            }
        }
    }


    public bool HasMonsterOfTypeInRange(Position position, int distance, MonsterDatabaseInfo monsterType)
    {
        foreach (Chunk c in GetChunkEnumeratorAroundPosition(position, distance))
        {
            foreach (var m in c.Monsters)
            {
                var ch = m.Get<WorldObject>();
                if (!ch.IsActive)
                    continue;

                var monster = m.Get<Monster>();

                if (monster.MonsterBase != monsterType)
                    continue;

                if (position.InRange(ch.Position, distance))
                    return true;
            }
        }

        return false;
    }


    public bool HasAllyInRange(WorldObject character, int distance, bool checkLineOfSight, bool checkImmunity = false)
    {
        foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, distance))
        {
            foreach (var m in c.AllEntities)
            {
                var ch = m.Get<WorldObject>();
                if (!ch.IsActive)
                    continue;

                if (m == character.Entity)
                    continue;

                if (ch.Type == CharacterType.NPC)
                    continue;

                if (checkImmunity && ch.IsTargetImmune)
                    continue;

                if (!ch.CombatEntity.IsValidAlly(character.CombatEntity))
                    continue;

                if (character.Position.InRange(ch.Position, distance))
                {
                    if (checkLineOfSight && !WalkData.HasLineOfSight(character.Position, ch.Position))
                        continue;

                    return true;
                }
            }
        }

        return false;
    }

    public bool IsEntityStacked(WorldObject character)
    {
        foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, 0))
        {
            foreach (var m in c.AllEntities)
            {
                var ch = m.Get<WorldObject>();
                if (!ch.IsActive)
                    continue;

                if (m == character.Entity)
                    continue;

                if (ch.Position != character.Position)
                    continue;

                if (ch.Hidden || ch.State == CharacterState.Moving)
                    continue;

                if (ch.Type == CharacterType.NPC && ch.Npc.IsEvent)
                    continue;

                if (ch.IsTargetImmune)
                    continue;

                return true;
            }
        }

        return false;
    }

    public bool IsTileOccupied(Position pos, bool playersOnly = false)
    {
        foreach (Chunk c in GetChunkEnumeratorAroundPosition(pos, 0))
        {
            var list = c.AllEntities;
            if (playersOnly)
                list = c.Players;
            foreach (var m in list)
            {
                var ch = m.Get<WorldObject>();
                if (!ch.IsActive)
                    continue;

                if (ch.Position != pos)
                    continue;

                if (ch.Hidden)
                    continue;

                if (ch.IsTargetImmune)
                    continue;

                if (ch.Type == CharacterType.NPC && ch.Npc.IsEvent)
                    continue;

                return true;
            }
        }

        return false;
    }

    public bool FindUnoccupiedAdjacentTile(Position pos, out Position freeTile, bool playersOnly = false)
    {
        freeTile = pos;
        var xSign = GameRandom.Next(2) == 0 ? -1 : 1;
        var ySign = GameRandom.Next(2) == 0 ? -1 : 1;
        var start = GameRandom.Next(27);

        for (var i = start; i < start + 9; i++) //this is actually crazy but trust me it works
        {
            var x = (i / 3) % 3 - 1;
            var y = (i + 1) % 3 - 1;

            if (x == 0 && y == 0)
                continue;
            var test = new Position(pos.X + x * xSign, pos.Y + y * ySign);
            if (WalkData.IsCellWalkable(test) && !IsTileOccupied(test, playersOnly))
            {
                freeTile = test;
                return true;
            }
        }

        return false;
    }

    public void GatherAlliesInRange(WorldObject character, int distance, EntityList list, bool checkLineOfSight, bool checkImmunity = false)
    {
        foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, distance))
        {

            foreach (var m in c.AllEntities)
            {
                var ch = m.Get<WorldObject>();
                if (!ch.IsActive)
                    continue;

                if (ch.Type == CharacterType.NPC)
                    continue;

                if (checkImmunity && ch.IsTargetImmune)
                    continue;

                if (!ch.CombatEntity.IsValidAlly(character.CombatEntity))
                    continue;

                if (character.Position.InRange(ch.Position, distance))
                {
                    if (checkLineOfSight && !WalkData.HasLineOfSight(character.Position, ch.Position))
                        continue;

                    list.Add(m);
                }
            }

            //foreach (var p in c.Players)
            //{
            //    var ch = p.Get<WorldObject>();
            //    if (!ch.IsActive)
            //        continue;

            //    if (checkImmunity && ch.SpawnImmunity > 0)
            //        continue;

            //    if (character.Position.InRange(ch.Position, distance))
            //        list.Add(p);
            //}
        }
    }

    public void GatherValidTargets(WorldObject character, int distance, int attackRange, EntityList list)
    {
        foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, distance))
        {

            foreach (var m in c.AllEntities)
            {
                var ch = m.Get<WorldObject>();
                if (!ch.IsActive)
                    continue;

                if (ch.Type == CharacterType.NPC || ch.Hidden)
                    continue;

                if (ch.IsTargetImmune)
                    continue;

                if (!ch.CombatEntity.IsValidTarget(character.CombatEntity))
                    continue;

                if (!character.Position.InRange(ch.Position, distance))
                    continue;

                if (!WalkData.HasLineOfSight(character.Position, ch.Position))
                    continue;

                if (character.Position.DistanceTo(ch.Position) > attackRange)
                {
                    if (!Instance.Pathfinder.HasPath(WalkData, character.Position, ch.Position, 1))
                        continue;
                }


                list.Add(m);
            }

        }
    }

    public void GatherMonstersInArea(Position center, int distance, EntityList list)
    {
        foreach (Chunk c in GetChunkEnumeratorAroundPosition(center, distance))
        {

            foreach (var m in c.AllEntities)
            {
                var ch = m.Get<WorldObject>();
                if (!ch.IsActive)
                    continue;

                if (ch.Type != CharacterType.Monster)
                    continue;

                list.Add(m);
            }
        }
    }

    public void GatherEnemiesInArea(WorldObject character, Position position, int distance, EntityList list, bool checkLineOfSight, bool checkImmunity = false)
    {
        foreach (Chunk c in GetChunkEnumeratorAroundPosition(position, distance))
        {

            foreach (var m in c.AllEntities)
            {
                var potentialTarget = m.Get<WorldObject>();
                if (!potentialTarget.IsActive)
                    continue;

                if (potentialTarget.Type == CharacterType.NPC)
                    continue;

                if (checkImmunity && potentialTarget.IsTargetImmune)
                    continue;

                if (!potentialTarget.CombatEntity.IsValidTarget(character.CombatEntity))
                    continue;

                if (position.InRange(potentialTarget.Position, distance))
                {
                    if (checkLineOfSight)
                    {
                        if (!WalkData.HasLineOfSight(character.Position, potentialTarget.Position))

                            continue;
                    }

                    list.Add(m);
                }
            }
        }
    }

    public void GatherEnemiesInRange(WorldObject character, int distance, EntityList list, bool checkLineOfSight, bool checkImmunity = false)
    {
        GatherEnemiesInArea(character, character.Position, distance, list, checkLineOfSight, checkImmunity);
        return;
        //foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, distance))
        //{

        //    foreach (var m in c.AllEntities)
        //    {
        //        var ch = m.Get<WorldObject>();
        //        if (!ch.IsActive)
        //            continue;

        //        if (ch.Type == CharacterType.NPC)
        //            continue;

        //        if (checkImmunity && ch.IsTargetImmune)
        //            continue;

        //        if (!ch.CombatEntity.IsValidTarget(character.CombatEntity))
        //            continue;

        //        if (character.Position.InRange(ch.Position, distance))
        //        {
        //            if (checkLineOfSight)
        //            {
        //                if (!WalkData.HasLineOfSight(character.Position, ch.Position))

        //                        continue;
        //            }

        //            list.Add(m);
        //        }
        //    }
        //}
    }

    public bool QuickCheckPlayersNearby(WorldObject character, int distance)
    {
        if (PlayerCount == 0) return false;

        foreach (Chunk c in GetChunkEnumeratorAroundPosition(character.Position, distance))
        {
            if (c.Players.Count > 0)
                return true;
        }

        return false;
    }

    public void SendVisualEffectToPlayers(int effectId, Position pos, int facing)
    {
        using var targetList = EntityListPool.Get();
        GatherAllPlayersInViewDistance(pos, targetList);
        CommandBuilder.AddRecipients(targetList);
        CommandBuilder.SendEffectAtLocationMulti(effectId, pos, facing);
        CommandBuilder.ClearRecipients();
    }

    public int GatherAllPlayersInViewDistance(Position position, EntityList list) => GatherPlayersInRange(position, ServerConfig.MaxViewDistance+2, list, false,false);

    public int GatherPlayersInRange(Position position, int distance, EntityList? list, bool checkLineOfSight, bool checkImmunity = false)
    {
        var hasList = list != null;
        var count = 0;

        foreach (Chunk c in GetChunkEnumeratorAroundPosition(position, ServerConfig.MaxViewDistance))
        {
            foreach (var p in c.Players)
            {
                var ch = p.Get<WorldObject>();
                if (!ch.IsActive)
                    continue;

                if (checkImmunity)
                {
                    if (ch.IsTargetImmune || ch.State == CharacterState.Dead)
                        continue;
                }

                if (position.InRange(ch.Position, distance))
                {
                    if (checkLineOfSight && !WalkData.HasLineOfSight(position, ch.Position))
                        continue;
                    if (hasList)
                        list!.Add(p);
                    count++;
                }
            }
        }

        return count;
    }

    public void TriggerAreaOfEffectForCharacter(WorldObject character, Position initialPos, Position targetPosition)
    {
        //Since aoes are added to each chunk they affect, we only need to check aoes in our current chunk

        var c = GetChunkForPosition(character.Position);

        for (var i = 0; i < c.AreaOfEffects.Count; i++)
        {
            var aoe = c.AreaOfEffects[i];
            if (!aoe.IsActive)
                continue;

            if (aoe.HasTouchedAoE(initialPos, targetPosition))
                aoe.OnAoETouch(character);
        }
    }

    public Position GetRandomVisiblePositionInArea(Position center, int minDistance, int distance, int checksPerDistance = 10)
    {
        for (var i = 0; i < distance; i++)
        {
            var d = distance - i;
            var md = minDistance - i;
            if (md < 0)
                md = 0;

            var tile = GetRandomWalkablePositionInAreaWithMinDistance(Area.CreateAroundPoint(center, d), md, 20);
            if (WalkData.IsCellWalkable(tile) && WalkData.HasLineOfSight(center, tile))
                return tile;
        }

        return center;
    }


    public Position GetRandomWalkablePositionInAreaWithMinDistance(Area area, int minDistanceFromMid, int tries = 100)
    {
        var p = area.RandomInArea();
        if (WalkData.IsCellWalkable(p))
            return p;

        var c = area.Center;

        var attempt = 0;
        do
        {
            attempt++;
            if (attempt > tries)
                return Position.Invalid;

            p = area.RandomInArea();
        } while (!WalkData.IsCellWalkable(p) && c.DistanceTo(p) < minDistanceFromMid);

        return p;
    }

    public Position GetRandomWalkablePositionInArea(Area area, int tries = 100)
    {
        var p = area.RandomInArea();
        if (WalkData.IsCellWalkable(p))
            return p;

        var attempt = 0;
        do
        {
            attempt++;
            if (attempt > tries)
                return Position.Invalid;

            p = area.RandomInArea();
        } while (!WalkData.IsCellWalkable(p));

        return p;
    }

    private void ClearInactive(int id)
    {
        PlayerCount -= Chunks[id].Players.ClearInactive();
        Chunks[id].Monsters.ClearInactive();
        Chunks[id].AllEntities.ClearInactive();
        ClearExpiredDropsForChunk(id);
    }

    private void ClearExpiredDropsForChunk(int id)
    {
        var drops = Chunks[id].GroundItems;
        for (var i = 0; i < drops.Count; i++)
        {
            var drop = drops[i];
            if (drop.Expiration < Time.ElapsedTimeFloat)
            {
                using var entities = EntityListPool.Get();
                GatherAllPlayersInViewDistance(drop.Position, entities);
                CommandBuilder.AddRecipients(entities);
                CommandBuilder.PickUpOrRemoveItemMulti(null, drop);
                CommandBuilder.ClearRecipients();

                ItemChunkLookup.Remove(drop.Id);
                drops.SwapFromBack(i);
                i--;
            }
        }
    }
    
    private void LoadNpcs()
    {
        if (!DataManager.NpcManager.NpcSpawnsForMaps.TryGetValue(Name, out var spawns))
            return;

        for (var i = 0; i < spawns.Count; i++)
        {
            var spawn = spawns[i];

            World.CreateNpc(this, spawn);
        }
    }

    public void UpdateImportantEntity(WorldObject character)
    {
        CommandBuilder.AddRecipients(Players);
        CommandBuilder.SendUpdateMapImportantEntityMulti(character);
        CommandBuilder.ClearRecipients();
    }

    public void RegisterImportantEntity(WorldObject character)
    {
        var e = character.Entity;
        if(!MapImportantEntities.Contains(ref e))
            MapImportantEntities.Add(ref e);

        if (Players.Count <= 0)
            return;

        CommandBuilder.AddRecipients(Players);
        CommandBuilder.SendUpdateMapImportantEntityMulti(character);
        CommandBuilder.ClearRecipients();
    }

    public void RemoveImportantEntity(WorldObject character)
    {
        var e = character.Entity;
        MapImportantEntities.Remove(ref e);

        if (Players.Count <= 0)
            return;

        CommandBuilder.AddRecipients(Players);
        CommandBuilder.SendRemoveMapImportantEntityMulti(character);
        CommandBuilder.ClearRecipients();
    }

    public void CreateAreaOfEffect(AreaOfEffect aoe)
    {
        var chunkEntities = EntityListPool.Get();
        //add the aoe to every chunk touched by the aoe
        foreach (var chunk in GetChunkEnumerator(GetChunksForArea(aoe.Area)))
        {
            chunk.AreaOfEffects.Add(aoe);
            //since our interactions can cause entities to get added or removed from this chunk
            //we want an enumerator that will not change
            chunkEntities.CopyEntities(chunk.AllEntities); 

            foreach (var e in chunkEntities)
            {
                if(e.TryGet<CombatEntity>(out var ce) && aoe.Area.Contains(ce.Character.Position))
                    aoe.OnAoETouch(ce.Character);
            }
        }

        EntityListPool.Return(chunkEntities);
    }

    public void RemoveAreaOfEffect(AreaOfEffect aoe)
    {
        foreach (var chunk in GetChunkEnumerator(GetChunksForArea(aoe.Area)))
        {
            chunk.AreaOfEffects.Remove(aoe);
        }
    }

    public void DropGroundItem(ref GroundItem item)
    {
        var chunk = GetChunkForPosition(item.Position);
        chunk.AddGroundItem(ref item);
        ItemChunkLookup.Add(item.Id, chunk.Id);

        using var entityList = EntityListPool.Get();
        GatherAllPlayersInViewDistance(item.Position, entityList);
        CommandBuilder.AddRecipients(entityList);
        CommandBuilder.DropItemMulti(item, true);
        CommandBuilder.ClearRecipients();
    }

    public bool PickUpOrRemoveItem(WorldObject? pickerUpper, int groundId)
    {
        if (!ItemChunkLookup.TryGetValue(groundId, out var chunkId))
            return false;

        if(!Chunks[chunkId].TryGetGroundItem(groundId, out var item))
            return false;

        using var entityList = EntityListPool.Get();
        GatherAllPlayersInViewDistance(item.Position, entityList);
        CommandBuilder.AddRecipients(entityList);
        CommandBuilder.PickUpOrRemoveItemMulti(pickerUpper, item);
        CommandBuilder.ClearRecipients();

        Chunks[chunkId].RemoveGroundItem(groundId);
        ItemChunkLookup.Remove(groundId);

        return true;
    }

    public bool TryGetGroundItemByDropId(int groundId, out GroundItem item)
    {
        if (!ItemChunkLookup.TryGetValue(groundId, out var chunkId))
        {
            item = default;
            return false;
        }

        if (!Chunks[chunkId].TryGetGroundItem(groundId, out item))
            return false;

        return true;
    }


    private void LoadMapConfig()
    {
        if (!DataManager.MapConfigs.TryGetValue(Name, out var action))
            return;

        ServerLogger.LogVerbose("Loading map config for map " + Name);
        MapConfig = new ServerMapConfig(this);

        action(MapConfig);

        MapConfig.ApplySpawnsToMap();
    }

    public void RemoveEntity(ref Entity entity, CharacterRemovalReason reason, bool removeFromInstance)
    {
        if (!entity.IsAlive())
            return;

        var ch = entity.Get<WorldObject>();

        //if (!ch.Hidden)
        if (ch.Type != CharacterType.NPC || !ch.Hidden)
            SendRemoveEntityAroundCharacter(ref entity, ch, reason);
        ch.ClearVisiblePlayerList();

        var hasRemoved = false;

        var charChunk = GetChunkForPosition(ch.Position);
        var removal = charChunk.RemoveEntity(ref entity, ch.Type);

        if (!removal && ch.State != CharacterState.Dead) //monster could be waiting respawn, it's not that weird to not be on a chunk if dead
        {
            ServerLogger.LogWarning($"Attempting to remove entity {entity} from map, but it appears to already be gone.");
        }

        //Entities.Remove(ref entity);
        //removeList.Add(entity);

        //ch.Map = null;

        if (removeFromInstance)
            Instance.RemoveEntity(ref entity);

        if (hasRemoved)
            entityCount--;

        if (entity.Type == EntityType.Player)
        {
            Debug.Assert(Players.Contains(ref entity));
            Players.Remove(ref entity);
            PlayerCount--;
            ServerLogger.Debug($"Map {Name} changed player count to {PlayerCount}.");
        }

        if(ch.IsImportant)
            RemoveImportantEntity(ch);
    }

    //private void RunEntityUpdates()
    //{
    //    foreach (var e in Entities)
    //    {
    //        if (!e.IsAlive())
    //            continue;

    //        e.Get<WorldObject>().Update();
    //    }

    //    if (removeList.Count == 0)
    //        return;

    //    for(var i = 0; i < removeList.Count; i++)
    //    {
    //        var e = removeList[i];
    //        Entities.Remove(ref e);
    //    }

    //    removeList.Clear();
    //}

    public void Update()
    {
        //RunEntityUpdates();
        ClearInactive(chunkCheckId);

        //#if DEBUG
        //        if (PlayerCount < 0)
        //            throw new Exception("Player count has become an impossible value!");

        chunkCheckId++;
        if (chunkCheckId >= chunkWidth * chunkHeight)
        {
            chunkCheckId = 0;
            Players.ClearInactive(); //should never happen
            MapImportantEntities.ClearInactive(); //but why risk it?
        }
        
        foreach (var c in Chunks)
        {
            for(var i = 0; i < c.AreaOfEffects.Count; i++)
            {
                var a = c.AreaOfEffects[i];
                if (a.CheckStayTouching)
                {
                    a.Update();
                    if (!a.IsActive)
                        i--; //we've ended this aoe so step back on the iterator and continue. Probably dangerous.
                }

            }
        }

#if DEBUG
            //sanity checks
            if (chunkCheckId == 0 && PlayerCount == 0)
        {
            foreach (var c in Chunks)
            {
                foreach (var e in c.AllEntities)
                {
                    if (!e.TryGet<WorldObject>(out var obj))
                        continue;

                    if (obj.TryGetVisiblePlayerList(out var list))
                    {
                        if (list.Count > 0)
                            throw new Exception(
                                $"Map {Name} has no players, but the entity {obj.Name} thinks it can still see a player.");
                    }
                }

                foreach (var aoe in c.AreaOfEffects)
                {
                    if (!aoe.IsActive)
                        throw new Exception(
                            $"Oh snap! An inactive aoe is still attached to the chunk {c} on map {Name}.");
                }
            }
        }
#endif
    }
    
    public ChunkAreaEnumerator GetChunkEnumeratorAroundPosition(Position p, int distance)
    {
        var area = Area.CreateAroundPoint(p, distance).ClipArea(MapBounds);
        var area2 = GetChunksForArea(area);
        return new ChunkAreaEnumerator(Chunks, chunkWidth, area2);
    }

    public ChunkAreaEnumerator GetChunkEnumerator(Area area)
    {
        return new ChunkAreaEnumerator(Chunks, chunkWidth, area);
    }

    private int AlignValue(int value, int alignment)
    {
        var remainder = value % alignment;
        if (remainder == 0)
            return value;
        return value - remainder + alignment;
    }

    public Chunk GetChunkForPosition(Position pos)
    {
        return Chunks[(pos.X / ChunkSize) + (pos.Y / ChunkSize) * chunkWidth];
    }

    public Area GetChunksForArea(Area area)
    {
        return new Area(area.MinX / ChunkSize, area.MinY / ChunkSize, area.MaxX / ChunkSize, area.MaxY / ChunkSize);
    }

    //it's weird but it's how official servers find a free position
    public bool FindPositionUsing9Slice(Area area, out Position p)
    {
        p = area.Center;
        var mapBounds = MapBounds.Shrink(4, 4);
        var xr = area.Width / 2;
        var yr = area.Height / 2;
        if (xr < 1) xr = 1;
        if (yr < 1) yr = 1;

        var xDir = (GameRandom.Next(0, 20000) % 2) == 1 ? 1 : -1;
        var yDir = (GameRandom.Next(0, 20000) % 2) == 1 ? 1 : -1;

        for (var i = -1; i < 2; i++)
        {
            for (var j = -1; j < 2; j++)
            {
                var sx = p.X + i * (GameRandom.Next(0, 20000) % xr) * xDir;
                var sy = p.Y + i * (GameRandom.Next(0, 20000) % yr) * yDir;
                var target = new Position(sx, sy);

                if (mapBounds.Contains(target) && WalkData.IsCellWalkable(target))
                {
                    p = target;
                    return true;
                }
            }
        }

        return false;
    }

    public Position FindRandomPositionOnMap()
    {
        var area = MapBounds.Shrink(5, 5);
        var pos = new Position(20, 20);
        var count = 0;

        do
        {
            pos = area.RandomInArea();
            count++;
        } while (!WalkData.IsCellWalkable(pos) && count < 50);

        return pos;
    }

    public bool FindPositionInRange(Area area, out Position p)
    {
#if DEBUG
        if (area.ClipArea(MapBounds) != area)
        {
            ServerLogger.LogWarning($"Attempting to find position in an area that exceeds map bounds! You should clip the area first.");
        }
#endif

        if (WalkData.FindWalkableCellInArea(area, out p))
            return true;

        ServerLogger.Debug($"Could not find walkable tile in {area} on map {Name} within 100 checks. Falling back to tile scan.");

        if (WalkData.ScanForWalkableCell(area, out p))
            return true;

        ServerLogger.LogWarning($"Attempted to find walkable cell in area {area} on map {Name}, but there are no walkable cells in the zone.");
        p = Position.Invalid;
        return false;
    }

    public void ReloadMapScripts()
    {
        LoadNpcs();
        LoadMapConfig();
    }

    public Map(World world, Instance instance, string name, string walkData)
    {
#if DEBUG
        Players.IsActive = true; //bypass EntityListPool borrow tracking
        MapImportantEntities.IsActive = true;
#endif

        World = world;
        Name = name;
        MapConfig = new ServerMapConfig(this);
        Instance = instance;

        WalkData = new MapWalkData(walkData);

        Width = WalkData.Width;
        Height = WalkData.Height;

        ChunkSize = ServerConfig.OperationConfig.MapChunkSize;

        chunkWidth = AlignValue(Width, ChunkSize) / ChunkSize;
        chunkHeight = AlignValue(Height, ChunkSize) / ChunkSize;

        MapBounds = new Area(0, 0, Width - 1, Height - 1);
        ChunkBounds = new Area(0, 0, chunkWidth - 1, chunkHeight - 1);

        Chunks = new Chunk[chunkWidth * chunkHeight];

        PlayerCount = 0;

        var id = 0;
        var fullUnwalkable = 0;
        for (var x = 0; x < chunkWidth; x++)
        {
            for (var y = 0; y < chunkHeight; y++)
            {
                var walkable = 0;
                for (var x2 = 0; x2 < ChunkSize; x2++)
                {
                    for (var y2 = 0; y2 < ChunkSize; y2++)
                    {
                        if (WalkData.IsCellWalkable(x * ChunkSize + x2, y * ChunkSize + y2))
                            walkable++;
                    }
                }
                Chunks[x + y * chunkWidth] = new Chunk();
                Chunks[x + y * chunkWidth].Id = x + y * chunkWidth;
                Chunks[x + y * chunkWidth].Size = ChunkSize;
                Chunks[x + y * chunkWidth].X = x;
                Chunks[x + y * chunkWidth].Y = y;
                Chunks[x + y * chunkWidth].WalkableTiles = walkable;

                if (walkable == 0)
                    fullUnwalkable++;
                id++;
            }
        }

        LoadNpcs();
        LoadMapConfig();
    }
}