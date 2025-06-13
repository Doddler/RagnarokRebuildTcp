using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Extensions.ObjectPool;
using Microsoft.VisualBasic;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Database;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.Requests;
using RoRebuildServer.Database.Utility;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Server;
using RoRebuildServer.Simulation.Parties;
using RoRebuildServer.Simulation.Util;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RoRebuildServer.Simulation;

public class World
{
    public static World Instance = null!;

    public List<Instance> Instances = new();
    private ObjectPool<AreaOfEffect> AoEPool { get; set; }

    private Channel<MapMoveRequest> moveRequests;

    private Dictionary<int, Entity> entityList = new();
    private Dictionary<string, Entity> playerNameLookup = new();
    private Dictionary<string, Entity> globalSignalNpcs = new();

    private Dictionary<string, int> worldMapInstanceLookup = new();
    private ConcurrentBag<Entity> removeList = new();

    private ConcurrentDictionary<Guid, Entity> playerList = new();
    private ConcurrentDictionary<int, Party> partyLookup = new();

    private int nextEntityId = 0;
    private int maxEntityId = 10_000_000;

    private int mapCount = 0;
    const int initialEntityCount = 10_000;

    private int currentDropIndex = 5000;
    public int GetNextDropId() => Interlocked.Increment(ref currentDropIndex);

    public void TriggerReloadServerScripts() => reloadScriptsFlag = true;
    private bool reloadScriptsFlag = false;

    private readonly object aoeLock = new();
    private readonly Lock entityLock = new();

    public ConcurrentStack<Action> MainThreadActions = new();

    public bool TryFindPartyById(int id, [NotNullWhen(true)] out Party? party) => partyLookup.TryGetValue(id, out party);
    public bool TryAddParty(Party party) => partyLookup.TryAdd(party.PartyId, party);
    public bool RemoveParty(Party party) => partyLookup.TryRemove(party.PartyId, out _);
    public bool TryFindPlayerByGuid(Guid id, out Entity entity) => playerList.TryGetValue(id, out entity) && entity.IsAlive();
    public bool TryFindPlayerByName(string name, out Entity entity) => playerNameLookup.TryGetValue(name, out entity) && entity.IsAlive();
    
    public World()
    {
        Instance = this;

        Time.ResetDiagnosticsTimer();
        EntityManager.Initialize(initialEntityCount);
        AoEPool = new DefaultObjectPool<AreaOfEffect>(new AreaOfEffectPoolPolicy(), 128);

        foreach (var instanceEntry in DataManager.InstanceList)
        {
            if (!instanceEntry.IsWorldInstance)
                continue;

            var instance = new Instance(this, instanceEntry);
            var id = Instances.Count;
            Instances.Add(instance);
            foreach (var map in instanceEntry.Maps)
            {
                if (worldMapInstanceLookup.ContainsKey(map))
                    ServerLogger.LogWarning($"Could not add map '{map}' to instance {instanceEntry.Name} because that map is already assigned to the instance {Instances[worldMapInstanceLookup[map]].Name}");
                worldMapInstanceLookup.Add(map, id);
                mapCount++;
            }
        }

        foreach (var instance in Instances)
            instance.LoadNpcs(); //we defer this till after all the maps are loaded, as some npcs will need to do cross map stuff
        
        moveRequests = Channel.CreateUnbounded<MapMoveRequest>(new UnboundedChannelOptions() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false });

        ServerLogger.Log($"World created using {mapCount} maps across {Instances.Count} instances placing a total of {nextEntityId} entities in {Time.CurrentDiagnosticsTime():F2}s.");
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
            var req = new SaveCharacterRequest(player);
            RoDatabase.EnqueueDbRequest(req);
        }

        if (ch.Type == CharacterType.NPC)
        {
            var npc = ch.Npc;

            npc.RemoveAreaOfEffect();
            npc.RemoveSignal();
            npc.EndAllEvents();

        }

        entityList.Remove(ch.Id);
        ch.Map?.RemoveEntity(ref entity, reason, true);
        ch.IsActive = false;
        ch.Map = null;

        //ServerLogger.Log($"Recycling " + ch.Name);

        //EntityManager.Recycle(entity);
    }

    public void Update()
    {
#if DEBUG
        ZoneWorker.IsMainThread = false;
#endif

        if (NetworkManager.IsSingleThreadMode)
        {
            for (var i = 0; i < Instances.Count; i++)
                Instances[i].Update();
        }
        else
            Parallel.ForEach(Instances, instance => instance.Update());

#if DEBUG
        ZoneWorker.IsMainThread = true;
#endif

        PerformMoves();
        PerformRemovals();

        if (MainThreadActions.Count > 0)
            while (MainThreadActions.TryPop(out var a))
                a();

        if (reloadScriptsFlag)
        {
            DoScriptReload();
            reloadScriptsFlag = false;
        }

        if (currentDropIndex + 2000 > short.MaxValue)
            currentDropIndex = 5000;
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
        CommandBuilder.ClearRecipients(); //if we don't clear it can cause issues

        globalSignalNpcs.Clear();
        NetworkManager.ExtendTimeoutForAllPlayers(30); //add 30s to player timeout timers. Won't take nearly that long, but better safe.
        UnloadAllNpcsAndMonsters(); //remove all npc/monsters from the maps
        PerformRemovals(); //recycle all returned npc/monster entities
        DataManager.ReloadScripts(); //recompile scripts
        ReloadNpcsAndMonsters(); //re-add npcs and monsters to maps

        CommandBuilder.AddAllPlayersAsRecipients();
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
                if (e.IsAlive())
                    FullyRemoveEntity(ref e);
                ids.Add(entity.Key);
            }
        }

        foreach (var i in ids)
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

        ch.IsActive = true;
        ch.ClassId = spawn.SpriteId;
        ch.Entity = e;
        ch.Position = spawn.Position;
        ch.MoveSpeed = 0;
        ch.Type = CharacterType.NPC;
        ch.FacingDirection = spawn.FacingDirection;
        ch.Name = spawn.FullName;
        ch.DisplayType = spawn.DisplayType;
        ch.Init(ref e);
        npc.FullName = spawn.FullName;
        npc.Name = spawn.Name;
        npc.HasInteract = spawn.HasInteract;
        npc.HasTouch = spawn.HasTouch;
        npc.Entity = e;
        npc.Behavior = spawn.Behavior;
        npc.Character = ch;

        using (entityLock.EnterScope())
        {
            ch.Id = GetNextEntityId();
            entityList.Add(ch.Id, e);
        }

        map.AddEntity(ref e);

        if (spawn.DisplayType == CharacterDisplayType.Portal)
            map.RegisterImportantEntity(ch);

        if (npc.HasTouch)
        {
            var aoe = AoEPool.Get();

            aoe.Area = Area.CreateAroundPoint(spawn.Position, spawn.Width, spawn.Height);
            aoe.Expiration = float.MaxValue;
            aoe.IsActive = true;
            aoe.NextTick = float.MaxValue;
            aoe.SourceEntity = e;
            aoe.Type = AoeType.NpcTouch;

            map.CreateAreaOfEffect(aoe);
            npc.AreaOfEffect = aoe;
        }

        if (!string.IsNullOrWhiteSpace(spawn.SignalName))
            npc.RegisterSignal(spawn.SignalName);

        npc.Behavior.Init(npc); //save this for last, the npc might do something silly like hide itself and needs to be on the map
    }

    //event is an invisible npc without a spawn definition intended to be transient
    public Entity CreateEvent(Entity owner, Map map, string eventName, Position pos, int param1, int param2, int param3, int param4, string? paramString)
    {
        var e = EntityManager.New(EntityType.Npc);
        var ch = e.Get<WorldObject>();
        var npc = e.Get<Npc>();

        if (!DataManager.NpcManager.EventBehaviorLookup.TryGetValue(eventName, out var behavior))
            throw new Exception($"Unable to create event \"{eventName}\" as the matching script could not be found.");


        ch.IsActive = true;
        ch.AdminHidden = true;
        ch.ClassId = 0;
        ch.Entity = e;
        ch.Position = pos;
        ch.MoveSpeed = 0;
        ch.Type = CharacterType.NPC;
        ch.Name = eventName;
        ch.Init(ref e);
        npc.FullName = eventName;
        npc.Name = eventName;
        npc.EventType = eventName;
        npc.HasInteract = false;
        npc.HasTouch = false;
        npc.Entity = e;
        npc.Behavior = behavior;
        npc.Character = ch;
        npc.IsEvent = true;
        npc.Owner = owner;
        if (owner.IsAlive())
            npc.ExpireEventWithoutOwner = true;

        using (entityLock.EnterScope())
        {
            ch.Id = GetNextEntityId();
            entityList.Add(ch.Id, e);
        }

        map.AddEntity(ref e);

        npc.Behavior.InitEvent(npc, param1, param2, param3, param4, paramString);

        return e;
    }

    public void FinalizeEnterServer(LoadCharacterRequest request, NetworkConnection connection)
    {
        var config = ServerConfig.EntryConfig;
        var debug = ServerConfig.DebugConfig;

        var map = config.Map;
        var area = Area.CreateAroundPoint(config.Position, config.Area);

        connection.LoadCharacterRequest = request;
        var isRespawn = true;

        if (request.Map != null && worldMapInstanceLookup.ContainsKey(request.Map))
        {
            map = request.Map;
            area = Area.CreateAroundPoint(request.Position, 0);
            isRespawn = false;
        }

        if (debug.DebugMapOnly && !string.IsNullOrWhiteSpace(debug.DebugMapName))
        {
            map = debug.DebugMapName;
            area = Area.CreateAroundPoint(Position.Zero, 0);
        }

        if (string.IsNullOrWhiteSpace(map))
        {
            if (!string.IsNullOrWhiteSpace(request.SavePosition.MapName))
            {
                map = request.SavePosition.MapName;
                area = Area.CreateAroundPoint(request.SavePosition.Position, request.SavePosition.Area);
            }
            else
            {
                map = ServerConfig.EntryConfig.Map;
                area = Area.CreateAroundPoint(ServerConfig.EntryConfig.Position, ServerConfig.EntryConfig.Area);
            }
        }

        var playerEntity = NetworkManager.World.CreatePlayer(connection, map, area, isRespawn);

        connection.Entity = playerEntity;
        connection.LastKeepAlive = Time.ElapsedTime;
        connection.Character = playerEntity.Get<WorldObject>();
        connection.Character.IsActive = false;
        var networkPlayer = playerEntity.Get<Player>();
        networkPlayer.Connection = connection;
        connection.Player = networkPlayer;
        CommandBuilder.SendUpdatePlayerData(connection.Player);

        playerList[connection.Player.Id] = connection.Entity;

        ServerLogger.Debug($"Player assigned entity {playerEntity}, creating entity at location {connection.Character.Position}.");
    }

    public Entity CreatePlayer(NetworkConnection connection, string mapName, Area spawnArea, bool isRespawn)
    {
        var e = EntityManager.New(EntityType.Player);
        var ch = e.Get<WorldObject>();
        var player = e.Get<Player>();
        var ce = e.Get<CombatEntity>();

        if (!TryGetWorldMapByName(mapName, out var map) || map == null)
            throw new Exception($"Could not create player on world map '{mapName}' as it could not be found.");

        var req = connection.LoadCharacterRequest;
        if (req == null || !req.HasCharacter)
            throw new Exception($"Cannot perform create character without a valid LoadCharacterRequest.");

        //force the spawn area to be in bounds
        spawnArea.ClipArea(map.MapBounds);

        if (spawnArea.IsZero)
            spawnArea = map.MapBounds;

        Position p = new Position(spawnArea.MidX, spawnArea.MidY);

        //if their tile isn't walkable, put them somewhere else on the map. Note that this could be exploited.
        if (!map.WalkData.IsCellWalkable(p) && spawnArea.Width <= 1 && spawnArea.Height <= 1)
            spawnArea = map.MapBounds;

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

        using (entityLock.EnterScope())
        {
            ch.Id = GetNextEntityId();
            entityList.Add(ch.Id, e);
            playerNameLookup[req.Name] = e;
        }

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

        player.Name = req.Name;
        player.Id = req.Id;
        player.SavePosition = req.SavePosition;
        player.Inventory = req.Inventory;
        player.CartInventory = req.Cart;
        if (req.EquipState != null)
            player.Equipment = req.EquipState;
        player.CharacterSlot = req.CharacterSlot;
        player.Party = req.Party;

        if (req.SaveVersion < 3)
        {
            var data = req.Data;
            player.NpcFlags = req.NpcFlags;
            player.LearnedSkills = req.SkillsLearned ?? new Dictionary<CharacterSkill, int>();

            if (data != null)
            {
                if (data.Length <=
                    player.CharData.Length * 4) //we can add fields, but if we take them away it's all bad
                    Buffer.BlockCopy(data, 0, player.CharData, 0, data.Length);
                else
                    ServerLogger.LogWarning(
                        $"Player '{player.Name}' character data does not match the expected size. Player will be loaded with default data.");
            }
        }
        else
        {
            if (req.Data != null)
                PlayerDataDbHelper.RestorePlayerDataFromDatabaseV3(req.Data, req.DataLength, player, req.SaveVersion);
        }

        req.Data = null; //no need to hold onto this memory

        //respawn flag forces the player to be alive after logging in if they've been moved from their logout position.
        if (isRespawn && player.GetData(PlayerStat.Hp) == 0)
        {
            player.SetData(PlayerStat.Hp, 1);
            player.Character.State = CharacterState.Idle;
        }

        //convert character that has no job level to one that does, using a questionable formula.
        if (req.SaveVersion < 2)
        {
            var job = player.GetData(PlayerStat.Job);
            var playerLevel = player.GetData(PlayerStat.Level);
            var jobLvl = 1;
            if (job == 0)
                jobLvl = int.Clamp(player.GetData(PlayerStat.Level), 1, 10);
            else
                jobLvl = player.GetData(PlayerStat.Level) switch
                {
                    < 11 => 1,
                    < 50 => playerLevel - 10,
                    < 70 => 40 + (playerLevel - 50) / 2,
                    _ => 50
                };
            player.SetData(PlayerStat.JobLevel, jobLvl);
            player.SetData(PlayerStat.JobExp, 0);
        }

        player.ApplyDataToCharacter();

        if (ce.GetStat(CharacterStat.Hp) <= 0)
            player.Character.State = CharacterState.Dead;
        //ce.FullRecovery(true, true);

        player.Init();

        ch.Name = player.Name;
        connection.LoadCharacterRequest = null;
        connection.Player = player;

        if (player.Party != null)
            player.Party.LogMemberIn(player);

        moveRequests.Writer.TryWrite(new MapMoveRequest(e, MoveRequestType.InitialSpawn, null, map, p));

        return e;
    }

    public Entity CreateMonster(Map map, MonsterDatabaseInfo monsterDef, Area spawnArea, MapSpawnRule? spawnRule, bool addToMap = true)
    {
        var e = EntityManager.New(EntityType.Monster);
        var ch = e.Get<WorldObject>();
        var ce = e.Get<CombatEntity>();
        var m = e.Get<Monster>();
        var monsterSpawnBounds = map.MapBounds.Shrink(4, 4);

        var useOfficialSpawnMode = ServerConfig.OperationConfig.UseAccurateSpawnZoneFormula && spawnRule != null;
        if (spawnRule != null)
        {
            if (spawnRule.GuaranteeInZone)
                useOfficialSpawnMode = false;
        }

        Position p;
        if (!spawnArea.IsZero && spawnArea.Size <= 1)
        {
            p = spawnArea.Min;

            //if our set location is not walkable we should spawn it somewhere else random
            //should probably change this to not allow a spawn to be blocked by icewall or something if that skill ever gets added
            if (!map.WalkData.IsCellWalkable(p))
            {
                if (map.FindPositionInRange(monsterSpawnBounds, out var p2))
                    p = p2;
                else
                    p = new Position(30, 30);
            }
        }
        else
        {
            if (useOfficialSpawnMode && !spawnArea.IsZero)
            {
                if (!map.FindPositionUsing9Slice(spawnArea, out p) && !map.FindPositionInRange(monsterSpawnBounds, out p))
                    ServerLogger.LogWarning($"Failed to spawn {monsterDef.Name} on map {map.Name}, could not find spawn location around {spawnArea}. Spawning randomly on map.");
            }
            else
            {
                if (spawnArea.IsZero)
                    spawnArea = map.MapBounds.Shrink(14, 14);
                else
                    spawnArea.ClipArea(monsterSpawnBounds);

                if (!map.FindPositionInRange(spawnArea, out p))
                {
                    ServerLogger.LogWarning($"Failed to spawn {monsterDef.Name} on map {map.Name}, could not find spawn location around {spawnArea}. Spawning randomly on map.");
                    if (!map.FindPositionInRange(monsterSpawnBounds, out p))
                        p = new Position(30, 30);
                }
            }
        }

        using (entityLock.EnterScope())
        {
            ch.Id = GetNextEntityId();
            entityList.Add(ch.Id, e);
        }

        ch.IsActive = true;
        ch.ClassId = monsterDef.Id;
        ch.Entity = e;
        ch.Position = p;
        ch.MoveSpeed = monsterDef.MoveSpeed;
        ch.Type = CharacterType.Monster;
        ch.FacingDirection = (Direction)GameRandom.NextInclusive(0, 7);
        ch.Init(ref e);

        ce.Entity = e;

        ce.Init(ref e, ch);
        m.Initialize(ref e, ch, ce, monsterDef, monsterDef.AiType, spawnRule, map.Name);

        if (addToMap)
        {
            map.AddEntity(ref e);

            if (spawnRule != null && (spawnRule.DisplayType == CharacterDisplayType.Boss ||
                                      spawnRule.DisplayType == CharacterDisplayType.Mvp))
            {
                ch.IsImportant = true;
                ch.DisplayType = spawnRule.DisplayType;
                map.RegisterImportantEntity(ch);
            }

            if (spawnRule == null)
            {
                if (DataManager.MvpMonsterCodes.Contains(monsterDef.Code))
                {
                    ch.IsImportant = true;
                    ch.DisplayType = CharacterDisplayType.Mvp;
                    map.RegisterImportantEntity(ch);
                }
            }
        }

        if (monsterDef.Minions != null && monsterDef.Minions.Count > 0)
        {
            for (var i = 0; i < monsterDef.Minions.Count; i++)
            {
                var minionDef = monsterDef.Minions[i];
                var givesExp = minionDef.InitialGivesExp;
                for (var j = 0; j < minionDef.Count; j++)
                {
                    var minion = CreateMonster(map, minionDef.Monster, Area.CreateAroundPoint(p, 2), null);
                    if (!givesExp)
                        minion.Get<Monster>().GivesExperience = false;
                    m.AddChild(ref minion, MonsterAiType.AiMinion);
                }
            }
        }


        return e;
    }


    public bool RespawnMonster(Monster monster)
    {
        if (monster.Character.IsActive)
            return false;

        var spawnEntry = monster.SpawnRule;
        if (spawnEntry == null)
        {
            ServerLogger.LogWarning($"A monster is attempting to respawn but it has no respawn rule!");
            return false;
        }

        var map = monster.Character.Map;

        Debug.Assert(map != null);

        var monsterSpawnBounds = map.MapBounds.Shrink(4, 4);
        var spawnArea = monsterSpawnBounds;
        if (spawnEntry.HasSpawnZone)
            spawnArea = spawnEntry.SpawnArea;

        Position p;
        if (spawnArea.Width == 1 && spawnArea.Height == 1 && spawnArea.MinX != 0 && spawnArea.MinY != 0)
        {
            p = new Position(spawnArea.MinX, spawnArea.MinY);

            if (!map.WalkData.IsCellWalkable(p))
            {
                if (!map.FindPositionInRange(monsterSpawnBounds, out p))
                {
                    return false;
                }
            }
        }
        else
        {
            if (ServerConfig.OperationConfig.UseAccurateSpawnZoneFormula && !spawnArea.IsZero)
            {
                if (!map.FindPositionUsing9Slice(spawnArea, out p) && !map.FindPositionInRange(monsterSpawnBounds, out p))
                    ServerLogger.LogWarning($"Failed to respawn {monster.MonsterBase.Name} on map {map.Name}, could not find a valid tile.");
            }
            else
            {
                if (spawnArea.IsZero)
                    spawnArea = map.MapBounds.Shrink(14, 14);
                else
                    spawnArea.ClipArea(monsterSpawnBounds);

                if (!map.FindPositionInRange(spawnArea, out p) && !map.FindPositionInRange(monsterSpawnBounds, out p))
                {
                    ServerLogger.LogWarning($"Failed to respawn {monster.MonsterBase.Name} on map {map.Name}, could not find a valid tile.");
                    return false;
                }
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

        if (ch.IsImportant)
            map.RegisterImportantEntity(ch);

        map.AddEntity(ref e, false);

        var monsterDef = monster.MonsterBase;

        if (monsterDef.Minions != null && monsterDef.Minions.Count > 0)
        {
            for (var i = 0; i < monsterDef.Minions.Count; i++)
            {
                var minionDef = monsterDef.Minions[i];
                var givesExp = minionDef.InitialGivesExp;
                for (var j = 0; j < minionDef.Count; j++)
                {
                    var minion = CreateMonster(map, minionDef.Monster, Area.CreateAroundPoint(p, 2), null);
                    if (!givesExp)
                        minion.Get<Monster>().GivesExperience = false;
                    monster.AddChild(ref minion, MonsterAiType.AiMinion);
                }
            }
        }

        return true;
    }


    public void PerformRemovals()
    {
        while (removeList.Count > 0)
        {
            if (!removeList.TryTake(out var entity))
            {
                ServerLogger.LogError($"Read from entity remove list failed! This shouldn't happen if PerformRemovals is done on the main thread.");
                return;
            }

            if (entity.IsNull() || !entity.IsAlive())
                continue;

            if (entity.TryGet<Player>(out var p))
            {
                p.Party?.LogMemberOut(p);
                playerNameLookup.Remove(p.Name);
            }

            ServerLogger.Debug($"Removing entity {entity} from world.");

            EntityManager.Recycle(entity);
        }

        removeList.Clear();
    }

    public bool IsValidMap(string mapName) => worldMapInstanceLookup.ContainsKey(mapName);

    public bool TryGetWorldMapByName(string mapName, [NotNullWhen(true)] out Map? map)
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
            {
                CommandBuilder.InformEnterServer(character, player);
                CommandBuilder.SendMapMemoLocations(player);
            }

            if (move.MoveRequestType == MoveRequestType.MapMove)
            {
                CommandBuilder.SendChangeMap(character, player);

                player.SaveCharacterToData();
                var req = new SaveCharacterRequest(player);
                RoDatabase.EnqueueDbRequest(req);
            }
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
        character.IsActive = false;
        moveRequests.Writer.TryWrite(new MapMoveRequest(entity, MoveRequestType.MapMove, character.Map, map, newPosition));
    }

    public void SetGlobalSignal(string signalName, Entity entity)
    {
        using (entityLock.EnterScope())
        {
            globalSignalNpcs.Add(signalName, entity);
        }
    }

    public Entity GetGlobalSignalTarget(string signalName)
    {
        return globalSignalNpcs.TryGetValue(signalName, out var e) ? e : Entity.Null;
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