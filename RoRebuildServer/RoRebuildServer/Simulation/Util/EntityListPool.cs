using Microsoft.Extensions.ObjectPool;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Simulation.Util;

public static class EntityListPool
{
    private static readonly ObjectPool<EntityList> Pool;

#if DEBUG
    //it is very, VERY bad if an entity list gets returned to the pool more than once
    //to make sure that doesn't happen we track used lists while in debug mode.
    private static readonly HashSet<int> ActiveLists = new();
#endif

    static EntityListPool()
    {
        var defaultPolicy = new DefaultPooledObjectPolicy<EntityList>();
        Pool = new DefaultObjectPool<EntityList>(defaultPolicy);
    }

    public static EntityList Get()
    {
#if DEBUG
        var active = Pool.Get();
        if (!ActiveLists.Add(active.GetHashCode()))
            throw new Exception($"Attempting to borrow an entity list that is already in use!");

        return active;
#else
        return Pool.Get();
#endif
    }

    public static void Return(EntityList e)
    {
#if DEBUG
        if (!ActiveLists.Remove(e.GetHashCode()))
            throw new Exception($"Attempting to return an entity list that is not in use!");
#endif
        e.Clear();
        Pool.Return(e);
    }
}