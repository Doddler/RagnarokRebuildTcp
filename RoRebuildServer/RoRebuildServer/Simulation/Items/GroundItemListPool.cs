using Microsoft.Extensions.ObjectPool;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Simulation.Items;

public static class GroundItemListPool
{
    private static ObjectPool<GroundItemList> pool;

    //private static bool isInitialized;

    static GroundItemListPool()
    {
        var defaultPolicy = new DefaultPooledObjectPolicy<GroundItemList>();
        pool = new DefaultObjectPool<GroundItemList>(defaultPolicy);
        //isInitialized = true;
    }

    public static GroundItemList Get()
    {
        //if (!isInitialized)
        //    Initialize();
        return pool.Get();
    }

    public static void Return(GroundItemList e)
    {
        //if (!isInitialized)
        //    Initialize();
        e.Clear();
        pool.Return(e);
    }
}