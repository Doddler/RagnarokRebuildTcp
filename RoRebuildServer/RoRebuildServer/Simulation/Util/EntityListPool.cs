using Microsoft.Extensions.ObjectPool;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Simulation.Util;

public static class EntityListPool
{
    private static ObjectPool<EntityList> pool;

    private static bool isInitialized;

    static EntityListPool()
    {
        var defaultPolicy = new DefaultPooledObjectPolicy<EntityList>();
        pool = new DefaultObjectPool<EntityList>(defaultPolicy);
        isInitialized = true;
    }

    public static EntityList Get()
    {
        //if (!isInitialized)
        //    Initialize();
        return pool.Get();
    }

    public static void Return(EntityList e)
    {
        //if (!isInitialized)
        //    Initialize();
        e.Clear();
        pool.Return(e);
    }
}