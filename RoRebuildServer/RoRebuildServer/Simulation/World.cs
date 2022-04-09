using System.Threading.Channels;
using Microsoft.Extensions.ObjectPool;
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
    public ObjectPool<AreaOfEffect> AoEPool;

    private Channel<MapMoveRequest> moveRequests;

    private Dictionary<int, Entity> entityList = new();
    //private Dictionary<string, int> mapIdLookup = new();

    private Dictionary<string, int> worldMapInstanceLookup = new();

    private List<Entity> removeList = new(30);
    
    private int nextEntityId = 0;
    private int maxEntityId = 10_000_000;

    private int mapCount = 0;
    const int initialEntityCount = 10_000;

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
        removeList.Add(entity);

        var ch = entity.Get<WorldObject>();

        if (ch.Type == CharacterType.Player)
        {
            var player = ch.Player;
            player.SaveCharacterToData();
            var req = new SaveCharacterRequest(player.Id, player.Name, ch.Map?.Name, ch.Position, player.CharData);
            RoDatabase.EnqueueDbRequest(req);
        }

        entityList.Remove(ch.Id);
        ch.Map?.RemoveEntity(ref entity, reason, true);
        ch.Map?.Instance.RemoveEntity(ref entity);
        ch.IsActive = false;
        ch.Map = null;

        EntityManager.Recycle(entity);
    }

    public void Update()
    {
        //for(var i = 0; i < Instances.Count; i++)
        //    Instances[i].Update();

        Parallel.ForEach(Instances, instance => instance.Update());
            
        PerformMoves();
        PerformRemovals();
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
        ch.Init(ref e);
        npc.Name = spawn.Name;
        npc.HasInteract = spawn.HasInteract;
        npc.HasTouch = spawn.HasTouch;
        npc.Entity = e;
        npc.Behavior = spawn.Behavior;

        map.AddEntity(ref e);

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

            map.CreateAreaOfEffect(aoe, ch.Position);
        }
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
        ch.FacingDirection = (Direction)GameRandom.Next(0, 7);
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
            var data = connection.LoadCharacterRequest.Data;

            if (data != null)
                Buffer.BlockCopy(data, 0, player.CharData, 0, data.Length);

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

    public void CreateMonster(Map map, MonsterDatabaseInfo monsterDef, Area spawnArea, MapSpawnRule spawnRule)
    {
        var e = EntityManager.New(EntityType.Monster);
        var ch = e.Get<WorldObject>();
        var ce = e.Get<CombatEntity>();
        var m = e.Get<Monster>();
        
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
        ch.FacingDirection = (Direction)GameRandom.Next(0, 7);
        ch.Init(ref e);

        ce.Entity = e;

        entityList.Add(ch.Id, e);

        ce.Init(ref e, ch);
        m.Initialize(ref e, ch, ce, monsterDef, monsterDef.AiType, spawnRule, map.Name);
        
        map.AddEntity(ref e);
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
            var entity = removeList[i];

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