using RoBotClient.Bot.GameData;

namespace RoBotClient.Bot.Behavior;

// Every death sends the bot back to its save point, so the walk save point -> hunting map is paid once per death.
// Bot3 hunted pay_fild06 while respawning on prt_fild08: each death meant a trek through moc_fild02/03's Elder
// Willows (21 of its 51 deaths). When a save-capable Kafra has a much cheaper route (cells + map danger, the same
// cost the travel planner uses) to the hunting map, the bot detours once on its way there and saves at it.
public sealed partial class BotBehavior
{
    private const float SavePointMinGain = 150; // route cells (~20s walk) saved per death before a detour is worth it
    private bool _respawnPending;
    private DateTime? _respawnAliveAt;
    private bool _kafraSaved;
    private KafraEntry? _saveKafra;
    private DateTime _saveDeadline, _nextSaveCheck;

    // The first settled position after a respawn is the save point.
    private void LearnSavePoint(Snapshot snap)
    {
        if (!_respawnPending || snap.SelfPos is { X: 0, Y: 0 }) return;
        _respawnAliveAt ??= DateTime.UtcNow;
        if (DateTime.UtcNow - _respawnAliveAt < TimeSpan.FromSeconds(1)) return; // let the position settle
        _respawnPending = false;
        _respawnAliveAt = null;
        if (string.Equals(_config.SavePointMap, snap.Map, StringComparison.OrdinalIgnoreCase)) return;
        OnLog?.Invoke($"Save point: respawned on {snap.Map} ({snap.SelfPos.X},{snap.SelfPos.Y}).");
        SetSavePoint(snap.Map, snap.SelfPos.X, snap.SelfPos.Y);
    }

    private void SetSavePoint(string map, int x, int y)
    {
        (_config.SavePointMap, _config.SavePointX, _config.SavePointY) = (map, x, y);
        OnHomeMapChanged?.Invoke(); // persists the config
    }

    private float RouteCost(string fromMap, int fromX, int fromY, string toMap) =>
        _data!.World.EntryCell(toMap) is { } entry
            ? PlanRoute(fromMap, fromX, fromY, toMap, entry.X, entry.Y)?.Cost ?? float.MaxValue
            : float.MaxValue;

    /// <summary>Drives a save-point detour when one is active or worth starting. True = handled this tick.</summary>
    private async Task<bool> TickSavePointAsync(Snapshot snap, CancellationToken ct)
    {
        if (_saveKafra == null && !TryStartSaveTrip(snap)) return false;
        var k = _saveKafra!;
        if (_kafraSaved)
        {
            _kafraSaved = false;
            _saveKafra = null;
            OnLog?.Invoke($"Save point: saved at the Kafra on {k.Map} ({k.X},{k.Y}) — resuming travel to {_config.HomeMap}.");
            SetSavePoint(k.Map, k.X, k.Y); // the real respawn cell is learned on the next death
            return false;
        }
        if (DateTime.UtcNow > _saveDeadline || IsKafraBlacklisted(k.Map, k.X, k.Y))
        {
            OnLog?.Invoke($"Save point: gave up saving at the Kafra on {k.Map} (timed out).");
            _saveKafra = null;
            ResetKafraStep();
            return false;
        }
        Mode = BotMode.Traveling;
        _targetId = 0;
        if (!string.Equals(snap.Map, k.Map, StringComparison.OrdinalIgnoreCase))
            await StepTowardMapAsync(snap, k.Map, ct, (k.X, k.Y));
        else
            await StepKafraInteractionAsync(snap, k.X, k.Y, new WorldGraph.KafraEdgeInfo(0, -1, 0, "Save"), ct);
        return true;
    }

    // Only while commuting (off the hunting map), so the detour replaces walking time, not hunting time.
    private bool TryStartSaveTrip(Snapshot snap)
    {
        if (_data == null || !_config.AutoMap || string.IsNullOrEmpty(_config.HomeMap) || string.IsNullOrEmpty(_config.SavePointMap)
            || string.Equals(snap.Map, _config.HomeMap, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(_config.SquadId) && !_config.IsSquadLeader)
            || DateTime.UtcNow < _nextSaveCheck) return false;
        _nextSaveCheck = DateTime.UtcNow.AddMinutes(2);
        var home = _config.HomeMap;
        var current = RouteCost(_config.SavePointMap, _config.SavePointX, _config.SavePointY, home);
        // @Kafra NPCs (Save, Storage, Teleport...) put Teleport at option 2; @KafraNoSave ones can't save.
        var best = _data.Kafras.Where(k => k.TopMenuOption == 2 && !IsKafraBlacklisted(k.Map, k.X, k.Y)
                                           && !string.Equals(k.Map, _config.SavePointMap, StringComparison.OrdinalIgnoreCase))
            .Select(k => (Kafra: k, Cost: RouteCost(k.Map, k.X, k.Y, home)))
            .Where(c => c.Cost < float.MaxValue && RouteCost(snap.Map, snap.SelfPos.X, snap.SelfPos.Y, c.Kafra.Map) < float.MaxValue)
            .OrderBy(c => c.Cost).FirstOrDefault();
        if (best.Kafra == null || current - best.Cost < SavePointMinGain) return false;
        _saveKafra = best.Kafra;
        _saveDeadline = DateTime.UtcNow.AddMinutes(5);
        _kafraSaved = false;
        var was = current == float.MaxValue ? "no route" : $"{current:F0}";
        OnLog?.Invoke($"Save point: moving it {_config.SavePointMap} → {best.Kafra.Map}. Respawn → {home} route cost {was} → {best.Cost:F0} (cells + map danger).");
        return true;
    }
}
