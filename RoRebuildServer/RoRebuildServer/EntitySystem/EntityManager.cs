namespace RoRebuildServer.EntitySystem;

public static class EntityManager
{
    public static EntityData[] Entities = null!;
    public static int EntitiesCreated;
    public static GrowList<int> FreeEntities = null!;

    internal static int ComponentTypeCount;

    private static ReaderWriterLockSlim clientLock = new();

    public static void Initialize(int initialCapacity)
    {
        Entities = new EntityData[initialCapacity];
        FreeEntities = new GrowList<int>(initialCapacity);
        EntitiesCreated = 1; //entity 0 is considered null, so we start at 1
    }

    private static EntityData GenerateEntityData(EntityType type, int id)
    {
        if (id >= Entities.Length)
            Array.Resize(ref Entities, Entities.Length * 2);

        var data = Entities[id];
       
        data.Type = type;
        if(data.Gen == 0)
            data.Gen = 1;

        //we don't advance gen here, we advance it on recycle so we can look up
        //and see if an entity reference is of an older generation

        var typeCount = EntityComponentManager.MaxComponentPerType;

        if (data.Components == null)
            data.Components = new object[EntityComponentManager.MaxComponentPerType];

        for (var i = 0; i < typeCount; i++)
        {
            data.Components[i] = EntityComponentManager.GetComponentByIndex(type, i)!;
        }

        Entities[id] = data;

        return data;
    }

    public static Entity New(EntityType type)
    {
        clientLock.EnterWriteLock();

        try
        {
            if (FreeEntities.Count > 0)
            {
                var id = FreeEntities.Pop();
                var entityData = GenerateEntityData(type, id);

                return new Entity() { Id = id, Gen = entityData.Gen, TypeId = (byte)type };
            }

            if (EntitiesCreated < Entities.Length)
            {
                
                var id = EntitiesCreated;
                var entityData = GenerateEntityData(type, id);
                EntitiesCreated++;

                return new Entity() { Id = id, Gen = entityData.Gen, TypeId = (byte)type };
            }
   
            Array.Resize(ref Entities, Entities.Length << 1);

            var id2 = EntitiesCreated;
            var data = GenerateEntityData(type, id2);
            EntitiesCreated++;

            return new Entity() { Id = id2, Gen = data.Gen, TypeId = (byte)type };
        }
        finally
        {
            clientLock.ExitWriteLock();
        }
    }

    public static void Recycle(Entity e)
    {
        if (!e.IsAlive())
            return;

        clientLock.EnterWriteLock();

        try
        {
            FreeEntities.Add(e.Id);

            var entity = Entities[e.Id];
            
            for (var i = 0; i < EntityComponentManager.MaxComponentPerType; i++)
            {
                EntityComponentManager.RecycleComponent(entity.Type, i, entity.Components[i]);
                entity.Components[i] = null!;
            }

            entity.Gen++;
            Entities[e.Id] = entity;
        }
        finally
        {
            clientLock.ExitWriteLock();
        }
    }
}