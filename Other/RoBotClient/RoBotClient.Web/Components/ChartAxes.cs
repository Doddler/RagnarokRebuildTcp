using System.Globalization;

namespace RoBotClient.Web.Components;

/// <summary>Axis ticks and labels shared by the dashboard's SVG charts.</summary>
internal static class ChartAxes
{
    /// <summary>0-based "nice" ticks (1/2/5 × 10^k) covering max, about five of them.</summary>
    public static List<long> NiceTicks(long max)
    {
        max = Math.Max(max, 1);
        var raw = max / 4.0;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = (long)Math.Max(1, new[] { 1, 2, 5, 10 }.Select(m => m * mag).First(s => s >= raw));
        var ticks = new List<long>();
        for (long v = 0; ; v += step)
        {
            ticks.Add(v);
            if (v >= max) break;
        }
        return ticks;
    }

    /// <summary>Playtime ticks on round durations (1min ... 1 day), at most about six.</summary>
    public static List<double> TimeTicks(double from, double to)
    {
        var steps = new[] { 60, 300, 600, 900, 1800, 3600, 7200, 10800, 21600, 43200, 86400 };
        var step = steps.FirstOrDefault(s => (to - from) / s <= 6, 172800);
        var ticks = new List<double>();
        for (var t = Math.Ceiling(from / step) * step; t <= to; t += step) ticks.Add(t);
        return ticks;
    }

    /// <summary>Playtime as "3h15min" or "12min".</summary>
    public static string Duration(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h{t.Minutes:00}min" : $"{t.Minutes}min";
    }

    /// <summary>Axis value: in full under 10,000, then 12.5K / 1.2M.</summary>
    public static string Compact(long v) =>
        v >= 1_000_000 ? (v / 1_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "M"
        : v >= 10_000 ? (v / 1_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "K"
        : v.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>SVG coordinate, invariant culture.</summary>
    public static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Evenly thinned to at most <paramref name="max"/> items, always keeping the first and newest.</summary>
    public static List<T> Downsample<T>(IReadOnlyList<T> all, int max)
    {
        if (all.Count <= max) return all.ToList();
        var step = (all.Count - 1) / (double)(max - 1);
        return Enumerable.Range(0, max).Select(i => all[(int)Math.Round(i * step)]).ToList();
    }
}
