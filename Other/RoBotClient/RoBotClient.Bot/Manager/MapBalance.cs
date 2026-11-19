using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Manager;

/// <summary>One map, judged across every bot that hunted there (rates per active hour of hunting, time-weighted over stays).
/// LevelsPerHour is exp/h over the exp the bots' level needed, so maps of different level bands compare on one scale.
/// Peers are the other maps hunted at a similar level (median level ±PeerBand): PeerRatio is this map's leveling speed over
/// theirs (1 = fair, null when no peer has enough hunting), PeerDeathsPerHour their death rate.</summary>
public sealed record MapBalanceRow(string Map, int Bots, int Stays, double Hours, int MinLevel, int MaxLevel, int MedianLevel,
    double ExpPerHour, double LevelsPerHour, double? PeerRatio, int PeerMaps,
    double ZenyPerHour, double? KillSeconds, double? SearchShare, int Deaths, double? PeerDeathsPerHour)
{
    public double DeathsPerHour => Hours > 0 ? Deaths / Hours : 0;
    public double? MinutesPerLevel => LevelsPerHour > 0 ? 60 / LevelsPerHour : null;
}

public static class MapBalance
{
    public const int PeerBand = 5;
    private const double MinPeerHours = 0.25;

    public sealed record Character(string Name, IReadOnlyList<TelemetryEvent> Events, IReadOnlyList<ProgressSample> Samples, bool InGame);

    private sealed record Stay(string Bot, string Map, int Level, double Hours, long Exp, double LevelsPerHour, int Deaths,
        List<ProgressSample> Kills, long LootValue, double Search, double Tracked);

    /// <summary>Only maps a bot hunted on (a stay with a kill) appear; towns and fields walked through don't. Busiest first.
    /// itemValue: zeny an NPC pays for one of the named item (0 when unknown).</summary>
    public static List<MapBalanceRow> Compute(IEnumerable<Character> characters, Func<int, int> expToNext, Func<string, int> itemValue)
    {
        var stays = new List<Stay>();
        foreach (var c in characters)
        {
            var deaths = c.Events.Where(e => e.Type == TelemetryEventType.Death).Select(e => e.Time).ToList();
            foreach (var run in MapRuns.Compute(c.Events, c.Samples, c.InGame))
            {
                var hours = run.Active.TotalHours;
                if (hours <= 0 || run.Kills == 0) continue;
                var inRun = c.Samples.Where(p => p.Time > run.Start && p.Time <= run.End).ToList();
                var level = inRun.Count > 0 ? inRun[inRun.Count / 2].Level : c.Samples.LastOrDefault(p => p.Time <= run.Start)?.Level ?? 0;
                var need = expToNext(level);
                stays.Add(new Stay(c.Name, run.Map, level, hours, run.Exp, need > 0 ? run.Exp / (double)need / hours : 0,
                    deaths.Count(t => t > run.Start && t <= run.End),
                    inRun.Where(p => p.Monster != null).ToList(),
                    run.Items.Sum(kv => (long)itemValue(kv.Key) * kv.Value),
                    run.Time.Search, run.Time.Total));
            }
        }

        var byMap = stays.GroupBy(s => s.Map, StringComparer.OrdinalIgnoreCase).ToList();
        return byMap.Select(g =>
        {
            var hours = g.Sum(s => s.Hours);
            var levels = g.Where(s => s.Level > 0).Select(s => s.Level).OrderBy(l => l).DefaultIfEmpty(0).ToList();
            var median = levels[levels.Count / 2];
            var peers = stays.Where(s => !string.Equals(s.Map, g.Key, StringComparison.OrdinalIgnoreCase) && Math.Abs(s.Level - median) <= PeerBand).ToList();
            var peerHours = peers.Sum(s => s.Hours);
            var speed = g.Sum(s => s.LevelsPerHour * s.Hours) / hours;
            var peerSpeed = peerHours >= MinPeerHours ? peers.Sum(s => s.LevelsPerHour * s.Hours) / peerHours : 0;
            var ttk = g.SelectMany(s => s.Kills).Where(k => k.TimeToKill is > 0).Select(k => k.TimeToKill!.Value).OrderBy(t => t).ToList();
            var tracked = g.Sum(s => s.Tracked);
            return new MapBalanceRow(g.Key, g.Select(s => s.Bot).Distinct().Count(), g.Count(), hours, levels[0], levels[^1], median,
                g.Sum(s => s.Exp) / hours, speed, peerSpeed > 0 ? speed / peerSpeed : null,
                peerHours >= MinPeerHours ? peers.Select(s => s.Map).Distinct(StringComparer.OrdinalIgnoreCase).Count() : 0,
                g.Sum(s => s.LootValue) / hours, ttk.Count > 0 ? ttk[ttk.Count / 2] : null,
                tracked > 0 ? g.Sum(s => s.Search) / tracked : null,
                g.Sum(s => s.Deaths), peerHours >= MinPeerHours ? peers.Sum(s => s.Deaths) / peerHours : null);
        }).OrderByDescending(r => r.Hours).ToList();
    }
}
