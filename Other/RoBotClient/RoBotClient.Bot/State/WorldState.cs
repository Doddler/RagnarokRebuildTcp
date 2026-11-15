using System.Collections.Concurrent;
using RebuildSharedData.Data;

namespace RoBotClient.Bot.State;

/// <summary>Everything the bot currently knows: its own character plus all entities in view.</summary>
public sealed class WorldState
{
    public readonly SelfState Self = new();
    public readonly ConcurrentDictionary<int, EntityView> Entities = new();
    public readonly ConcurrentDictionary<int, GroundItemView> GroundItems = new();

    public Position SelfPosition =>
        Entities.TryGetValue(Self.EntityId, out var e) ? e.Position : default;
}
