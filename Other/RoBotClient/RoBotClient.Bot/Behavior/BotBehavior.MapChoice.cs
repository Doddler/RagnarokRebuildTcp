using RoBotClient.Bot.GameData;

namespace RoBotClient.Bot.Behavior;

/// <summary>One hunting map the bot weighed. ExpPerHour is measured (time on the map vs exp gained, scaled to
/// the current level) when Measured, else the battle simulator's estimate scaled by how this bot's real
/// results compare to its predictions so far.</summary>
/// Monsters = the map's regular spawns (name, count, whether the bot can beat it), huntable first; Note = a warning, or empty.
/// PerKill = seconds per kill by activity: measured on the current map, predicted (calibrated) elsewhere.
public sealed record MapOption(string Map, string Name, double ExpPerHour, bool Measured, int Hops, string Note,
    IReadOnlyList<(string Name, int Count, bool Huntable)> Monsters, double VsCurrent = 0, ActivityTime PerKill = default);

/// <summary>Seconds by activity. Fight = in a duel (target within reach), Search = looking for and walking to the next
/// target (looting included), Recover = sitting for HP/SP, Flee = running from a monster it can't beat.</summary>
public readonly record struct ActivityTime(double Fight, double Search, double Recover, double Flee)
{
    public double Total => Fight + Search + Recover + Flee;
    public static ActivityTime operator +(ActivityTime a, ActivityTime b) => new(a.Fight + b.Fight, a.Search + b.Search, a.Recover + b.Recover, a.Flee + b.Flee);
    public static ActivityTime operator -(ActivityTime a, ActivityTime b) => new(a.Fight - b.Fight, a.Search - b.Search, a.Recover - b.Recover, a.Flee - b.Flee);
    public ActivityTime Per(double count) => count > 0 ? new(Fight / count, Search / count, Recover / count, Flee / count) : default;
}

// Auto map choice: hunt where exp/hour is best. The current map is judged on what actually happened there;
// other maps on a simulator estimate (spawn list x forecast per monster). Moving costs travel time, so a map
// must beat the current one by MoveFactor after that cost — otherwise two similar maps would ping-pong.
public sealed partial class BotBehavior
{
    private const double MoveFactor = 1.25;
    private const double MeasureSeconds = 180;        // hunting time on a map before its real exp/h counts
    private const double StayHorizonSeconds = 1800;  // travel time is spread over a ~30 min stay
    private const double SecondsPerHop = 40;          // ponytail: flat per-map walk estimate; use route cell length if it misleads
    private static readonly TimeSpan MapCheckEvery = TimeSpan.FromMinutes(10);

    public string MapDecision { get; private set; } = "";
    public IReadOnlyList<MapOption> MapOptions { get; private set; } = Array.Empty<MapOption>(); // candidates, current map excluded
    public MapOption? CurrentMapOption { get; private set; }
    /// <summary>Map the bot decided to move to and is still travelling to; null once it arrives.</summary>
    public string? NextMap { get; private set; }
    /// <summary>Raised after the bot picks a new hunting map, so the manager can save it (survives restarts).</summary>
    public event Action? OnHomeMapChanged;
    /// <summary>Exp/hour a candidate needs (after its travel time) to be worth moving to: current x MoveFactor.</summary>
    public double MinToChange { get; private set; }

    /// <summary>Seconds and exp on a map, from saved history (set by BotManager; survives restarts). Kills, Time (by activity) and
    /// Other (seconds in no activity: shopping, dying) cover only the stays where the activity split was tracked.</summary>
    public Func<string, (double Seconds, double Exp, int Kills, ActivityTime Time, double Other)>? MeasureMap { get; set; }
    /// <summary>Running total of seconds by activity. Seeded from the last progress sample and saved with each one, so a
    /// map stay's split is the difference between two samples (like exp).</summary>
    public ActivityTime TimeSpent { get; set; }
    private DateTime _lastActivityTick;

    // Called at the start of each tick: the time since the last tick belongs to the mode that tick left us in.
    private void CountActivityTime(Snapshot snap)
    {
        var now = DateTime.UtcNow;
        var elapsed = _lastActivityTick == default ? 0 : Math.Min(2, (now - _lastActivityTick).TotalSeconds); // a stall isn't activity
        _lastActivityTick = now;
        var target = Mode == BotMode.Hunting && _targetId != 0 ? FindMonster(snap, _targetId) : null;
        TimeSpent += Mode switch
        {
            BotMode.Hunting when target != null && Dist(snap.SelfPos, target.Pos) <= InferAttackRange(snap) + 1 => new ActivityTime(elapsed, 0, 0, 0),
            BotMode.Hunting or BotMode.Idle or BotMode.Looting => new ActivityTime(0, elapsed, 0, 0),
            BotMode.Resting => new ActivityTime(0, 0, elapsed, 0),
            BotMode.Fleeing => new ActivityTime(0, 0, 0, elapsed),
            _ => default, // travelling, shopping, dead, ...: not hunting
        };
    }

    private readonly Dictionary<string, List<DateTime>> _mapDeaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _mapBannedUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _walkableCells = new(StringComparer.OrdinalIgnoreCase);
    // Per map, per kill: exp, predicted seconds by activity, and the respawn cap (exp/s the spawns can supply).
    private sealed record MapEstimate(double Rate, bool Safe, string Note, List<(string Name, int Count, bool Huntable)> Monsters,
        double ExpPerKill, ActivityTime PerKill, double SupplyPerSecond);
    private Dictionary<string, MapEstimate> _mapEstimates = new(StringComparer.OrdinalIgnoreCase);
    private int _estimatesLevel = -1;
    private DateTime _nextMapCheck = DateTime.UtcNow.AddMinutes(2);
    private int _lastMapCheckLevel;
    private DateTime _lastMapCheckAt;

    private void NoteMapDeath(string map)
    {
        if (!_mapDeaths.TryGetValue(map, out var list)) _mapDeaths[map] = list = new();
        list.Add(DateTime.UtcNow);
        list.RemoveAll(t => DateTime.UtcNow - t > TimeSpan.FromMinutes(10));
        if (list.Count < 2 || !string.Equals(map, _config.HomeMap, StringComparison.OrdinalIgnoreCase)) return;
        _mapBannedUntil[map] = DateTime.UtcNow.AddMinutes(30);
        list.Clear();
        _nextMapCheck = DateTime.MinValue; // re-pick right away
        _retreatReason = $"died twice on {map} in 10 min";
    }
    private string? _retreatReason;

    private void TickMapChoice(Snapshot snap)
    {
        if (_data == null || snap.SelfLevel <= 0) return;
        var now = DateTime.UtcNow;
        if (snap.SelfLevel != _estimatesLevel) { _mapEstimates = EstimateMaps(snap); _estimatesLevel = snap.SelfLevel; }

        if (!_config.AutoMap || (!string.IsNullOrEmpty(_config.SquadId) && !_config.IsSquadLeader)) { MapDecision = ""; NextMap = null; return; }
        // On the way to the chosen map: keep showing the decision until it arrives, then judge the new map at once.
        if (NextMap != null)
        {
            if (!string.Equals(snap.Map, NextMap, StringComparison.OrdinalIgnoreCase)
                && string.Equals(NextMap, _config.HomeMap, StringComparison.OrdinalIgnoreCase)) return;
            NextMap = null;
            _nextMapCheck = DateTime.MinValue;
        }
        var levelUp = snap.SelfLevel != _lastMapCheckLevel && now - _lastMapCheckAt >= TimeSpan.FromMinutes(1);
        if (now < _nextMapCheck && !levelUp) return;
        _nextMapCheck = now + MapCheckEvery;
        _lastMapCheckAt = now;
        _lastMapCheckLevel = snap.SelfLevel;
        DecideMap(snap, now);
    }

    private void DecideMap(Snapshot snap, DateTime now)
    {
        var home = _config.HomeMap;
        // The map we're on is judged only on what the bot really earned there. Its real/estimated ratio then scales
        // every other map's estimate, so they're on the same footing (raw estimates run several times too high).
        var (homeSeconds, homeExp, homeKills, homeTime, homeOther) = MeasureMap?.Invoke(home) ?? (0, 0, 0, default, 0);
        var homeMeasured = homeSeconds >= MeasureSeconds;
        var homeRate = homeMeasured ? homeExp / homeSeconds * 3600 : 0;
        _mapEstimates.TryGetValue(home, out var homeEstimateEntry);
        var homeEstimateRate = homeEstimateEntry?.Rate ?? 0;
        var calibration = homeMeasured && homeEstimateRate > 0 ? Math.Clamp(homeRate / homeEstimateRate, 0.05, 3) : 1.0;
        // Better: correct each activity on its own. Measured seconds per kill vs predicted, per activity, on the map we're on.
        // Time not in any activity (shopping, dying) is a per-kill overhead every map pays. Fleeing is map-specific
        // (candidates with a monster that chases it are excluded anyway), so it's not carried over.
        var measuredPerKill = homeTime.Per(homeKills);
        var split = homeMeasured && homeKills >= 5 && homeTime.Total >= MeasureSeconds / 2 && homeEstimateEntry != null;
        static double Factor(double measured, double predicted) => predicted > 0.1 ? Math.Clamp(measured / predicted, 0.2, 5) : 1;
        var factors = split ? new ActivityTime(Factor(measuredPerKill.Fight, homeEstimateEntry!.PerKill.Fight),
            Factor(measuredPerKill.Search, homeEstimateEntry.PerKill.Search), Factor(measuredPerKill.Recover, homeEstimateEntry.PerKill.Recover), 0) : default;
        var overheadPerKill = split ? homeOther / homeKills : 0;
        ActivityTime Predict(MapEstimate estimate) => new(estimate.PerKill.Fight * factors.Fight, estimate.PerKill.Search * factors.Search,
            estimate.PerKill.Recover * factors.Recover, 0);
        double PredictRate(MapEstimate estimate)
        {
            var seconds = Predict(estimate).Total + overheadPerKill;
            return seconds > 0 ? Math.Min(estimate.ExpPerKill / seconds, estimate.SupplyPerSecond) * 3600 : 0;
        }

        var options = new List<MapOption>();
        foreach (var (map, estimate) in _mapEstimates)
        {
            if (!estimate.Safe && !string.Equals(map, home, StringComparison.OrdinalIgnoreCase)) continue;
            if (estimate.Rate <= 0 && !string.Equals(map, home, StringComparison.OrdinalIgnoreCase)) continue;
            var hops = string.Equals(map, snap.Map, StringComparison.OrdinalIgnoreCase) ? 0 : HopsTo(snap, map);
            if (hops < 0) continue;
            var isHome = string.Equals(map, home, StringComparison.OrdinalIgnoreCase);
            var measured = isHome && homeMeasured;
            var rate = measured ? homeRate : split ? PredictRate(estimate) : estimate.Rate * calibration;
            var note = _mapBannedUntil.GetValueOrDefault(map) > now ? "avoiding: died twice here" : estimate.Note;
            var perKill = isHome ? measuredPerKill : split ? Predict(estimate) : estimate.PerKill; // current map: measured only ("—" until then)
            options.Add(new MapOption(map, _data!.MapName(map), rate, measured, hops, note, estimate.Monsters, PerKill: perKill));
        }
        var current = options.FirstOrDefault(option => string.Equals(option.Map, home, StringComparison.OrdinalIgnoreCase));
        var currentSafe = homeEstimateEntry?.Safe == true;
        var banned = _mapBannedUntil.GetValueOrDefault(home) > now;
        double Effective(MapOption option) => option.ExpPerHour * Math.Max(0, StayHorizonSeconds - option.Hops * SecondsPerHop) / StayHorizonSeconds;
        var best = options.Where(option => !string.Equals(option.Map, home, StringComparison.OrdinalIgnoreCase)
                                      && _mapBannedUntil.GetValueOrDefault(option.Map) <= now)
                          .OrderByDescending(Effective).FirstOrDefault();
        var currentRate = current?.ExpPerHour ?? 0;
        CurrentMapOption = current ?? (_mapEstimates.TryGetValue(home, out var homeEstimate)
            ? new MapOption(home, _data!.MapName(home), 0, false, 0, homeEstimate.Note, homeEstimate.Monsters)
            : new MapOption(home, _data!.MapName(home), 0, false, 0, "no monsters spawn here", Array.Empty<(string, int, bool)>()));
        MinToChange = currentRate * MoveFactor;
        MapOptions = options.Where(option => !string.Equals(option.Map, home, StringComparison.OrdinalIgnoreCase))
            .Select(option => option with { VsCurrent = currentRate > 0 ? Effective(option) / currentRate : 0 })
            .OrderByDescending(option => option.VsCurrent).ThenByDescending(option => option.ExpPerHour).Take(8).ToList();
        var currentText = current == null ? $"{home}: nothing huntable at Lv {snap.SelfLevel}"
            : $"{home} measured {currentRate:N0} exp/h";

        string? reason = null;
        var measuring = current is not { Measured: true };
        if (measuring && currentSafe && _retreatReason == null && !banned)
        {
            MapDecision = $"Staying on {home}. Reason: still measuring its real exp/h ({Duration(homeSeconds)} of {Duration(MeasureSeconds)} hunted here).";
            _nextMapCheck = now.AddMinutes(1); // decide as soon as the measurement is ready
            return;
        }
        if (best == null) { MapDecision = $"Staying on {home}. Reason: no other reachable map is safe at Lv {snap.SelfLevel}."; return; }
        var ratio = currentRate > 0 ? Effective(best) / currentRate : double.PositiveInfinity;
        var bestText = $"{best.Map} {(best.Measured ? "measured" : "estimated")} {best.ExpPerHour:N0} exp/h, {best.Hops} map(s) away";
        if (_retreatReason != null || banned) reason = $"{_retreatReason ?? $"avoiding {home}"}; {bestText}";
        else if (!currentSafe) reason = $"{home} has monsters it can't beat at Lv {snap.SelfLevel}; {bestText}";
        else if (ratio >= MoveFactor)
            reason = $"{bestText} = {Math.Floor(ratio * 100) / 100:0.00}× {currentText}, over the {MoveFactor}× bar (after travel time)";
        _retreatReason = null;

        if (reason == null)
        {
            MapDecision = $"Staying on {home}. Reason: {currentText}; best alternative {bestText} is only {Math.Floor(ratio * 100) / 100:0.00}× (needs {MoveFactor}×).";
            return;
        }
        MapDecision = $"Next map → {best.Map}. Reason: {reason}.";
        OnLog?.Invoke($"Map: moving {home} → {best.Map}. {reason}.");
        _config.HomeMap = best.Map;
        NextMap = best.Map;
        OnHomeMapChanged?.Invoke();
    }

    // 30s, 1min20s, 3min
    private static string Duration(double seconds)
    {
        var total = (int)seconds;
        return total < 60 ? $"{total}s" : total % 60 == 0 ? $"{total / 60}min" : $"{total / 60}min{total % 60}s";
    }

    private int HopsTo(Snapshot snap, string map)
    {
        var seed = _data!.World.EntryCell(map) is { } entry ? new RebuildSharedData.Data.Position(entry.X, entry.Y) : FindAnyWalkableCell(map);
        if (seed == null) return -1;
        var plan = _data.World.Plan(snap.Map, snap.SelfPos.X, snap.SelfPos.Y, map, seed.Value.X, seed.Value.Y,
            mapPenalty: m => GetMapDanger(m) * MapDangerRouteWeight);
        return plan?.Edges.Count ?? -1;
    }

    /// <summary>Simulator exp/hour per spawn map at the bot's current stats. Unsafe = an aggressive regular monster
    /// spawns there that the bot loses to (bosses/MVPs are ignored: rare, and the hazard system steers around them).</summary>
    private Dictionary<string, MapEstimate> EstimateMaps(Snapshot snap)
    {
        var perMap = new Dictionary<string, List<(RebuildSharedData.ClientTypes.MonsterDbEntry Monster, int Count, double RespawnSeconds)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var monster in _data!.MonstersById.Values)
            foreach (var spawn in monster.Spawns ?? new())
                if (!string.IsNullOrEmpty(spawn.Map) && !spawn.IsBoss && !spawn.IsMvp && monster.Exp > 0)
                {
                    if (!perMap.TryGetValue(spawn.Map, out var list)) perMap[spawn.Map] = list = new();
                    list.Add((monster, Math.Max(1, spawn.Count), Math.Max(1, spawn.RespawnMin + spawn.RespawnMax) / 2000.0)); // ms -> s, average
                }

        var hpBefore = snap.SelfHp;
        snap.SelfHp = snap.SelfMaxHp; // judge at full HP, not mid-fight (restored below)
        var regen = Math.Max(1, snap.SelfMaxHp / 50.0); // ponytail: ~2% max HP/s (sitting + potions); measured calibration absorbs the error
        var result = new Dictionary<string, MapEstimate>(StringComparer.OrdinalIgnoreCase);
        foreach (var (map, spawns) in perMap)
        {
            var cells = WalkableCells(map);
            if (cells == 0) continue;
            double exp = 0, fightSeconds = 0, recoverSeconds = 0, supplyPerSecond = 0;
            int huntableCount = 0;
            string? threat = null;
            var tooStrong = new HashSet<string>();
            foreach (var (monster, count, respawnSeconds) in spawns)
            {
                if (string.Equals(monster.Special, "Boss", StringComparison.OrdinalIgnoreCase)) continue;
                var forecast = Forecast(snap, new MonsterInfo(0, monster.Id, monster.Name, default, monster.HP, monster.HP));
                if (!forecast.CanWin) { tooStrong.Add(monster.Name); if (IsAggressive(monster.Id)) threat ??= monster.Name; continue; }
                huntableCount += count;
                exp += count * monster.Exp;
                supplyPerSecond += count * monster.Exp / respawnSeconds; // a killed monster is gone until it respawns
                fightSeconds += count * forecast.SecondsToKillMonster;
                recoverSeconds += count * forecast.ExpectedDamageTaken / regen;
            }
            // Walking to the next target: about the spacing between monsters we can beat (the rest are just scenery).
            var findSeconds = huntableCount > 0 ? 0.15 * Math.Sqrt(cells / (double)huntableCount) : 0;
            var seconds = fightSeconds + recoverSeconds + huntableCount * findSeconds;
            var perKill = huntableCount > 0 ? new ActivityTime(fightSeconds / huntableCount, findSeconds, recoverSeconds / huntableCount, 0) : default;
            var monsters = spawns.GroupBy(spawn => spawn.Monster.Name)
                .Select(group => (Name: group.Key, Count: group.Sum(spawn => spawn.Count), Huntable: !tooStrong.Contains(group.Key)))
                .OrderByDescending(entry => entry.Huntable).ThenByDescending(entry => entry.Count).ToList();
            // Exp/h is the slower of: how fast the bot kills, and how fast the map refills (7 Thief Bugs on a 6 min respawn run dry).
            result[map] = new MapEstimate(seconds > 0 ? Math.Min(exp / seconds, supplyPerSecond) * 3600 : 0, threat == null,
                threat != null ? $"aggressive {threat} too strong" : "", monsters,
                huntableCount > 0 ? exp / huntableCount : 0, perKill, supplyPerSecond);
        }
        snap.SelfHp = hpBefore;
        return result;
    }

    private int WalkableCells(string map)
    {
        if (_walkableCells.TryGetValue(map, out var n)) return n;
        var walkMap = _data!.GetWalkMap(map);
        n = 0;
        if (walkMap != null)
            for (var y = 0; y < walkMap.Height; y++)
                for (var x = 0; x < walkMap.Width; x++)
                    if (walkMap.IsWalkable(x, y)) n++;
        return _walkableCells[map] = n;
    }
}
