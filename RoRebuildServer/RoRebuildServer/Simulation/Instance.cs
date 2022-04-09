using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Pathfinding;

namespace RoRebuildServer.Simulation;

public class Instance
{
    private readonly World world;
    public List<Map> Maps { get; set; }
    public Dictionary<string, Map> MapNameLookup = new();

    public EntityList Entities { get; set; }
    private List<Entity> removeList = new(30);

    public Pathfinder Pathfinder { get; set; }

    public Instance(World world, InstanceEntry instanceDetails)
    {
        this.world = world;
        Maps = new List<Map>(instanceDetails.Maps.Count);
        Entities = new EntityList(256);
        Pathfinder = new Pathfinder();

        foreach (var mapId in instanceDetails.Maps)
        {
            var md = DataManager.Maps.FirstOrDefault(m => m.Code == mapId);
            if (md == null)
            {
                ServerLogger.LogError($"Instance {instanceDetails.Name} could not create map {mapId} as it was not found in the map data table.");
                continue;
                
            }
            
            var map = new Map(world, this, md.Code, md.WalkData);
            Maps.Add(map);
            MapNameLookup.Add(md.Code, map);
        }
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

    public void Update()
    {
        DoRemovals(); //this happens a frame late, but it's done to catch removals added after update is called

        foreach (var map in Maps)
            map.Update();

        foreach (var entity in Entities)
        {
            if(entity.IsAlive())
                entity.Get<WorldObject>().Update();
        }
    }
}