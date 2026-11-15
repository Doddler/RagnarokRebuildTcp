namespace RoBotClient.Bot.Behavior;

// Manual operator commands (from the UI / MCP) that override autonomous behavior: force a sell trip now,
// or send the bot to a spot and hold it there until released.
public sealed partial class BotBehavior
{
    private bool _forceShop;
    private bool _parkActive;
    private string _parkMap = "";
    private int _parkX, _parkY;
    private DateTime _nextPark = DateTime.MinValue;

    public bool IsParked => _parkActive;

    /// <summary>Force a sell trip on the next tick — sell junk now, ignoring the weight/stack thresholds
    /// and the shop cooldown. Works even when AutoShop is off.</summary>
    public void RequestSell() => _forceShop = true;

    /// <summary>Send the bot to (map, x, y) and hold there until <see cref="ClearPark"/>. Overrides hunting/shopping.</summary>
    public void GoToAndWait(string map, int x, int y)
    {
        if (string.IsNullOrWhiteSpace(map)) return;
        _parkActive = true;
        _parkMap = map;
        _parkX = x;
        _parkY = y;
        _nextPark = DateTime.MinValue;
        OnLog?.Invoke($"Ordered to {map} ({x},{y}) — will hold there until released.");
    }

    /// <summary>Cancel a go-to-and-wait order and resume normal behavior.</summary>
    public void ClearPark()
    {
        if (!_parkActive) return;
        _parkActive = false;
        OnLog?.Invoke("Hold released — resuming normal behavior.");
    }

    private async Task TickParkAsync(Snapshot snap, bool stuck, CancellationToken ct)
    {
        Mode = BotMode.Parked;

        // Travel to the target map first.
        if (!string.Equals(snap.Map, _parkMap, StringComparison.OrdinalIgnoreCase))
        {
            if (stuck) { ResetStuck(snap.SelfPos); await NudgeAsync(snap, ct); return; }
            if (DateTime.UtcNow < _nextPark) return;
            _nextPark = DateTime.UtcNow.AddSeconds(1.3);
            await StepTowardMapAsync(snap, _parkMap, ct);
            return;
        }

        // On the target map: walk to the spot, then hold position.
        if (Math.Max(Math.Abs(_parkX - snap.SelfPos.X), Math.Abs(_parkY - snap.SelfPos.Y)) <= 2)
            return; // arrived — idle here
        if (stuck) { ResetStuck(snap.SelfPos); await NudgeAsync(snap, ct); return; }
        if (DateTime.UtcNow < _nextPark) return;
        _nextPark = DateTime.UtcNow.AddSeconds(1.2);
        await WalkPathTowardAsync(snap, _parkX, _parkY, 16, ct);
    }
}
