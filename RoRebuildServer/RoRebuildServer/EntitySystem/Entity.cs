using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RoRebuildServer.EntitySystem;

[Flags]
public enum EntityType : byte
{
    None = 0,
    Player = 1,
    Monster = 2,
    Combatant = Player & Monster,
    Npc = 4,
    Effect = 8
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public struct EntityData
{
    public ushort Gen;
    public EntityType Type;

    public object[] Components;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public struct Entity
{
    public int Id;
    public ushort Gen;
    public byte TypeId;

    public EntityType Type => (EntityType)TypeId;
    public static Entity Null = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAlive()
    {
        if (IsNull())
            return false;
        ref var data = ref EntityManager.Entities[Id];
        return Gen == data.Gen;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNull()
    {
        return Id == 0 || Gen == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() where T : class
    {
        var data = EntityManager.Entities[Id];
#if DEBUG
        if (data.Gen != Gen)
            throw new Exception("Attempting to get component of an expired entity!");
#endif
        var id = EntityComponentManager.GetComponentIndex<T>(data.Type);
        return (T)data.Components[id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetIfAlive<T>() where T : class
    {
        if (!IsAlive())
            return null;

        var data = EntityManager.Entities[Id];
#if DEBUG
        if (data.Gen != Gen)
            throw new Exception("Attempting to get component of an expired entity!");
#endif
        var id = EntityComponentManager.GetComponentIndex<T>(data.Type);
        return (T)data.Components[id];
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode() ^ (Gen.GetHashCode() << 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in Entity left, in Entity right)
    {
        return left.Id == right.Id && left.Gen == right.Gen;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Entity left, Entity right)
    {
        return left.Id != right.Id || left.Gen != right.Gen;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Entity other)
    {
        return Id == other.Id && Gen == other.Gen;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        return obj is Entity entity && Equals(entity);
    }

#if DEBUG
    public override string ToString()
    {
        return IsNull() ? "Entity-Null" : $"Entity-{Id.ToString()}:{Gen.ToString()}";
    }
#endif
}