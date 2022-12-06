using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.ObjectPool;
using Microsoft.VisualBasic;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Database;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.Requests;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation;

public class World
{
    public static World Instance = null!;

    public List<Instance> Instances = new();
    private ObjectPool<AreaOfEffect> AoEPool { get; set; }
    
    private Channel<MapMoveRequest> moveRequests;

    private Dictionary<int, Entity> entityList = new();
    //private Dictionary<string, int> mapIdLookup = new();

    private Dictionary<string, int> worldMapInstanceLookup = new();
    private ConcurrentBag<Entity> removeList = new();
    
    private int nextEntityId = 0;
    private int maxEntityId = 10_000_000;

    private int mapCount = 0;
    const int initialEntityCount = 10_000;

    public void TriggerReloadServerScripts() => reloadScriptsFlag = true;
    private bool reloadScriptsFlag = false;

    private readonly object aoeLock = new();

    public World()
    {
        Instance = this;

        EntityManager.Initialize(initialEntityCount);

        foreach (var instanceEntry in DataManager.InstanceList)
        {
            if(!instanceEntry.IsWorldInstance)
                continue;

            var instance = new Instance(this, instanceEntry);
            var id = Instances.Count;
            Instances.Add(instance);
            foreach(var map in instanceEntry.Maps)
                worldMapInstanceLookup.Add(map, id);
        }

        AoEPool = new DefaultObjectPool<AreaOfEffect>(new AreaOfEffectPoolPolicy(), 64);
        moveRequests = Channel.CreateUnbounded<MapMoveRequest>(new UnboundedChannelOptions() {AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false});
    }

    public void FullyRemoveEntity(ref Entity entity, CharacterRemovalReason reason = CharacterRemovalReason.OutOfSight)
    {
        //important that this only happens on the player's map thread or between server updates on the main thread

        removeList.Add(entity);

        var ch = entity.Get<WorldObject>();

        if (ch.Type == CharacterType.Player)
        {
            var player = ch.Player;
            player.EndNpcInteractions();
            player.SaveCharacterToData();
            var req = new SaveCharacterRequest(player.Id, player.Name, ch.Map?.Name, ch.Position, player.CharData, player.SavePosition);
            RoDatabase.EnqueueDbRequest(req);
        }

        if (ch.Type == CharacterType.NPC)
        {
            var npc = ch.Npc;

            if (npc.HasTouch && npc.AreaOfEffect != null)
                ch.Map?.RemoveAreaOfEffect(npc.AreaOfEffect);
            
            if (ch.Map != null && ch.Map.Instance.NpcNameLookup.ContainsKey(npc.FullName))
                ch.Map.Instance.NpcNameLookup.Remove(npc.FullName);
        }

        entityList.Remove(ch.Id);
        ch.Map?.RemoveEntity(ref entity, reason, true);
        ch.IsActive = false;
        ch.Map = null;

        //ServerLogger.Log($"Recycling " + ch.Name);

        EntityManager.Recycle(entity);
    }

    public void Update()
    {
        if (NetworkManager.IsSingleThreadMode)
        {
            for (var i = 0; i < Instances.Count; i++)
                Instances[i].Update();
        }
        else
            Parallel.ForEach(Instances, instance => instance.Update());
            
        PerformMoves();
        PerformRemovals();

        if (reloadScriptsFlag)
        {
            DoScriptReload();
            reloadScriptsFlag = false;
        }
    }

    public AreaOfEffect GetNewAreaOfEffect()
    {
        lock (aoeLock)
        {
            return AoEPool.Get();
        }
    }

    public void ReturnAreaOfEffect(AreaOfEffect aoe)
    {
        lock (aoeLock)
        {
            AoEPool.Return(aoe);
        }
    }

    private void DoScriptReload()
    {
        CommandBuilder.AddAllPlayersAsRecipients();
        CommandBuilder.SendServerMessage("Server is reloading NPC and Monster scripts, things might get a bit spicy!");

        NetworkManager.ExtendTimeoutForAllPlayers(30); //add 30s to player timeout timers. Won't take nearly that long, but better safe.
        UnloadAllNpcsAndMonsters(); //remove all npc/monsters from the maps
        PerformRemovals(); //recycle all returned npc/monster entities
        DataManager.ReloadScripts(); //recompile scripts
        ReloadNpcsAndMonsters(); //re-add npcs and monsters to maps

        CommandBuilder.SendServerMessage("Reload complete! Hopefully things continue to work!");
        CommandBuilder.ClearRecipients();
    }

    private void UnloadAllNpcsAndMonsters()
    {
        //gracefully terminate npc interactions before we kill the npc
        foreach (var player in NetworkManager.Players)
            player.Player?.EndNpcInteractions();

        var ids = new List<int>();

        foreach (var entity in entityList)
        {
            var e = entity.Value;

            if (e.Type == EntityType.Npc || e.Type == EntityType.Monster)
            {
                FullyRemoveEntity(ref e);
                ids.Add(entity.Key);
            }
        }

        foreach(var i in ids)
            entityList.Remove(i);
    }

    private void ReloadNpcsAndMonsters()
    {
        foreach (var instance in Instances)
        {
            instance.ReloadScripts();
        }
    }
    
    public void CreateNpc(Map map, NpcSpawnDefinition spawn)
    {
        var e = EntityManager.New(EntityType.Npc);
        var ch = e.Get<WorldObject>();
        var npc = e.Get<Npc>();
        
        ch.Id = GetNextEntityId();
        ch.IsActive = true;
        ch.ClassId = spawn.SpriteId;
        ch.Entity = e;
        ch.Position = spawn.Position;
        ch.MoveSpeed = 0;
        ch.Type = CharacterType.NPC;
        ch.FacingDirection = spawn.FacingDirection;
        ch.Name = spawn.FullName;
        ch.Init(ref e);
        npc.FullName = spawn.FullName;
        npc.Name = spawn.Name;
        npc.HasInteract = spawn.HasInteract;
        npc.HasTouch = spawn.HasTouch;
        npc.Entity = e;
        npc.Behavior = spawn.Behavior;

        map.AddEntity(ref e);

        map.Instance.NpcNameLookup.TryAdd(spawn.FullName, e);

        if (npc.HasTouch)
        {
            var aoe = new AreaOfEffect()
            {
                Area = Area.CreateAroundPoint(spawn.Position, spawn.Width, spawn.Height),
                Expiration = float.MaxValue,
                IsActive = true,
                NextTick = float.MaxValue,
                SourceEntity = e,
                Type = AoeType.NpcTouch
            };

            map.CreateAreaOfEffect(aoe);
            npc.AreaOfEffect = aoe;
        }

        entityList.Add(ch.Id, e);


        npc.Behavior.Init(npc); //save this for last, the npc might do something silly like hide itself and needs to be on the map
    }

    //event is an invisible npc without a spawn definition intended to be transient
    public Entity CreateEvent(Map map, string eventName, Position pos, int param1, int param2, int param3, int param4, string? paramString)
    {
        var e = EntityManager.New(EntityType.Npc);
        var ch = e.Get<WorldObject>();
        var npc = e.Get<Npc>();

        if (!DataManager.NpcManager.EventBehaviorLookup.TryGetValue(eventName, out var behavior))
            throw new Exception($"Unable to create event \"{eventName}\" as the matching script could not be found.");

        ch.Id = GetNextEntityId();
        ch.IsActive = false; //it shouldn't see or interact with anyone
        ch.ClassId = 0;
        ch.Entity = e;
        ch.Position = pos;
        ch.MoveSpeed = 0;
        ch.Type = CharacterType.NPC;
        ch.Name = eventName;
        ch.Init(ref e);
        npc.FullName = eventName;
        npc.Name = eventName;
        npc.HasInteract = false;
        npc.HasTouch = false;
        npc.Entity = e;
        npc.Behavior = behavior;

        map.AddEntity(ref e);

        npc.Behavior.InitEvent(npc, param1, param2, param3, param4, paramString);
        
        return e;
    }
    
    public Entity CreatePlayer(NetworkConnection connection, string mapName, Area spawnArea)
    {
        var e = EntityManager.New(EntityType.Player);
        var ch = e.Get<WorldObject>();
        var player = e.Get<Player>();
        var ce = e.Get<CombatEntity>();
        
        if (!TryGetWorldMapByName(mapName, out var map) || map == null)
            throw new Exception($"Could not create player on world map '{mapName}' as it could not be found.");
        
        if (spawnArea.IsZero)
            spawnArea = map.MapBounds;

        Position p;

        if (spawnArea.Width > 1 || spawnArea.Height > 1)
        {
            p = spawnArea.RandomInArea();

            //Position p = new Position(170 + GameRandom.Next(-5, 5), 365 + GameRandom.Next(-5, 5));
            var attempt = 0;
            do
            {
                attempt++;
                if (attempt > 100)
                {
                    ServerLogger.LogWarning("Trouble spawning player, will place him on a random cell instead.");
                    spawnArea = map.MapBounds;
                    attempt = 0;
                }

                p = spawnArea.RandomInArea();
            } while (!map.WalkData.IsCellWalkable(p));
        }
        else
            p = new Position(spawnArea.MidX, spawnArea.MidY);

        ch.Id = GetNextEntityId();
        ch.IsActive = false; //start off inactive

        ch.Entity = e;
        ch.Position = p;
        ch.ClassId = 0; // GameRandom.Next(0, 6);
        ch.MoveSpeed = 0.15f;
        ch.Type = CharacterType.Player;
        ch.FacingDirection = (Direction)GameRandom.NextInclusive(0, 7);
        ch.Init(ref e);

        ce.Init(ref e, ch);

        ce.SetStat(CharacterStat.Level, 1);

        player.Connection = connection;
        player.Entity = e;
        player.CombatEntity = ce;
        player.Character = ch;
        
        if (connection.LoadCharacterRequest != null && connection.LoadCharacterRequest.HasCharacter)
        {
            player.Name = connection.LoadCharacterRequest.Name;
            player.Id = connection.LoadCharacterRequest.Id;
            player.SavePosition = connection.LoadCharacterRequest.SavePosition;
            var data = connection.LoadCharacterRequest.Data;

            if (data != null)
            {
                if(data.Length == player.CharData.Length * 4)
                    Buffer.BlockCopy(data, 0, player.CharData, 0, data.Length);
                else
                    ServerLogger.LogWarning($"Player '{player.Name}' character data does not match the expected size. Player will be loaded with default data.");
            }

            player.ApplyDataToCharacter();
        }

        player.Init();

        if(ce.GetStat(CharacterStat.Hp) <= 0)
            ce.FullRecovery(true, true);

        ch.Name = player.Name;
        connection.LoadCharacterRequest = null;
        connection.Player = player;
        
        entityList.Add(ch.Id, e);
        //player.IsMale = false;

        //player.IsMale = true;
        //ch.ClassId = 1;
        //player.HeadId = 15;

        //map.SendAllEntitiesToPlayer(ref e);
        //map.AddEntity(ref e);

        moveRequests.Writer.TryWrite(new MapMoveRequest(e, MoveRequestType.InitialSpawn, null, map, p));
        
        return e;
    }

    public Entity CreateMonster(Map map, MonsterDatabaseInfo monsterDef, Area spawnArea, MapSpawnRule? spawnRule)
    {
        var e = EntityManager.New(EntityType.Monster);
        var ch = e.Get<WorldObject>();
        var ce = e.Get<CombatEntity>();
        var m = e.Get<Monster>();

        spawnArea.ClipArea(map.MapBounds);
        
        Position p;
        if (!spawnArea.IsZero && spawnArea.Size <= 1)
        {
            p = new Position(spawnArea.MinX, spawnArea.MinY);
        }
        else
        {
            if (spawnArea.IsZero)
                spawnArea = map.MapBounds;

            if (!map.FindPositionInRange(spawnArea, out p))
            {
                ServerLogger.LogWarning($"Failed to spawn {monsterDef.Name} on map {map.Name}, could not find spawn location around {spawnArea}. Spawning randomly on map.");
                map.FindPositionInRange(map.MapBounds, out p);
            }
        }

        ch.Id = GetNextEntityId();

        ch.IsActive = true;
        ch.ClassId = monsterDef.Id;
        ch.Entity = e;
        ch.Position = p;
        ch.MoveSpeed = monsterDef.MoveSpeed;
        ch.Type = CharacterType.Monster;
        ch.FacingDirection = (Direction)GameRandom.NextInclusive(0, 7);
        ch.Init(ref e);

        ce.Entity = e;

        entityList.Add(ch.Id, e);

        ce.Init(ref e, ch);
        m.Initialize(ref e, ch, ce, monsterDef, monsterDef.AiType, spawnRule, map.Name);
        
        map.AddEntity(ref e);

        return e;
    }


    public bool RespawnMonster(Monster monster)
    {
        if (monster.Character.IsActive)
            return false;

        var spawnEntry = monster.SpawnRule;
        if (spawnEntry == null)
            throw new Exception("Cannot respawn monster that has no spawn rule!");

        var map = monster.Character.Map;

        var area = map.MapBounds;
        if (spawnEntry.HasSpawnZone)
            area = spawnEntry.SpawnArea;

        area.ClipArea(map.MapBounds);

        Position p;
        if (area.Width == 1 && area.Height == 1 && area.MinX != 0 && area.MinY != 0)
        {
            p = new Position(area.MinX, area.MinY);
        }
        else
        {
            if (!map.FindPositionInRange(area, out p))
            {
                return false;
            }
        }

        var ch = monster.Character;
        var ce = monster.CombatEntity;
        var e = monster.Entity;

        ch.IsActive = true;
        ch.Map = map;
        ch.Position = p;
        ch.AttackCooldown = 0f;
        ch.State = CharacterState.Idle;

        ce.Init(ref e, ch);
        monster.Initialize(ref e, ch, ce, monster.MonsterBase, monster.MonsterBase.AiType, spawnEntry, map.Name);
        
        map.AddEntity(ref e, false);

        return true;
    }


    public void PerformRemovals()
    {
        for (var i = 0; i < removeList.Count; i++)
        {
            if (!removeList.TryPeek(out var entity))
                throw new Exception("removeList collection blocked while world performing entity removal!");
            
            if (entity.IsNull() || !entity.IsAlive())
                return;

            ServerLogger.Debug($"Removing entity {entity} from world.");

            EntityManager.Recycle(entity);
        }

        removeList.Clear();
    }


    public bool TryGetWorldMapByName(string mapName, out Map? map)
    {
        if (worldMapInstanceLookup.TryGetValue(mapName, out var instanceId))
        {
            if (Instances[instanceId].MapNameLookup.TryGetValue(mapName, out map))
                return true;
        }

        map = null;
        return false;
    }

    public void PerformMoves()
    {
        while (moveRequests.Reader.TryRead(out var move))
        {
            if (!move.Player.IsAlive())
                continue;
            
            var character = move.Player.Get<WorldObject>();

            if (character.Map != move.SrcMap)
                continue; //player is no longer on the originating map

            ServerLogger.Log($"Performing move on player {character.Name} to map {move.DestMap.Name}.");

            if (character.Map != null)
            {
                character.Map.RemoveEntity(ref move.Player, CharacterRemovalReason.OutOfSight, character.Map.Instance != move.DestMap.Instance);
            }
            
            character.ResetState();
            character.Position = move.Position;
            
            move.DestMap.AddEntity(ref move.Player, character.Map?.Instance != move.DestMap.Instance);

            var player = character.Player;
            player.Connection.LastKeepAlive = Time.ElapsedTime; //reset tick time so they get 2 mins to load the map

            if (move.MoveRequestType == MoveRequestType.InitialSpawn)
                CommandBuilder.InformEnterServer(character, player);

            if (move.MoveRequestType == MoveRequestType.MapMove)
                CommandBuilder.SendChangeMap(character, player);
        }
    }

    public void MovePlayerWorldMap(ref Entity entity, WorldObject character, string mapName, Position newPosition)
    {
        if (!TryGetWorldMapByName(mapName, out var map) || map == null)
        {
            ServerLogger.LogWarning($"Map {mapName} does not exist! Could not move player.");
            return;
        }
        
        MovePlayerMap(ref entity, character, map, newPosition);
    }

    public void MovePlayerMap(ref Entity entity, WorldObject character, Map map, Position newPosition)
    {
        moveRequests.Writer.TryWrite(new MapMoveRequest(entity, MoveRequestType.MapMove, character.Map, map, newPosition));

        character.IsActive = false;
        //if (character.Map != null)
        //    character.Map.RemoveEntity(ref entity, CharacterRemovalReason.OutOfSight, character.Map.Instance != map.Instance);
        
        //character.ResetState();
        //character.Position = newPosition;
        
        //if (newPosition == Position.Zero)
        //{
        //    map.FindPositionInRange(map.MapBounds, out var p);
        //    character.Position = newPosition;
        //}
        
        //map.AddEntity(ref entity, character.Map?.Instance != map.Instance);

        //var player = entity.Get<Player>();
        //player.Connection.LastKeepAlive = Time.ElapsedTime; //reset tick time so they get 2 mins to load the map

        //CommandBuilder.SendChangeMap(character, player);
    }


    public Entity GetEntityById(int id)
    {
        if (entityList.TryGetValue(id, out var entity))
            return entity;

        return Entity.Null;
    }

    private int GetNextEntityId()
    {
        nextEntityId++;
        if (nextEntityId > maxEntityId)
            nextEntityId = 0;

        if (!entityList.ContainsKey(nextEntityId))
            return nextEntityId;

        while (entityList.ContainsKey(nextEntityId))
            nextEntityId++;

        return nextEntityId;
    }
}