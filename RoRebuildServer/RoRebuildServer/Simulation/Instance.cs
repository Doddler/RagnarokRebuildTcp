using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Parties;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation;

public class Instance
{
    private readonly World world;
    public List<Map> Maps { get; set; }
    public Dictionary<string, Map> MapNameLookup = new();
    public Dictionary<string, Entity> NpcNameLookup = new();
    public PartyExpAccumulator ExpCalculator;

    public string Name;
    public EntityList Entities { get; set; }
    private List<Entity> removeList = new(30);
    private float nextUpdate;

    public PathFinder Pathfinder { get; set; }

    public Instance(World world, InstanceEntry instanceDetails)
    {
        this.world = world;
        Name = instanceDetails.Name;
        Maps = new List<Map>(instanceDetails.Maps.Count);
        Entities = new EntityList(128);
        Pathfinder = new PathFinder();
        ExpCalculator = new PartyExpAccumulator();
#if DEBUG
        Entities.IsActive = true; //bypass EntityListPool borrow tracking
#endif

        foreach (var mapId in instanceDetails.Maps)
        {
            var md = DataManager.Maps.FirstOrDefault(m => m.Code == mapId);
            if (md == null)
            {
                ServerLogger.LogError($"Instance {instanceDetails.Name} could not create map {mapId} as it was not found in the map data table.");
                continue;
            }

            var map = new Map(world, this, md.Code, md.WalkData, md.MapMode != "Indoor");
            Maps.Add(map);
            MapNameLookup.Add(md.Code, map);
        }
    }

    public void LoadNpcs()
    {
        foreach (var map in Maps)
        {
            map.LoadNpcs();
        }
    }

    public void ReloadScripts()
    {
        foreach (var map in Maps)
            map.ReloadMapScripts();
    }

    public void RemoveEntity(ref Entity e)
    {
        removeList.Add(e);
    }

    public void DoRemovals()
    {
        for (var i = 0; i < removeList.Count; i++)
        {
            var e = removeList[i];
            Entities.Remove(ref e);
        }

        removeList.Clear();

        Entities.ClearInactive();
    }

    private static int ExceptionCount = 0;

    public void Update()
    {
        DoRemovals(); //this happens a frame late, but it's done to catch removals added after update is called

        var players = 0;
#if !DEBUG
        try
#endif
        {
            foreach (var map in Maps)
            {
                map.Update();
                players += map.PlayerCount;
            }

            if (players == 0)
            {
                if (nextUpdate > Time.ElapsedTimeFloat)
                    return;
                nextUpdate = Time.ElapsedTimeFloat + 2f;
            }

            foreach (var entity in Entities)
            {
                if (entity.IsAlive())
                {
                    var chara = entity.Get<WorldObject>();
                    if (chara.IsActive || chara.Type == CharacterType.Monster)
                        chara.Update();
                }
            }
        }
#if !DEBUG
        catch (Exception e)
        {
            ServerLogger.LogError($"Map instance {Name} generated a fatal exception! (exception limit {ExceptionCount}/10)\n{e}");
            var count = Interlocked.Increment(ref ExceptionCount);
            if (count > 10)
            {
                ServerLogger.LogError(
                    "Exception suppression level reached, throwing exception. This will kill the server.");
                throw e;
            }
        }
#endif
    }
}