using System.Reflection;
using System.Runtime.CompilerServices;

namespace RoRebuildServer.EntitySystem;

public sealed class EntityIgnoreNullCheckAttribute : Attribute
{
}

public sealed class EntityComponent : Attribute
{
    public EntityType[] ApplicableTypes;

    public EntityComponent(EntityType type)
    {
        ApplicableTypes = new EntityType[1];
        ApplicableTypes[0] = type;
    }

    public EntityComponent(EntityType[] applicableEntityTypes)
    {
        ApplicableTypes = applicableEntityTypes;
    }
}

public interface IEntityAutoReset
{
    public void Reset();
}

public static class ComponentType<T> where T : class
{
    public static readonly int Index;
    public static readonly Type Type;
    public static readonly bool IsAutoReset;

    static ComponentType()
    {
        Index = Interlocked.Increment(ref EntityManager.ComponentTypeCount) - 1;
        Type = typeof(T);
        IsAutoReset = typeof(IEntityAutoReset).IsAssignableFrom(Type);
    }
}

public static class EntityComponentManager
{
    public static int MaxComponentCount;
    public static int MaxComponentPerType;
    private static readonly int[] componentIndexes;
    private static readonly int[] componentPositions;

    private static readonly EntityComponentPool[] componentPool;

    static EntityComponentManager()
    {
        var uniqueEntities = Enum.GetNames(typeof(EntityType)).Length;
        var componentTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => p.GetCustomAttributes(typeof(EntityComponent), true).Length > 0).ToArray();

        MaxComponentCount = componentTypes.Length;

        componentIndexes = new int[uniqueEntities * MaxComponentCount];
        componentPositions = new int[uniqueEntities * MaxComponentCount];
        componentPool = new EntityComponentPool[MaxComponentCount];


        for (var i = 0; i < MaxComponentCount; i++)
        {
            var et = componentTypes[i];
            var index = GetComponentIndex(et);
            componentPool[i] = new EntityComponentPool(et);
        }

        for (var i = 0; i < uniqueEntities; i++)
        {
            var index = 0;
            var entityType = (EntityType)i;

            for (var j = 0; j < MaxComponentCount; j++)
            {
                componentIndexes[i * MaxComponentCount + j] = -1;
                componentPositions[i * MaxComponentCount + j] = -1;
            }

            for (var j = 0; j < MaxComponentCount; j++)
            {
                var et = componentTypes[j];
                var attribute = et.GetCustomAttributes(typeof(EntityComponent), true).First() as EntityComponent;
                if (attribute != null && attribute.ApplicableTypes.Any(t => t == entityType))
                {
                    componentIndexes[i * MaxComponentCount + index] = GetComponentIndex(et);
                    componentPositions[i * MaxComponentCount + j] = index;
                    index++;
                }
            }

            if (index > MaxComponentPerType)
                MaxComponentPerType = index;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetComponentIndex<T>(EntityType type) where T : class
    {
        return componentPositions[(int)type * MaxComponentCount + ComponentType<T>.Index];
    }

    public static object? GetComponentByIndex(EntityType type, int slot)
    {
        var id = componentIndexes[(int)type * MaxComponentCount + slot];
        if (id < 0)
            return null;

        return componentPool[id].Get();
    }

    public static void RecycleComponent(EntityType type, int slot, object component)
    {
        var id = componentIndexes[(int)type * MaxComponentCount + slot];

        if (id < 0)
            return;

        componentPool[id].Return(component);
    }

    private static int GetComponentIndex(Type type)
    {
        var m2 = typeof(ComponentType<>).MakeGenericType(type).GetField("Index", BindingFlags.Static | BindingFlags.Public);
        return (int)(m2?.GetValue(null) ?? -1);
    }
}