using RoBotClient.Bot.State;
using RoBotClient.Bot.Behavior;

namespace RoBotClient.Bot.Manager;

/// Time = seconds by activity during the run; TimedKills / TimedSeconds = the own kills and active seconds that split covers
/// (all zero for runs from before activity was tracked).
public sealed record MapRun(string Map, DateTime Start, DateTime End, TimeSpan Active, int Kills, long Exp, Dictionary<string, int> Items, bool Ongoing,
    ActivityTime Time = default, int TimedKills = 0, double TimedSeconds = 0);

/// <summary>Stays on one map, from persisted telemetry + progress history, so they survive restarts. Shared by the
/// dashboard's Runs table and the bot's map choice, so both show the same numbers.</summary>
public static class MapRuns
{
    // A run starts at each MapChange to a different map (re-entering the same map after a reconnect continues it).
    // Active time sums gaps between consecutive events. A quiet gap still counts (a bot waiting in an emptied room is
    // hunting there, just badly), unless it follows a Disconnect or runs past 15 min: then the bot was offline and
    // only 2 min count. Exp comes from the progress history over the run's wall-clock window. Runs under a minute are dropped (passing through).
    // ponytail: telemetry keeps the last 5000 events, so older runs drop off; persist runs if long history matters.
    public static List<MapRun> Compute(IReadOnlyList<TelemetryEvent> events, IReadOnlyList<ProgressSample> samples, bool inGame)
    {
        long ExpAt(DateTime t) => samples.LastOrDefault(p => p.Time <= t)?.TotalExp ?? samples.FirstOrDefault()?.TotalExp ?? 0;
        // Activity totals are running counters like exp; a run's split is end minus start. Untracked (old) samples count as zero.
        ActivityTime TimeAt(DateTime t) => samples.LastOrDefault(p => p.Time <= t)?.Activity ?? default;
        // The run's split, the own kills it covers (from the progress kill lines) and the active seconds it covers. A run
        // that began before tracking did counts only from the first tracked line (minus the activity that line already holds).
        (ActivityTime Time, int Kills, double Seconds) Tracked(DateTime from, DateTime to, TimeSpan active)
        {
            var (start, end) = (TimeAt(from), TimeAt(to));
            if (end.Total <= 0) return default;
            var first = samples.FirstOrDefault(p => p.Time > from && p.Activity != null);
            var since = start.Total > 0 || first == null ? from : first.Time.AddSeconds(-first.Activity!.Value.Total);
            var kills = samples.Count(p => p.Time > from && p.Time <= to && p.Monster != null && p.Activity != null);
            return (end - start, kills, Math.Min(active.TotalSeconds, (to - since).TotalSeconds));
        }
        MapRun Run(string map, DateTime from, DateTime to, TimeSpan active, int kills, Dictionary<string, int> items, bool ongoing)
        {
            var tracked = Tracked(from, to, active);
            return new MapRun(map, from, to, active, kills, Math.Max(0, ExpAt(to) - ExpAt(from)), items, ongoing, tracked.Time, tracked.Kills, tracked.Seconds);
        }

        var runs = new List<MapRun>();
        string? map = null;
        DateTime start = default, last = default;
        var active = TimeSpan.Zero;
        int kills = 0;
        var items = new Dictionary<string, int>();
        void Close(DateTime end, bool ongoing)
        {
            if (map == null || active.TotalMinutes < 1) return;
            // A hidden short stop (a detour, a respawn in town) shouldn't split one stay into two runs.
            if (runs.Count > 0 && string.Equals(runs[^1].Map, map, StringComparison.OrdinalIgnoreCase))
            {
                var previous = runs[^1];
                foreach (var (name, count) in previous.Items) items[name] = items.GetValueOrDefault(name) + count;
                runs[^1] = Run(map, previous.Start, end, previous.Active + active, previous.Kills + kills, items, ongoing);
            }
            else runs.Add(Run(map, start, end, active, kills, items, ongoing));
        }
        var offline = false;
        foreach (var e in events)
        {
            if (map != null)
            {
                var gap = (e.Time - last).TotalSeconds;
                active += TimeSpan.FromSeconds(offline || gap > 900 ? Math.Min(gap, 120) : gap);
            }
            last = e.Time;
            offline = e.Type == TelemetryEventType.Disconnect;
            if (e.Type == TelemetryEventType.MapChange && !string.Equals(e.Detail, map, StringComparison.OrdinalIgnoreCase))
            {
                Close(e.Time, false);
                (map, start, active, kills, items) = (e.Detail, e.Time, TimeSpan.Zero, 0, new Dictionary<string, int>());
            }
            else if (e.Type == TelemetryEventType.Kill) kills++;
            else if (e.Type == TelemetryEventType.Loot && e.Detail.Length > 0) items[e.Detail] = items.GetValueOrDefault(e.Detail) + Math.Max(1, e.Value);
        }
        if (map != null && inGame) active += DateTime.UtcNow - last; // online right now: the quiet tail counts in full
        Close(DateTime.UtcNow, inGame);
        runs.Reverse(); // newest first
        return runs;
    }
}
