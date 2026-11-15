namespace RoBotClient.Bot.GameData;

/// <summary>
/// A map's walkability grid, loaded from RebuildClient/Assets/Maps/exportdata/&lt;map&gt;.walk —
/// the same data the server's pathfinder uses. Layout: int32 width, int32 height, then width*height
/// cell bytes (row-major). A cell is walkable when its low bit is set.
/// </summary>
public sealed class WalkMap
{
    public int Width { get; }
    public int Height { get; }
    private readonly byte[] _cells;

    private static readonly int[] Dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] Dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };

    public WalkMap(int width, int height, byte[] cells)
    {
        Width = width;
        Height = height;
        _cells = cells;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool IsWalkable(int x, int y) => InBounds(x, y) && (_cells[x + y * Width] & 1) == 1;

    public static WalkMap? LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return null;
        using var br = new BinaryReader(File.OpenRead(path));
        var w = br.ReadInt32();
        var h = br.ReadInt32();
        var cells = br.ReadBytes(w * h);
        return new WalkMap(w, h, cells);
    }

    /// <summary>Returns a walkable cell at (x,y) or the nearest one within <paramref name="radius"/> rings.</summary>
    public bool TryFindWalkableNear(int x, int y, int radius, out int outX, out int outY)
    {
        if (IsWalkable(x, y)) { outX = x; outY = y; return true; }
        for (var r = 1; r <= radius; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            for (var dy = -r; dy <= r; dy++)
            {
                if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue; // ring perimeter only
                if (IsWalkable(x + dx, y + dy)) { outX = x + dx; outY = y + dy; return true; }
            }
        }
        outX = x; outY = y;
        return false;
    }

    /// <summary>
    /// A* over the walkability grid from (sx,sy) to a walkable goal (gx,gy). Returns the cell path
    /// (start..goal inclusive) or null if there's no walkable route. 8-directional, octile heuristic,
    /// no diagonal corner-cutting through walls.
    ///
    /// Optional <paramref name="extraCost"/> adds a per-cell non-negative cost on top of the step cost,
    /// so callers can bias the path away from danger zones (aggressive mob auras, portal cells) without
    /// excluding them outright — if there's no safer route, the path still gets through. The heuristic
    /// stays admissible because it underestimates with zero danger.
    ///
    /// Budget bump: when <paramref name="extraCost"/> is supplied, doubles the default ceiling, since a
    /// danger-aware search may detour around cells the plain version would walk through. Callers passing
    /// an explicit maxExpansions are unaffected.
    /// </summary>
    public List<(int x, int y)>? FindPath(int sx, int sy, int gx, int gy,
        int maxExpansions = 40000, Func<int, int, float>? extraCost = null)
    {
        if (!InBounds(sx, sy) || !IsWalkable(gx, gy)) return null;
        if (sx == gx && sy == gy) return new List<(int, int)> { (sx, sy) };

        var size = Width * Height;
        var cameFrom = new int[size];
        var gScore = new float[size];
        var closed = new bool[size];
        Array.Fill(cameFrom, -1);
        Array.Fill(gScore, float.MaxValue);

        var start = sx + sy * Width;
        var goal = gx + gy * Width;
        gScore[start] = 0f;
        var open = new PriorityQueue<int, float>();
        open.Enqueue(start, Heuristic(sx, sy, gx, gy));
        var expansions = 0;

        while (open.Count > 0 && expansions++ < maxExpansions)
        {
            var cur = open.Dequeue();
            if (cur == goal) break;
            if (closed[cur]) continue;
            closed[cur] = true;
            var cx = cur % Width;
            var cy = cur / Width;
            for (var k = 0; k < 8; k++)
            {
                var nx = cx + Dx8[k];
                var ny = cy + Dy8[k];
                if (!IsWalkable(nx, ny)) continue;
                if (Dx8[k] != 0 && Dy8[k] != 0 && (!IsWalkable(cx + Dx8[k], cy) || !IsWalkable(cx, cy + Dy8[k])))
                    continue; // don't cut diagonally through a wall corner
                var ni = nx + ny * Width;
                if (closed[ni]) continue;
                var step = (Dx8[k] != 0 && Dy8[k] != 0) ? 1.41f : 1f;
                var danger = extraCost != null ? Math.Max(0f, extraCost(nx, ny)) : 0f;
                var tentative = gScore[cur] + step + danger;
                if (tentative < gScore[ni])
                {
                    gScore[ni] = tentative;
                    cameFrom[ni] = cur;
                    open.Enqueue(ni, tentative + Heuristic(nx, ny, gx, gy));
                }
            }
        }

        if (start != goal && cameFrom[goal] == -1) return null; // goal unreachable

        var path = new List<(int, int)>();
        var node = goal;
        var guard = 0;
        while (node != -1 && guard++ < size)
        {
            path.Add((node % Width, node / Width));
            node = cameFrom[node];
        }
        path.Reverse();
        return path.Count > 0 && path[0].Item1 == sx && path[0].Item2 == sy ? path : null;
    }

    private static float Heuristic(int x, int y, int gx, int gy)
    {
        var dx = Math.Abs(x - gx);
        var dy = Math.Abs(y - gy);
        return Math.Max(dx, dy) + 0.41f * Math.Min(dx, dy);
    }
}
