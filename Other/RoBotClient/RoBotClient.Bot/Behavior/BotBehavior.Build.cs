using RebuildSharedData.Enum;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Behavior;

// Build: spend stat and skill points toward the config's StatTargets / SkillOrder, one point per step.
public sealed partial class BotBehavior
{
    private static readonly string[] StatNames = { "STR", "AGI", "VIT", "INT", "DEX", "LUK" };
    private DateTime _nextBuildSpend = DateTime.MinValue;

    /// <summary>Points to raise a stat from <paramref name="current"/> to current+1. Matches the server's
    /// cumulativeStatPointCost table (Player.cs): 2 per point up to 11, then +1 every 10.</summary>
    public static int StatRaiseCost(int current) => Math.Max(current - 1, 0) / 10 + 2;

    /// <summary>Index (0..5) of the stat the build raises next, or null when every goal is met. Picks the stat
    /// furthest behind proportionally (lowest current/goal), so goals 40 STR / 70 AGI grow together at 4:7
    /// instead of AGI soaking up every point first. Ties go to the bigger goal.</summary>
    public static int? NextStat(int[] current, int[] targets)
    {
        int? best = null;
        for (var i = 0; i < 6; i++)
        {
            if (targets[i] <= current[i]) continue;
            if (best is not { } b) { best = i; continue; }
            var cmp = ((double)current[i] / targets[i]).CompareTo((double)current[b] / targets[b]);
            if (cmp < 0 || (cmp == 0 && targets[i] > targets[b])) best = i;
        }
        return best;
    }

    /// <summary>Parses "Bash 10, Magnum Break 3" into (skill, level) pairs; unknown names are skipped.</summary>
    public static List<(CharacterSkill Skill, int Level)> ParseSkillOrder(string order)
    {
        var result = new List<(CharacterSkill, int)>();
        foreach (var part in (order ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var words = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var level = words.Length > 1 && int.TryParse(words[^1], out var l) ? l : 1;
            var name = string.Concat(int.TryParse(words[^1], out _) ? words[..^1] : words);
            if (Enum.TryParse<CharacterSkill>(name, true, out var skill)) result.Add((skill, level));
        }
        return result;
    }

    private async Task TickBuildAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow < _nextBuildSpend) return;
        _nextBuildSpend = DateTime.UtcNow.AddSeconds(1); // server confirms via a stat update; one point per second is plenty

        var (stats, statPoints) = _bot.WithState(w => (new[] { w.Self.Str, w.Self.Agi, w.Self.Vit, w.Self.Int, w.Self.Dex, w.Self.Luk }, w.Self.StatPoints));
        if (statPoints > 0 && _config.StatTargets is { Length: 6 } targets && NextStat(stats, targets) is { } i
            && statPoints >= StatRaiseCost(stats[i]))
        {
            var deltas = new int[6];
            deltas[i] = 1;
            OnLog?.Invoke($"Build: {StatNames[i]} {stats[i]} -> {stats[i] + 1} (goal {targets[i]}).");
            await _bot.ApplyStatPointsAsync(deltas, ct);
            return;
        }

        var skill = _bot.WithState(NextBuildSkill);
        if (skill is { } s)
        {
            OnLog?.Invoke($"Build: learning {_data?.SkillInfo(s)?.Name ?? s.ToString()}.");
            await _bot.ApplySkillPointAsync(s, ct);
        }
    }

    /// <summary>First SkillOrder entry the current job can learn now (prerequisites met, below its goal).</summary>
    private CharacterSkill? NextBuildSkill(WorldState w)
    {
        if (w.Self.SkillPoints <= 0 || _data == null || string.IsNullOrWhiteSpace(_config.SkillOrder)) return null;
        var learned = new Dictionary<CharacterSkill, int>();
        foreach (var k in w.Self.KnownSkills) learned[k.Skill] = Math.Max(k.Level, learned.GetValueOrDefault(k.Skill));
        var tree = new Dictionary<CharacterSkill, RebuildSharedData.ClientTypes.ClientSkillTreeEntry>();
        foreach (var e in _data.LearnableSkills(w.Self.JobId)) tree.TryAdd(e.Skill, e);
        foreach (var (skill, level) in ParseSkillOrder(_config.SkillOrder))
        {
            var goal = Math.Min(level, _data.SkillInfo(skill)?.MaxLevel ?? level);
            if (learned.GetValueOrDefault(skill) >= goal || !tree.TryGetValue(skill, out var entry)) continue;
            if ((entry.Prerequisites ?? []).All(p => learned.GetValueOrDefault(p.Skill) >= p.Level)) return skill;
        }
        return null;
    }
}
