using RoBotClient.Bot.GameData;

namespace RoBotClient.Bot.Behavior;

/// <summary>One hunting map the bot weighed. ExpPerHour is measured (time on the map vs exp gained, scaled to
/// the current level) when Measured, else the battle simulator's estimate scaled by how this bot's real
/// results compare to its predictions so far.</summary>
/// Monsters = the map's regular spawns (name, count, whether the bot can beat it), huntable first; Note = a warning, or empty.
/// PerKill = seconds per kill by activity: measured on the current map, predicted (calibrated) elsewhere.
public sealed record MapOption(string Map, string Name, double ExpPerHour, bool Measured, int Hops, string Note,
    IReadOnlyList<(string Name, int Count, bool Huntable, double HpLoss)> Monsters, double VsCurrent = 0, ActivityTime PerKill = default,
    double ExpPerKill = 0, double HuntSecondsPerKill = 0, double OtherSecondsPerKill = 0,
    double SupplyExpPerHour = 0, double Calibration = 1, double MeasuredExp = 0, double MeasuredSeconds = 0,
    double ModeledExpPerHour = 0, ActivityTime ExpectedPerKill = default, int MeasuredKills = 0,
    ActivityTime ActualPerKill = default, double ActualOtherSecondsPerKill = 0)
{
    /// <summary>What the bot predicts for this map (rate shown is what it ranked by: measured if it hunted here recently).</summary>
    public MapBreakdown Predicted() => new(Map, ExpPerHour, Measured, Hops, ExpPerKill, ExpectedPerKill, OtherSecondsPerKill,
        SupplyExpPerHour, Calibration, 0, 0, 0);
    /// <summary>What the bot measured here (its recent stays, up to 20 min): exp per tracked kill and seconds per kill by activity.</summary>
    public MapBreakdown Actual() => new(Map, MeasuredSeconds > 0 ? MeasuredExp / MeasuredSeconds * 3600 : 0, Measured, Hops,
        MeasuredKills > 0 ? MeasuredExp / MeasuredKills : 0, ActualPerKill, ActualOtherSecondsPerKill, SupplyExpPerHour, 1,
        MeasuredKills, MeasuredSeconds, MeasuredExp);
}

/// <summary>One side of a map move, per kill. Other = seconds in no activity (shopping, dying, walking back);
/// Calibration = factor already applied to the predicted rate (includes CandidateRealization).</summary>
public sealed record MapBreakdown(string Map, double ExpPerHour, bool Measured, int Hops, double ExpPerKill, ActivityTime PerKill,
    double Other, double SupplyExpPerHour, double Calibration, int Kills, double Seconds, double Exp)
{
    public string Describe() => $"{ExpPerKill:N0} exp/kill ÷ ({PerKill.Fight:0.0} fight + {PerKill.Search:0.0} search + {PerKill.Recover:0.0} recover"
        + (PerKill.Flee >= 0.05 ? $" + {PerKill.Flee:0.0} flee" : "") + $" + {Other:0.0} other)s = {ExpPerHour:N0} exp/h";
}

/// <summary>A map move with both sides, so a bad move can be traced to the term that misled it: Predicted = the map it went
/// to, as it looked when chosen; Left = what it really got on the map it left (pair with the earlier move into that map).</summary>
public sealed record MapMoveAudit(DateTime Time, int Level, string From, string To, string Reason, MapBreakdown Predicted,
    MapBreakdown? Left, double Realization);

/// <summary>What the bot learned that must survive a restart: map bans and the deaths/flees counting toward them,
/// monster classes it died to, and the per-kill timings on the map it was hunting.</summary>
public sealed class BotMemory
{
    public Dictionary<string, DateTime> MapBannedUntil { get; set; } = new();
    public Dictionary<string, int> MapBanCount { get; set; } = new();
    public Dictionary<string, List<DateTime>> MapDeaths { get; set; } = new();
    public Dictionary<string, List<DateTime>> MapFlees { get; set; } = new();
    public Dictionary<int, DateTime> AvoidClasses { get; set; } = new();
    public string KillSplitMap { get; set; } = "";
    public List<ActivityTime> KillSplits { get; set; } = new();
    public StayLog? Stay { get; set; }
}

/// <summary>Where the time went since the bot made Map its hunting map (see BotBehavior.NoteStay). Saved with the memory so a
/// dashboard restart doesn't wipe a stay half way.</summary>
public sealed class StayLog
{
    public string Map { get; set; } = "";
    public Dictionary<string, double> Seconds { get; set; } = new();
    public int Kills { get; set; }
    public int Targets { get; set; }
    public double ApproachCells { get; set; }
    public int ShopTrips { get; set; }
    public int Deaths { get; set; }
    public double PlayerSeconds { get; set; }
}

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
    // Share of a candidate map's modeled exp/h a bot really earns after moving there. The home map's factors are fit on
    // the home map, so they don't capture what a new map adds: a different kill mix, deaths/flees, layout, and picking
    // the most optimistic estimate of ~40 (winner's curse). Logs from 11-15..11-16 (5 bots, 65 moves): median 0.37
    // (IQR 0.21-0.59); moves claiming 1.25-3x actually earned 0.55-0.60x of the old map.
    // ponytail: one global constant; re-derive after model changes by pairing each bot-logs "Map: moving → X. X estimated E"
    // with the next decision's "X measured A" and taking the median A/E (drop A/E outside 0.05-5); per-bot if they diverge.
    // Re-fit 11-17 on the current model (11 moves with >= 5 kills): median 0.71. The home map's own estimate now lands within
    // 10% (Bot2 mjolnir_09 47k vs 51k, Bot4 pay_fild07 17k vs 18k), and the worst misses (0.13, 0.22) were crowded maps the
    // slow-threat rule now skips. At 0.37 a map had to model 3.4x the current one to win, so bots sat on Rockers for hours.
    public const double CandidateRealization = 0.7;
    /// <summary>Raised on every map move with its prediction breakdown, so the manager can persist it for auditing.</summary>
    public event Action<MapMoveAudit>? OnMapMove;

    public string MapDecision { get; private set; } = "";
    public IReadOnlyList<MapOption> MapOptions { get; private set; } = Array.Empty<MapOption>(); // candidates, current map excluded
    public MapOption? CurrentMapOption { get; private set; }
    /// <summary>Map the bot decided to move to and is still travelling to; null once it arrives.</summary>
    public string? NextMap { get; private set; }
    /// <summary>Raised after the bot picks a new hunting map, so the manager can save it (survives restarts).</summary>
    public event Action? OnHomeMapChanged;
    /// <summary>Exp/hour a candidate needs (after its travel time) to be worth moving to: current x MoveFactor.</summary>
    public double MinToChange { get; private set; }

    /// <summary>Per map: seconds and exp hunted there, from saved history (set by BotManager; survives restarts). Kills, Time
    /// (by activity) and Other (seconds in no activity: shopping, dying) cover only the stays where the split was tracked.</summary>
    public Func<Dictionary<string, (double Seconds, double Exp, int Kills, ActivityTime Time, double Other)>>? MeasureMaps { get; set; }
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
        NoteKillSplit(snap.Map);
        NoteStay(snap, target, elapsed);
    }

    // Why a map's exp/h missed its prediction: seconds per cause since the bot made this its hunting map (shop trips and
    // deaths off the map count too). "Search" splits into walking to a picked target, wandering with no monster in view,
    // and wandering past monsters it didn't pick; approach cells vs the model's spacing say whether the layout or the count is off.
    private StayLog _stay = new();
    private int _stayTargetId, _stayLastKills = -1; // session kill counter at the last tick; -1 = not read yet (restart)
    private DateTime _staySavedAt;
    private BotMode _stayLastMode;
    private string _idleCause = "none picked"; // set by PickTarget when it finds nothing

    private void NoteStay(Snapshot snap, MonsterInfo? target, double elapsed)
    {
        if (!string.Equals(_stay.Map, _config.HomeMap, StringComparison.OrdinalIgnoreCase)) _stay = new StayLog { Map = _config.HomeMap };
        var kills = _bot.WithState(w => w.Self.Kills);
        if (_stayLastKills >= 0 && kills > _stayLastKills) _stay.Kills += kills - _stayLastKills;
        _stayLastKills = kills;
        if (Mode != _stayLastMode)
        {
            if (Mode == BotMode.Shopping) _stay.ShopTrips++;
            if (Mode == BotMode.Dead) _stay.Deaths++;
            _stayLastMode = Mode;
        }
        if (target != null && target.Id != _stayTargetId) { _stay.Targets++; _stay.ApproachCells += Dist(snap.SelfPos, target.Pos); }
        if (DateTime.UtcNow - _staySavedAt > TimeSpan.FromMinutes(1)) { _staySavedAt = DateTime.UtcNow; OnMemoryChanged?.Invoke(); }
        _stayTargetId = target?.Id ?? 0;
        if (elapsed <= 0) return;
        var onMap = string.Equals(snap.Map, _stay.Map, StringComparison.OrdinalIgnoreCase);
        var bucket = Mode switch
        {
            BotMode.Hunting when target != null => Dist(snap.SelfPos, target.Pos) <= InferAttackRange(snap) + 1 ? "fight" : "approach",
            BotMode.Hunting or BotMode.Idle => snap.Monsters.Count == 0 ? "wander, none in view" : $"wander, {_idleCause}",
            BotMode.Looting => "loot",
            BotMode.Resting => "rest",
            BotMode.Fleeing => "flee",
            BotMode.Traveling when onMap => "walking on map",
            _ => Mode.ToString().ToLowerInvariant(),
        };
        _stay.Seconds[bucket] = _stay.Seconds.GetValueOrDefault(bucket) + elapsed;
        if (onMap) _stay.PlayerSeconds += elapsed * _bot.WithState(w => w.Entities.Values.Count(e => e.Type == RebuildSharedData.Enum.CharacterType.Player && e.Id != w.Self.EntityId));
    }

    private string? StaySummary()
    {
        var total = _stay.Seconds.Values.Sum();
        if (total < 60 || string.IsNullOrEmpty(_stay.Map)) return null;
        var kills = _stay.Kills;
        var perKill = kills > 0 ? kills : 1;
        var onMapSeconds = total - _stay.Seconds.Where(kv => kv.Key is "traveling" or "shopping" or "dead").Sum(kv => kv.Value);
        var parts = _stay.Seconds.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / perKill:0.0}");
        var spacing = _mapEstimates.TryGetValue(_stay.Map, out var estimate) && estimate.PerKill.Search > 0 ? $", model spacing {estimate.PerKill.Search / PlayerSecondsPerCell:0}" : "";
        return $"Map: time on {_stay.Map} over {Duration(total)}, {kills} kills, s/kill: {string.Join(", ", parts)}"
               + $"; approach {(_stay.Targets > 0 ? _stay.ApproachCells / _stay.Targets : 0):0} cells/target over {_stay.Targets} targets{spacing}"
               + $"; {_stay.ShopTrips} shop trips, {_stay.Deaths} deaths; {(onMapSeconds > 0 ? _stay.PlayerSeconds / onMapSeconds : 0):0.0} other players in view.";
    }

    // Per kill on the current map: activity seconds since the previous kill. Reset on map change; in memory only.
    private readonly List<ActivityTime> _killSplits = new();
    private string _killSplitMap = "";
    private int _killSplitCount = -1;
    private ActivityTime _killSplitStart;

    private void NoteKillSplit(string map)
    {
        if (string.IsNullOrEmpty(map)) return; // not loaded yet: don't wipe splits restored from memory
        var kills = _bot.WithState(w => w.Self.Kills);
        var sameMap = string.Equals(map, _killSplitMap, StringComparison.OrdinalIgnoreCase);
        if (!sameMap || _killSplitCount < 0) // new map, or first tick after a restart restored this map's splits
        {
            _killSplitMap = map;
            if (!sameMap) lock (_killSplits) _killSplits.Clear();
            _killSplitCount = kills;
            _killSplitStart = TimeSpent;
            return;
        }
        if (kills <= _killSplitCount) return;
        var split = TimeSpent - _killSplitStart;
        _killSplitCount = kills;
        _killSplitStart = TimeSpent;
        if (split.Total <= 0) return; // a kill without hunting time (killed while travelling) says nothing
        lock (_killSplits)
        {
            _killSplits.Add(split);
            if (_killSplits.Count > 200) _killSplits.RemoveAt(0);
        }
        OnMemoryChanged?.Invoke();
    }

    /// <summary>Raised when something in <see cref="ExportMemory"/> changed (BotManager saves it).</summary>
    public event Action? OnMemoryChanged;

    public BotMemory ExportMemory()
    {
        lock (_killSplits)
            return new BotMemory
            {
                MapBannedUntil = new(_mapBannedUntil),
                MapBanCount = new(_mapBanCount),
                MapDeaths = _mapDeaths.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
                MapFlees = _mapFlees.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
                AvoidClasses = new(_avoidClasses),
                KillSplitMap = _killSplitMap,
                KillSplits = _killSplits.ToList(),
                Stay = new StayLog { Map = _stay.Map, Seconds = new(_stay.Seconds), Kills = _stay.Kills, Targets = _stay.Targets,
                    ApproachCells = _stay.ApproachCells, ShopTrips = _stay.ShopTrips, Deaths = _stay.Deaths, PlayerSeconds = _stay.PlayerSeconds },
            };
    }

    /// <summary>Restore before the first tick. Expired bans/avoidances are dropped; stale death/flee times are pruned
    /// by the 10 min window on the next death/flee.</summary>
    public void ImportMemory(BotMemory memory)
    {
        var now = DateTime.UtcNow;
        foreach (var (map, until) in memory.MapBannedUntil) if (until > now) _mapBannedUntil[map] = until;
        foreach (var (map, count) in memory.MapBanCount ?? new()) _mapBanCount[map] = count;
        foreach (var (map, times) in memory.MapDeaths) _mapDeaths[map] = times.ToList();
        foreach (var (map, times) in memory.MapFlees) _mapFlees[map] = times.ToList();
        foreach (var (cls, until) in memory.AvoidClasses) if (until > now) _avoidClasses[cls] = until;
        _killSplitMap = memory.KillSplitMap ?? "";
        lock (_killSplits) { _killSplits.Clear(); _killSplits.AddRange(memory.KillSplits); }
        if (memory.Stay != null) _stay = memory.Stay;
    }

    /// <summary>Median seconds per kill by activity on <paramref name="map"/> (each activity's own median), and how many
    /// kills that covers. Null when the bot has no timed kills there since it arrived.</summary>
    public (ActivityTime Median, int Kills)? MedianKillSplit(string map)
    {
        if (!string.Equals(map, _killSplitMap, StringComparison.OrdinalIgnoreCase)) return null;
        List<ActivityTime> splits;
        lock (_killSplits) splits = _killSplits.ToList();
        if (splits.Count == 0) return null;
        static double Median(IEnumerable<double> values) { var v = values.Order().ToList(); return v[v.Count / 2]; }
        return (new ActivityTime(Median(splits.Select(s => s.Fight)), Median(splits.Select(s => s.Search)),
            Median(splits.Select(s => s.Recover)), Median(splits.Select(s => s.Flee))), splits.Count);
    }

    /// <summary>On its hunting map and doing hunt work (not passing through, shopping or dead).</summary>
    public bool IsHuntingHere(string map) =>
        string.Equals(map, _config.HomeMap, StringComparison.OrdinalIgnoreCase)
        && Mode is BotMode.Hunting or BotMode.Idle or BotMode.Looting or BotMode.Resting or BotMode.Fleeing;

    private readonly Dictionary<string, List<DateTime>> _mapDeaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _mapBannedUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _mapBanCount = new(StringComparer.OrdinalIgnoreCase);

    // Only maps the bot really died or fled on get banned, and each repeat ban doubles: 30min, 1h, 2h, 4h, 8h. Bot5 (Lv 23)
    // cycled six mjolnir maps for a day on flat 30 min bans, every ban expired by the time the rotation came back around.
    // ponytail: the count never decays; cap at 8h so a bot that outlevels the map still gets to retry it the same day.
    private string BanMap(string map)
    {
        var count = _mapBanCount[map] = _mapBanCount.GetValueOrDefault(map) + 1;
        var minutes = Math.Min(30 * Math.Pow(2, count - 1), 480);
        _mapBannedUntil[map] = DateTime.UtcNow.AddMinutes(minutes);
        return Duration(minutes * 60);
    }
    private readonly Dictionary<string, int> _walkableCells = new(StringComparer.OrdinalIgnoreCase);
    // Per map, per kill: exp, predicted seconds by activity, and the respawn cap (exp/s the spawns can supply).
    private sealed record MapEstimate(double Rate, bool Safe, string Note, List<(string Name, int Count, bool Huntable, double HpLoss)> Monsters,
        double ExpPerKill, ActivityTime PerKill, double SupplyPerSecond, HuntZone? Zone = null);
    private Dictionary<string, MapEstimate> _mapEstimates = new(StringComparer.OrdinalIgnoreCase);
    private int _estimatesLevel = -1;
    private DateTime _nextMapCheck = DateTime.UtcNow.AddMinutes(2);
    private int _lastMapCheckLevel;
    private DateTime _lastMapCheckAt;

    private void NoteMapDeath(string map, bool onTheWay = false)
    {
        if (!_mapDeaths.TryGetValue(map, out var list)) _mapDeaths[map] = list = new();
        list.Add(DateTime.UtcNow);
        list.RemoveAll(t => DateTime.UtcNow - t > TimeSpan.FromMinutes(10));
        OnMemoryChanged?.Invoke();
        if (list.Count < 2 || !string.Equals(map, _config.HomeMap, StringComparison.OrdinalIgnoreCase)) return;
        var ban = BanMap(map);
        list.Clear();
        _nextMapCheck = DateTime.MinValue; // re-pick right away
        _retreatReason = (onTheWay ? $"died twice on the way to {map} in 10 min" : $"died twice on {map} in 10 min") + $", avoiding it for {ban}";
    }
    private string? _retreatReason;

    // Fleeing keeps a bot alive, so a map full of monsters it can't handle never trips the death rule; it just burns
    // Fly Wings (Bot2 on moc_pryd01, Bot1 on pay_fild07). Fleeing this often on the hunting map gets it banned too.
    private const int FleesToBan = 6;
    private readonly Dictionary<string, List<DateTime>> _mapFlees = new(StringComparer.OrdinalIgnoreCase);
    private void NoteMapFlee(string map)
    {
        if (!string.Equals(map, _config.HomeMap, StringComparison.OrdinalIgnoreCase)) return;
        if (!_mapFlees.TryGetValue(map, out var list)) _mapFlees[map] = list = new();
        list.Add(DateTime.UtcNow);
        list.RemoveAll(t => DateTime.UtcNow - t > TimeSpan.FromMinutes(10));
        OnMemoryChanged?.Invoke();
        if (list.Count < FleesToBan) return;
        // No ban: re-judge now on what the map really earns, flees included. Banning threw Bot4 off pay_fild07 (22.7k
        // measured, fleeing 0.9 s/kill) for a 14.8k guess 7 maps away. Deaths still ban (NoteMapDeath).
        list.Clear();
        _nextMapCheck = DateTime.MinValue;
        OnLog?.Invoke($"Map: fled {FleesToBan} times on {map} in 10 min, re-judging it on its measured exp/h.");
    }

    private void TickMapChoice(Snapshot snap)
    {
        if (_data == null || snap.SelfLevel <= 0) return;
        var now = DateTime.UtcNow;
        if (snap.SelfLevel != _estimatesLevel) RefreshEstimates(snap);

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


    // Forecasts move with gear, stats and potions, not just level, so every decision re-judges every map.
    private void RefreshEstimates(Snapshot snap)
    {
        _mapEstimates = EstimateMaps(snap);
        _estimatesLevel = snap.SelfLevel;    }

    private void DecideMap(Snapshot snap, DateTime now)
    {
        RefreshEstimates(snap);
        if (StaySummary() is { } stay) OnLog?.Invoke(stay);
        var home = _config.HomeMap;
        // The map we're on is judged only on what the bot really earned there. Its real/estimated ratio then scales
        // every other map's estimate, so they're on the same footing (raw estimates run several times too high).
        var measurements = MeasureMaps?.Invoke() ?? new();
        var (homeSeconds, homeExp, homeKills, homeTime, homeOther) = measurements.GetValueOrDefault(home);
        var homeMeasured = homeSeconds >= MeasureSeconds;
        var homeRate = homeMeasured ? homeExp / homeSeconds * 3600 : 0;
        _mapEstimates.TryGetValue(home, out var homeEstimateEntry);
        var homeEstimateRate = homeEstimateEntry?.Rate ?? 0;
        // Calibration only ever corrects estimates down. Beating the estimate is luck of that map's layout (anthell01's Ant Eggs
        // sit in one fixed cluster, so search time is near zero), not something that carries to other maps: carrying it inflated
        // every candidate and pulled Bot4 off a map earning 2.7x its old one after 3 minutes.
        var calibration = homeMeasured && homeEstimateRate > 0 ? Math.Clamp(homeRate / homeEstimateRate, 0.05, 1) : 1.0;
        // Better: correct each activity on its own. Measured seconds per kill vs predicted, per activity, on the map we're on.
        // Time not in any activity (shopping, dying) is a per-kill overhead every map pays. Fleeing is map-specific
        // (candidates with a monster that chases it are excluded anyway), so it's not carried over.
        var measuredPerKill = homeTime.Per(homeKills);
        var split = homeMeasured && homeKills >= 5 && homeTime.Total >= MeasureSeconds / 2 && homeEstimateEntry != null;
        static double Factor(double measured, double predicted) => predicted > 0.1 ? Math.Clamp(measured / predicted, 1, 5) : 1; // never faster than predicted (see calibration)
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
        var riskyRoute = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (map, estimate) in _mapEstimates)
        {
            if (!estimate.Safe && !string.Equals(map, home, StringComparison.OrdinalIgnoreCase)) continue;
            if (estimate.Rate <= 0 && !string.Equals(map, home, StringComparison.OrdinalIgnoreCase)) continue;
            var (hops, riskyStop) = string.Equals(map, snap.Map, StringComparison.OrdinalIgnoreCase) ? (0, null) : HopsTo(snap, map);
            if (hops < 0) continue;
            var isHome = string.Equals(map, home, StringComparison.OrdinalIgnoreCase);
            if (riskyStop != null && !isHome) riskyRoute[map] = $"route crosses {riskyStop} ({_mapEstimates[riskyStop].Note})";
            // A map the bot hunted recently is judged on what it really earned there, current map or not; otherwise a map
            // that measured badly looks good again as a candidate (its estimate) and the bot ping-pongs back to it.
            var (seconds, exp, mapKills, mapTime, mapOther) = measurements.GetValueOrDefault(map);
            var measured = isHome ? homeMeasured : seconds >= MeasureSeconds;
            var realization = isHome ? 1 : CandidateRealization;
            var modeledRate = (split ? PredictRate(estimate) : estimate.Rate * calibration) * realization;
            var rate = isHome && homeMeasured ? homeRate : measured ? exp / seconds * 3600 : modeledRate;
            var note = _mapBannedUntil.GetValueOrDefault(map) > now ? "avoiding: died or fled too often here"
                : riskyRoute.TryGetValue(map, out var risk) ? risk : estimate.Note;
            var perKill = isHome ? measuredPerKill : split ? Predict(estimate) : estimate.PerKill; // current map: measured only ("—" until then)
            var expectedPerKill = split ? Predict(estimate) : estimate.PerKill;
            var modeledSeconds = expectedPerKill.Total;
            options.Add(new MapOption(map, _data!.MapName(map), rate, measured, hops, note, estimate.Monsters, PerKill: perKill,
                ExpPerKill: estimate.ExpPerKill, HuntSecondsPerKill: modeledSeconds,
                OtherSecondsPerKill: split ? overheadPerKill : 0, SupplyExpPerHour: estimate.SupplyPerSecond * 3600,
                Calibration: (split ? 1 : calibration) * realization, MeasuredExp: exp, MeasuredSeconds: seconds,
                ModeledExpPerHour: modeledRate, ExpectedPerKill: expectedPerKill, MeasuredKills: mapKills,
                ActualPerKill: mapTime.Per(mapKills), ActualOtherSecondsPerKill: mapKills > 0 ? mapOther / mapKills : 0));
        }
        var current = options.FirstOrDefault(option => string.Equals(option.Map, home, StringComparison.OrdinalIgnoreCase));
        var currentSafe = homeEstimateEntry?.Safe == true;
        var banned = _mapBannedUntil.GetValueOrDefault(home) > now;
        // Away from the hunting map (respawned at the save point, back from shopping): is the way back still safe?
        var homeRiskyStop = string.Equals(snap.Map, home, StringComparison.OrdinalIgnoreCase) ? null : HopsTo(snap, home).RiskyStop;
        double Effective(MapOption option) => option.ExpPerHour * Math.Max(0, StayHorizonSeconds - option.Hops * SecondsPerHop) / StayHorizonSeconds;
        // Trapped: every way out crosses a map it loses on (Bot1 took the one-way Kafra to Yuno; at Lv 31 every exit field
        // but yuno_fild04 is deadly). Dying costs no exp on this server and respawns at the save point, so go anyway:
        // it either gets through or lands outside the trap.
        var away = options.Where(option => option.Hops > 0 && !string.Equals(option.Map, home, StringComparison.OrdinalIgnoreCase)).ToList();
        var trapped = away.Count > 0 && away.All(option => riskyRoute.ContainsKey(option.Map));
        var best = options.Where(option => !string.Equals(option.Map, home, StringComparison.OrdinalIgnoreCase)
                                      && _mapBannedUntil.GetValueOrDefault(option.Map) <= now && (trapped || !riskyRoute.ContainsKey(option.Map)))
                          .OrderByDescending(Effective).FirstOrDefault();
        var currentRate = current?.ExpPerHour ?? 0;
        CurrentMapOption = current ?? (_mapEstimates.TryGetValue(home, out var homeEstimate)
            ? new MapOption(home, _data!.MapName(home), 0, false, 0, homeEstimate.Note, homeEstimate.Monsters)
            : new MapOption(home, _data!.MapName(home), 0, false, 0, "no monsters spawn here", Array.Empty<(string, int, bool, double)>()));
        MinToChange = currentRate * MoveFactor;
        MapOptions = options.Where(option => !string.Equals(option.Map, home, StringComparison.OrdinalIgnoreCase))
            .Select(option => option with { VsCurrent = currentRate > 0 ? Effective(option) / currentRate : 0 })
            .OrderByDescending(option => option.VsCurrent).ThenByDescending(option => option.ExpPerHour).Take(8).ToList();
        var currentText = current == null ? $"{home}: nothing huntable at Lv {snap.SelfLevel}"
            : $"{home} measured {currentRate:N0} exp/h";

        string? reason = null;
        var measuring = current is not { Measured: true };
        if (measuring && currentSafe && _retreatReason == null && !banned && homeRiskyStop == null)
        {
            MapDecision = $"Staying on {home}. Reason: still measuring its real exp/h ({Duration(homeSeconds)} of {Duration(MeasureSeconds)} hunted here).";
            _nextMapCheck = now.AddMinutes(1); // decide as soon as the measurement is ready
            return;
        }
        if (best == null) { MapDecision = $"Staying on {home}. Reason: no other reachable map is safe at Lv {snap.SelfLevel}."; return; }
        var ratio = currentRate > 0 ? Effective(best) / currentRate : double.PositiveInfinity;
        var bestText = $"{best.Map} {(best.Measured ? "measured" : "estimated")} {best.ExpPerHour:N0} exp/h, {best.Hops} map(s) away"
                       + (_mapEstimates.GetValueOrDefault(best.Map)?.Zone != null ? $" ({_mapEstimates[best.Map].Note})" : "")
                       + (trapped && riskyRoute.TryGetValue(best.Map, out var trapNote) ? $"; trapped, every way out is risky ({trapNote}), going anyway: a death only respawns it at the save point, no exp lost" : "");
        if (_retreatReason != null || banned) reason = $"{_retreatReason ?? $"avoiding {home}"}; {bestText}";
        else if (homeRiskyStop != null) reason = $"the way back to {home} crosses {homeRiskyStop} ({_mapEstimates[homeRiskyStop].Note}); {bestText}";
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
        var predicted = best.Predicted();
        var left = current?.Actual();
        OnLog?.Invoke($"Map: predicted {best.Map}: " + (best.Measured ? $"measured {best.ExpPerHour:N0} exp/h" : predicted.Describe() + $" (× {best.Calibration:0.00} calibration)"));
        if (left is { Kills: > 0 }) OnLog?.Invoke($"Map: left {home}: actual {left.Describe()} over {left.Kills} kills, {Duration(left.Seconds)}.");
        OnMapMove?.Invoke(new MapMoveAudit(now, snap.SelfLevel, home, best.Map, reason, predicted, left, CandidateRealization));
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

    // Maps away by the route the bot would really take (only Kafras it can afford), and the first map on the way that has an
    // aggressive monster it loses to: Bot1 (606z, no Kafra to Geffen) died 8 times in 40 min crossing prt_fild11's Goblins
    // to reach gef_fild11, while gef_fild11 itself looked safe.
    private (int Hops, string? RiskyStop) HopsTo(Snapshot snap, string map)
    {
        var seed = _data!.World.EntryCell(map) is { } entry ? new RebuildSharedData.Data.Position(entry.X, entry.Y) : FindAnyWalkableCell(map);
        if (seed == null) return (-1, null);
        var plan = PlanRoute(snap.Map, snap.SelfPos.X, snap.SelfPos.Y, map, seed.Value.X, seed.Value.Y);
        if (plan == null) return (-1, null);
        var risky = plan.Edges.Select(e => e.DstMap).SkipLast(1)
            .FirstOrDefault(m => _mapEstimates.TryGetValue(m, out var stop) && !stop.Safe);
        return (plan.Edges.Count, risky);
    }

    /// <summary>Simulator exp/hour per spawn map at the bot's current stats. Unsafe = an aggressive regular monster
    /// spawns there that the bot loses to AND can't walk away from: faster than a player (Arclouse) or ranged (Kobold
    /// Archer). A slow melee one is just scenery the hazard system steers around: Bot1 crossed five yuno fields "unsafe"
    /// for slow monsters without a scratch. Bosses/MVPs are ignored: rare, and the hazard system steers around them.</summary>
    // ponytail: the server's base player walk (WorldObject.MoveSpeed); ignores Increase Agility and other speed buffs.
    private const float PlayerSecondsPerCell = 0.15f;
    private const int RangedMonsterRange = 3;
    // ponytail: one flat density; per-area (near the entrance / camp) if a sparse-overall map still kills on arrival.
    private const double CrowdedThreatsPer10kCells = 8; // spear reach (2) still has to walk up to you

    // Immobile ones (Geographer, Greatest General) only hit what walks into their small circle, which the hazard
    // overlay already routes around: counting them made ten yuno fields "unsafe" for Bot1.
    private static string? InescapableReason(RebuildSharedData.ClientTypes.MonsterDbEntry monster) =>
        IsMonsterImmobile(monster) ? null
        : monster.Range >= RangedMonsterRange ? $"ranged {monster.Range}"
        : monster.MoveSpeed > 0 && monster.MoveSpeed < PlayerSecondsPerCell ? "faster than you"
        : null;

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
            string? threat = null;
            var huntable = new List<(RebuildSharedData.ClientTypes.MonsterDbEntry Monster, int Count, double RespawnSeconds, BattleForecast Forecast)>();
            var tooStrong = new HashSet<string>();
            var slowThreats = new List<(string Name, int Count)>(); // aggressive, beats us, but slower than a player
            var hpLoss = new Dictionary<string, double>(); // forecast HP lost per fight, by monster name
            // Healers spread evenly: about count x (19x19 / walkable cells) of them within heal reach of any monster.
            var healArea = Math.Min(1.0, (2 * HealerRadius + 1) * (2 * HealerRadius + 1) / (double)cells);
            var mapHealing = spawns.Sum(spawn => spawn.Count * HealPerSecond(spawn.Monster)) * healArea;
            foreach (var (monster, count, respawnSeconds) in spawns)
            {
                if (string.Equals(monster.Special, "Boss", StringComparison.OrdinalIgnoreCase)) continue;
                var ownHeal = HealPerSecond(monster); // a healer heals itself too
                var healing = mapHealing - ownHeal * healArea + ownHeal;
                var forecast = WithHealing(Forecast(snap, new MonsterInfo(0, monster.Id, monster.Name, default, monster.HP, monster.HP)), monster.HP, healing);
                hpLoss[monster.Name] = Math.Max(hpLoss.GetValueOrDefault(monster.Name), forecast.ExpectedDamageTaken);
                // A slow monster is only scenery until it kills us: Bot3 died to Elder Willow (slower than a player) on
                // pay_fild07, moc_fild03 and pay_fild08 in a row, each map banned in turn while the next one still looked safe.
                var killedMe = _avoidClasses.TryGetValue(monster.Id, out var avoidUntil) && avoidUntil > DateTime.UtcNow;
                if (!forecast.CanWin || killedMe)
                {
                    tooStrong.Add(monster.Name);
                    if (IsAggressive(monster.Id) && (killedMe ? "killed me recently" : InescapableReason(monster)) is { } why) threat ??= $"{monster.Name} ({why})";
                    else if (IsAggressive(monster.Id) && !IsMonsterImmobile(monster)) slowThreats.Add((monster.Name, count));
                    continue;
                }
                huntable.Add((monster, count, respawnSeconds, forecast));
            }
            // Slow ones are scenery only while sparse: Bot5 (Lv 24) died 2 s into beach_dun2 to Tri Joint (20 Tri Joint +
            // 10 Neraid on 20k cells = 15 per 10k), and kept dying on mjolnir_02/05/10 (~20); the yuno fields Bot1 crossed are < 1.
            var slowCount = slowThreats.Sum(t => t.Count);
            if (threat == null && slowCount * 10000.0 / cells >= CrowdedThreatsPer10kCells)
                threat = $"{string.Join(", ", slowThreats.GroupBy(t => t.Name).Select(g => $"{g.Sum(t => t.Count)} {g.Key}"))} (crowded)";

            // Hunting `share` of each huntable spawn spread over `area` cells.
            (double Rate, double ExpPerKill, ActivityTime PerKill, double Supply) Hunt(Func<int, double> share, int area)
            {
                double exp = 0, fightSeconds = 0, recoverSeconds = 0, supplyPerSecond = 0, huntableCount = 0;
                for (var i = 0; i < huntable.Count; i++)
                {
                    var (monster, _, respawnSeconds, forecast) = huntable[i];
                    var count = share(i);
                    huntableCount += count;
                    exp += count * monster.Exp;
                    supplyPerSecond += count * monster.Exp / respawnSeconds; // a killed monster is gone until it respawns
                    fightSeconds += count * forecast.SecondsToKillMonster;
                    recoverSeconds += count * forecast.ExpectedDamageTaken / regen;
                }
                if (huntableCount <= 0) return default;
                // Walking to the next target: about the spacing between monsters we can beat (the rest are just scenery).
                var findSeconds = 0.15 * Math.Sqrt(area / huntableCount);
                var seconds = fightSeconds + recoverSeconds + huntableCount * findSeconds;
                // Exp/h is the slower of: how fast the bot kills, and how fast the area refills (7 Thief Bugs on a 6 min respawn run dry).
                return (Math.Min(exp / seconds, supplyPerSecond) * 3600, exp / huntableCount,
                    new ActivityTime(fightSeconds / huntableCount, findSeconds, recoverSeconds / huntableCount, 0), supplyPerSecond);
            }

            // Roam: everything spread over the whole map. Camp: a cluster of pinned spawns (anthell01's 87 Ant Eggs on a few
            // cells by the entrance) plus the free-roaming spawns' share of its cells. The map is worth its better option.
            var best = Hunt(i => huntable[i].Count, cells);
            HuntZone? bestZone = null;
            var totalByCode = huntable.GroupBy(h => h.Monster.Code).ToDictionary(g => g.Key, g => g.Sum(h => h.Count));
            foreach (var zone in SpawnZones(map, totalByCode))
            {
                var option = Hunt(i =>
                {
                    var (monster, count, _, _) = huntable[i];
                    var total = totalByCode[monster.Code];
                    var pinnedHere = zone.Counts.GetValueOrDefault(monster.Code);
                    var roaming = Math.Max(0, total - zone.PinnedTotal.GetValueOrDefault(monster.Code));
                    return count / (double)total * (pinnedHere + roaming * zone.Cells / (double)cells);
                }, zone.Cells);
                if (option.Rate > best.Rate) { best = option; bestZone = zone; }
            }
            var monsters = spawns.GroupBy(spawn => spawn.Monster.Name)
                .Select(group => (Name: group.Key, Count: group.Sum(spawn => spawn.Count), Huntable: !tooStrong.Contains(group.Key), HpLoss: hpLoss.GetValueOrDefault(group.Key)))
                .OrderByDescending(entry => entry.Huntable).ThenByDescending(entry => entry.Count).ToList();
            var note = threat != null ? $"aggressive {threat} too strong"
                : bestZone != null ? $"camp at ({(bestZone.MinX + bestZone.MaxX) / 2},{(bestZone.MinY + bestZone.MaxY) / 2}): {string.Join(", ", bestZone.Counts.Select(c => $"{c.Value} {_data.MonstersByCode.GetValueOrDefault(c.Key)?.Name ?? c.Key}"))}" : "";
            result[map] = new MapEstimate(best.Rate, threat == null, note, monsters,
                best.ExpPerKill, best.PerKill, best.Supply, bestZone);
        }
        snap.SelfHp = hpBefore;
        return result;
    }

    /// <summary>A cluster of pinned spawns worth camping: bounding box, walkable cells in it, pinned counts by monster code
    /// (PinnedTotal = the monster's pinned count anywhere on the map, so the rest of its count roams).</summary>
    private sealed record HuntZone(int MinX, int MinY, int MaxX, int MaxY, int Cells, Dictionary<string, int> Counts, Dictionary<string, int> PinnedTotal)
    {
        public bool Contains(int x, int y, int margin = 0) => x >= MinX - margin && x <= MaxX + margin && y >= MinY - margin && y <= MaxY + margin;
    }

    private const int ZoneMergeGap = 10; // pinned spawns this close together are one camp

    // Clusters the pinned spawns of the given monsters: rectangles closer than ZoneMergeGap merge into one box.
    private List<HuntZone> SpawnZones(string map, IReadOnlyDictionary<string, int> huntableByCode)
    {
        var areas = _data!.SpawnAreas(map).Where(a => huntableByCode.ContainsKey(a.MonsterCode)).ToList();
        if (areas.Count == 0) return new();
        var pinnedTotal = areas.GroupBy(a => a.MonsterCode).ToDictionary(g => g.Key, g => g.Sum(a => a.Count));
        var boxes = areas.Select(a => (a.MinX, a.MinY, a.MaxX, a.MaxY, Members: new List<GameDatabase.SpawnArea> { a })).ToList();
        for (var merged = true; merged;)
        {
            merged = false;
            for (var i = 0; i < boxes.Count && !merged; i++)
                for (var j = i + 1; j < boxes.Count && !merged; j++)
                {
                    var (a, b) = (boxes[i], boxes[j]);
                    if (b.MinX - a.MaxX > ZoneMergeGap || a.MinX - b.MaxX > ZoneMergeGap || b.MinY - a.MaxY > ZoneMergeGap || a.MinY - b.MaxY > ZoneMergeGap) continue;
                    a.Members.AddRange(b.Members);
                    boxes[i] = (Math.Min(a.MinX, b.MinX), Math.Min(a.MinY, b.MinY), Math.Max(a.MaxX, b.MaxX), Math.Max(a.MaxY, b.MaxY), a.Members);
                    boxes.RemoveAt(j);
                    merged = true;
                }
        }
        var walkMap = _data.GetWalkMap(map);
        return boxes.Select(box =>
        {
            var walkable = 0;
            for (var y = box.MinY; y <= box.MaxY; y++)
                for (var x = box.MinX; x <= box.MaxX; x++)
                    if (walkMap?.IsWalkable(x, y) != false) walkable++;
            var counts = box.Members.GroupBy(a => a.MonsterCode).ToDictionary(g => g.Key, g => g.Sum(a => a.Count));
            return new HuntZone(box.MinX, box.MinY, box.MaxX, box.MaxY, Math.Max(1, walkable), counts, pinnedTotal);
        }).ToList();
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
