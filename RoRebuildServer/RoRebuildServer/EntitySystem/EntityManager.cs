namespace RoRebuildServer.EntitySystem;

public static class EntityManager
{
    public static EntityData[] Entities = null!;
    public static int EntityCount;
    public static GrowList<int> FreeEntities = null!;

    internal static int ComponentTypeCount;

    private static ReaderWriterLockSlim clientLock = new();

    public static void Initialize(int initialCapacity)
    {
        Entities = new EntityData[initialCapacity];
        FreeEntities = new GrowList<int>(initialCapacity);
        EntityCount = 0;
    }

    private static EntityData GenerateEntityData(EntityType type, int id)
    {
        if (id >= Entities.Length)
            Array.Resize(ref Entities, Entities.Length * 2);

        var data = Entities[id];

        data.Type = type;
        data.Gen++;
        
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

            if (EntityCount < Entities.Length)
            {
                EntityCount++;
                var id = EntityCount;
                var entityData = GenerateEntityData(type, id);

                return new Entity() { Id = id, Gen = entityData.Gen, TypeId = (byte)type };
            }
   
            Array.Resize(ref Entities, Entities.Length << 1);

            EntityCount++;
            var id2 = EntityCount;
            var data = GenerateEntityData(type, id2);

            return new Entity() { Id = id2, Gen = data.Gen, TypeId = (byte)type };
        }
        finally
        {
            clientLock.ExitWriteLock();
        }
    }

    public static void Recycle(Entity e)
    {
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