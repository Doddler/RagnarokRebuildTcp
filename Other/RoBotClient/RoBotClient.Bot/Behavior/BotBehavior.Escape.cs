namespace RoBotClient.Bot.Behavior;

// Stuck-escape escalation: when the bot has been making no progress for substantially longer than the
// normal StuckSeconds threshold (so NudgeAsync's random-walk recovery clearly hasn't worked), reach for a
// Fly Wing (random same-map teleport) and ultimately a Butterfly Wing (return to save point). Both are
// validated against the item DB as Useable, and both are throttled together by a single _nextEscapeItem
// timer so we don't burn through the stack on a single bad tile.
public sealed partial class BotBehavior
{
    private DateTime _nextEscapeItem = DateTime.MinValue;

    private async Task<bool> TryEscapeStuckAsync(Snapshot snap, CancellationToken ct)
    {
        if (!_config.AutoEscapeWhenStuck || _data == null) return false;
        if (DateTime.UtcNow < _nextEscapeItem) return false;

        var stuckSecs = (DateTime.UtcNow - _lastProgressAt).TotalSeconds;
        if (stuckSecs < _config.FlyWingStuckSeconds) return false;

        var (hasFly, hasButterfly) = _bot.WithState(w =>
        {
            var fly = false; var bfly = false;
            foreach (var it in w.Self.Inventory)
            {
                if (it.Count <= 0) continue;
                if (it.ItemId == _config.FlyWingItemId) fly = true;
                else if (it.ItemId == _config.ButterflyWingItemId) bfly = true;
            }
            return (fly, bfly);
        });

        // Prefer Fly Wing first — same map, cheap, fast. Fall back to Butterfly Wing only after a longer
        // wait (we've either run out of Fly Wings or none were in the bag in the first place).
        int itemId;
        string itemName;
        if (hasFly && _data.IsUsableItem(_config.FlyWingItemId))
        {
            itemId = _config.FlyWingItemId;
            itemName = "Fly Wing";
        }
        else if (hasButterfly && stuckSecs >= _config.ButterflyWingStuckSeconds && _data.IsUsableItem(_config.ButterflyWingItemId))
        {
            itemId = _config.ButterflyWingItemId;
            itemName = "Butterfly Wing";
        }
        else
        {
            return false;
        }

        OnLog?.Invoke($"Stuck for {stuckSecs:F0}s — using {itemName} to escape.");
        _nextEscapeItem = DateTime.UtcNow.AddSeconds(5); // throttle so we don't blow the whole stack at once
        await _bot.UseInventoryItemAsync(itemId, -1, ct);
        ResetStuck(snap.SelfPos);
        return true;
    }
}
