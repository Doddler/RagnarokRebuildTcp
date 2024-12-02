using System.Numerics;
using Microsoft.Extensions.ObjectPool;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Simulation.Util;

public static class EntityValueListPool<T> where T : INumber<T>
{
    private static readonly ObjectPool<EntityValueList<T>> Pool;

#if DEBUG
    //it is very, VERY bad if an entity list gets returned to the pool more than once
    //to make sure that doesn't happen we track used lists while in debug mode.
    private static readonly HashSet<int> ActiveLists = new();
#endif

    static EntityValueListPool()
    {
        var defaultPolicy = new DefaultPooledObjectPolicy<EntityValueList<T>>();
        Pool = new DefaultObjectPool<EntityValueList<T>>(defaultPolicy);
    }

    public static EntityValueList<T> Get()
    {
#if DEBUG
        var active = Pool.Get();
        if (!ActiveLists.Add(active.GetHashCode()))
            throw new Exception($"Attempting to borrow an entity value list ({typeof(T)}) that is already in use!");

        return active;
#else
        return Pool.Get();
#endif
    }

    public static void Return(EntityValueList<T> e)
    {
#if DEBUG
        if (!ActiveLists.Remove(e.GetHashCode()))
            throw new Exception($"Attempting to return an entity value list ({typeof(T)}) that is not in use!");
#endif
        e.Clear();
        Pool.Return(e);
    }
}