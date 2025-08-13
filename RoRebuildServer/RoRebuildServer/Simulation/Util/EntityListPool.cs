using Microsoft.Extensions.ObjectPool;
using RoRebuildServer.EntitySystem;
using System.Runtime.CompilerServices;

namespace RoRebuildServer.Simulation.Util;

public static class EntityListPool
{
    private static readonly ObjectPool<EntityList> Pool;

#if DEBUG
    //it is very, VERY bad if an entity list gets returned to the pool more than once
    //to make sure that doesn't happen we track used lists while in debug mode.
    private static readonly HashSet<Guid> ActiveLists = new();
    private static bool TrackListBorrows = true; //you should probably leave this off by default because it's expensive
    private static ConditionalWeakTable<EntityList, string> EntityListCallStacks = new();
    private static object _lockObject = new();
#endif

    static EntityListPool()
    {
        var defaultPolicy = new DefaultPooledObjectPolicy<EntityList>();
        Pool = new DefaultObjectPool<EntityList>(defaultPolicy);
    }

    public static EntityList Get()
    {
#if DEBUG
        lock (_lockObject)
        {
            var active = Pool.Get();
            if (!ActiveLists.Add(active.UniqueId))
            {
                if (TrackListBorrows)
                {
                    if (EntityListCallStacks.TryGetValue(active, out var origStack))
                        throw new Exception(
                            $"Attempting to borrow an entity list that is already in use! The stack for the original borrow event is listed first:\n{origStack}");
                    else
                        throw new Exception(
                            $"Attempting to borrow an entity list that is already in use! There does not appear to be a stack trace for the original creation.");
                }

                throw new Exception($"Attempting to borrow an entity list that is already in use!");
            }

            if (TrackListBorrows)
                EntityListCallStacks.AddOrUpdate(active, Environment.StackTrace);

            active.IsActive = true;

            return active;
        }
#else
        return Pool.Get();
#endif
    }

    public static void Return(EntityList e)
    {
#if DEBUG
        lock (_lockObject)
        {

            if (!ActiveLists.Remove(e.UniqueId))
            {
                if (TrackListBorrows)
                {
                    if (EntityListCallStacks.TryGetValue(e, out var origStack))
                        throw new Exception(
                            $"Attempting to return an entity list that is not in use! The stack for the previous return event is listed first:\n{origStack}");
                    else
                        throw new Exception(
                            $"Attempting to return an entity list that is not in use! This entity list does not appear to be tracked, it might have been created directly rather than borrowed.");
                }

                throw new Exception($"Attempting to return an entity list that is not in use!");
            }

            if (TrackListBorrows)
                EntityListCallStacks.AddOrUpdate(e, Environment.StackTrace);

            if (!e.IsActive)
                throw new Exception(
                    $"Attempting to return an entity list that is either uninitialized, or has already been returned!");
            e.IsActive = false;
        }
#endif
        e.Clear();
        Pool.Return(e);
    }
}

