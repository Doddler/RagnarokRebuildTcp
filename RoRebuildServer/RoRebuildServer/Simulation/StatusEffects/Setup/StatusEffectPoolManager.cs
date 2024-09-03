using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.StatusEffects.Setup;

public struct PendingStatusEffect : IEquatable<PendingStatusEffect>
{
    public float EffectiveTime;
    public bool OverwriteExisting;
    public StatusEffectState Effect;

    public bool Equals(PendingStatusEffect other)
    {
        return EffectiveTime.Equals(other.EffectiveTime) && Effect.Equals(other.Effect);
    }

    public override bool Equals(object? obj)
    {
        return obj is PendingStatusEffect other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(EffectiveTime, Effect);
    }
}

public static class StatusEffectPoolManager
{
    private static readonly ConcurrentBag<CharacterStatusContainer> statusContainers = new();
    private static readonly ConcurrentBag<SwapList<PendingStatusEffect>> pendingStateContainers = new();

    public static CharacterStatusContainer BorrowStatusContainer()
    {
        if(statusContainers.TryTake(out var container))
            return container;
        return new CharacterStatusContainer();
    }

    public static void ReturnStatusContainer(CharacterStatusContainer container)
    {
        container.Reset();
        statusContainers.Add(container);
    }

    public static SwapList<PendingStatusEffect> BorrowPendingContainer()
    {
        if (pendingStateContainers.TryTake(out var container))
            return container;
        return new SwapList<PendingStatusEffect>(4);
    }

    public static void ReturnPendingContainer(SwapList<PendingStatusEffect> container)
    {
        container.Clear();
        pendingStateContainers.Add(container);
    }
}