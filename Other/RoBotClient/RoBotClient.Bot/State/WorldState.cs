using System.Collections.Concurrent;
using RebuildSharedData.Data;

namespace RoBotClient.Bot.State;

/// <summary>Everything the bot currently knows: its own character plus all entities in view.</summary>
public sealed class WorldState
{
    public readonly SelfState Self = new();
    public readonly ConcurrentDictionary<int, EntityView> Entities = new();
    public readonly ConcurrentDictionary<int, GroundItemView> GroundItems = new();
    /// <summary>Monsters the server removed as dead, kept briefly so the dashboard minimap can play their death
    /// animation (behavior code only looks at <see cref="Entities"/>). Pruned on each new death.</summary>
    public readonly ConcurrentDictionary<int, (EntityView Entity, DateTime At)> RecentDeaths = new();

    public Position SelfPosition =>
        Entities.TryGetValue(Self.EntityId, out var e) ? e.Position : default;
}
