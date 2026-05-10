using RoRebuildServer.Simulation;

namespace RoRebuildServer.Data.MapData;

public static class MapDataManager
{
    private static Dictionary<string, ServerMapConfig> mapConfigData = new();
    private static Stack<Map> unInstantiatedMaps = new();

    private static readonly Lock mapDataLock = new();


    public static MapWalkData LoadMapWalkData(string mapBaseName)
    {
        //temporarily disabled, need a way to handle map modifications smoothly

        var flags = DataManager.GetFlagsForMap(mapBaseName);
        return new MapWalkData(mapBaseName, flags);

        //lock (mapDataLock)
        //{
        //    if (mapWalkData.TryGetValue(mapBaseName, out var walkData))
        //        return walkData;

        //    var flags = DataManager.GetFlagsForMap(mapBaseName);
        //    var walkData = new MapWalkData(mapBaseName, flags);

        //    mapWalkData.Add(mapBaseName, walkData);

        //    return walkData;
        //}
    }

    public static void ReturnMapToPool(Map map)
    {
        lock (mapDataLock)
            unInstantiatedMaps.Push(map);
    }

    public static Map PrepareMap(World world, Instance instance, string name, string walkData, bool canTeleport)
    {
        lock (mapDataLock)
        {
            if (unInstantiatedMaps.Count > 0)
            {
                var existing = unInstantiatedMaps.Pop();
                existing.Instantiate(world, instance, name, walkData, canTeleport);

                return existing;
            }
        }

        return new Map(world, instance, name, walkData, canTeleport);
    }
}
