using System.Text.RegularExpressions;

namespace RoBotClient.Bot.GameData;

/// <summary>
/// Region-aware world pathfinder. Every map's walkable cells are partitioned into connected
/// components (regions) via 8-connected flood-fill with no-corner-cutting (same rule as A*). Each portal
/// becomes a directed edge between <c>(srcMap, srcRegion)</c> and <c>(destMap, destRegion)</c> with the
/// source cell on map M and the landing cell on the destination map both recorded.
///
/// Built once at boot from:
///   * The walk grids the bot already loads from <c>RebuildClient/Assets/Maps/exportdata/</c>.
///   * The warp scripts at <c>RoRebuildServer/GameConfig/ServerData/Script/Warps/**/*.txt</c>.
///     These carry BOTH source AND destination cells in the same Warp() call; the existing
///     mapwarps.json export throws the destination away, so we parse the .txt files directly.
///
/// Query: <see cref="NextPortalToward"/> returns the source-side cell of the next portal to walk to,
/// or null if no path exists. Uses Dijkstra over the region graph with strict progress so a bot in a
/// sub-region with no forward portal returns null cleanly instead of cycling.
/// </summary>
public sealed class WorldGraph
{
    // ---- nodes ----

    /// <summary>One node = one connected walkable region on one map.</summary>
    private sealed class Node
    {
        public string Map = "";
        public ushort Region;
        public List<Edge> Out = new();
    }

    /// <summary>A portal from one region to another, with both endpoints' cells. When <see cref="Kafra"/>
    /// is non-null this isn't a walkable portal — it's a paid teleport via a kafra NPC. The (Sx, Sy)
    /// is then the NPC's cell (where the bot must walk to click); the bot drives the dialog to reach
    /// the destination instead of crossing a portal tile.</summary>
    public sealed record Edge(string SrcMap, ushort SrcRegion, int Sx, int Sy,
                              string DstMap, ushort DstRegion, int Dx, int Dy,
                              KafraEdgeInfo? Kafra = null);

    /// <summary>Per-edge data for kafra teleport edges: which menu option to pick in the kafra's first
    /// menu (e.g. 2 for "Teleport Service" on @Kafra NPCs), which option in the destination menu, and
    /// the zeny cost (informational — currently free on this server, but kept for future-proofing and
    /// for the bot to refuse routes it can't afford).</summary>
    public sealed record KafraEdgeInfo(int TopMenuOption, int DestOptionIndex, int Zeny, string OptionLabel);

    /// <summary>Cached query result — the ordered list of portal edges from a (map,cell) to a (map,cell).</summary>
    public sealed record TravelPlan(IReadOnlyList<Edge> Edges, float Cost);

    // ---- storage ----

    private readonly Dictionary<(string map, ushort region), Node> _nodes =
        new(EqualityComparer<(string, ushort)>.Default);
    // Per-map: regionId[x + y*width] (0 = unwalkable, 1..N = region id). Lazily filled by EnsureRegions.
    private readonly Dictionary<string, ushort[]> _regionGrid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int w, int h)> _mapDims = new(StringComparer.OrdinalIgnoreCase);
    // All portals declared in the warp .txt files, indexed by source map for fast lookup.
    private readonly Dictionary<string, List<Edge>> _edgesBySrcMap =
        new(StringComparer.OrdinalIgnoreCase);
    // Real portal footprints per map: (minX, minY, maxX, maxY) — inclusive — derived from Warp() w/h
    // (which are half-extents on the server side). Stepping ANY cell within one of these rectangles
    // triggers the warp; the pathfinder needs to avoid them all, not just the center cell. Distinct
    // from the legacy IsNearPortal(center, d) which only knows distance-to-center.
    private readonly Dictionary<string, List<(int minX, int minY, int maxX, int maxY)>> _portalFootprints =
        new(StringComparer.OrdinalIgnoreCase);

    public int NodeCount => _nodes.Count;
    public int EdgeCount => _edgesBySrcMap.Values.Sum(l => l.Count);
    public bool IsBuilt => _nodes.Count > 0;
    public IReadOnlyDictionary<string, List<Edge>> EdgesBySrcMap => _edgesBySrcMap;

    // ---- parser ----

    // Warp("src", "id", [optional "displayName",] sx, sy, sw, sh, "dst", dx, dy [, dw, dh])
    // Source AND destination cells are both right there in the call. The (sw, sh) values are HALF-EXTENTS
    // (see RebuildSharedData/Data/Area.cs CreateAroundPoint: Area(x-w, y-h, x+w, y+h)), so the actual
    // warp footprint on the source side is (sx-sw..sx+sw, sy-sh..sy+sh) — capture them; the bot's
    // pathfinder needs the real footprint to know which cells trigger a warp.
    private static readonly Regex WarpPattern = new(
        @"\bWarp\(\s*""(?<src>[^""]+)""\s*,\s*""[^""]+""\s*,\s*" +
        @"(?:""[^""]+""\s*,\s*)?" +
        @"(?<sx>-?\d+)\s*,\s*(?<sy>-?\d+)\s*,\s*(?<sw>-?\d+)\s*,\s*(?<sh>-?\d+)\s*,\s*" +
        @"""(?<dst>[^""]+)""\s*,\s*(?<dx>-?\d+)\s*,\s*(?<dy>-?\d+)",
        RegexOptions.Compiled);

    // Match a // line comment from `//` to end-of-line. Used to strip disabled warps before regex
    // matches them as live portals.
    private static readonly Regex LineCommentPattern = new(@"//[^\r\n]*", RegexOptions.Compiled);

    /// <summary>Parse every Warps/**/*.txt under <paramref name="warpScriptDir"/> into raw edges. Strips
    /// // line comments first so commented-out Warp() lines are skipped (some maps have legacy
    /// disabled portals — e.g. <c>//Warp("izlude", "welcome_arena", …)</c>). The (srcRegion, dstRegion)
    /// fields are filled later in <see cref="Build"/> after walkmaps are computed. <c>sw/sh</c> are the
    /// half-extents — the actual warp footprint is (sx±sw, sy±sh).</summary>
    private List<(string src, int sx, int sy, int sw, int sh, string dst, int dx, int dy)> ParseWarpFiles(string warpScriptDir)
    {
        var list = new List<(string, int, int, int, int, string, int, int)>();
        if (!Directory.Exists(warpScriptDir)) return list;
        foreach (var file in Directory.EnumerateFiles(warpScriptDir, "*.txt", SearchOption.AllDirectories))
        {
            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }
            // Strip // line comments so disabled warps don't get parsed as live portals.
            var stripped = LineCommentPattern.Replace(text, "");
            foreach (Match m in WarpPattern.Matches(stripped))
            {
                var src = m.Groups["src"].Value;
                var dst = m.Groups["dst"].Value;
                if (string.Equals(src, dst, StringComparison.OrdinalIgnoreCase)) continue; // same-map warps are intra-region
                var sx = int.Parse(m.Groups["sx"].Value);
                var sy = int.Parse(m.Groups["sy"].Value);
                var sw = int.Parse(m.Groups["sw"].Value);
                var sh = int.Parse(m.Groups["sh"].Value);
                var dx = int.Parse(m.Groups["dx"].Value);
                var dy = int.Parse(m.Groups["dy"].Value);
                list.Add((src, sx, sy, sw, sh, dst, dx, dy));
            }
        }
        return list;
    }

    // ---- region computation ----

    /// <summary>Compute the connected-components label grid for one map using 8-connected flood-fill
    /// with the no-diagonal-corner-cutting rule (matches WalkMap.FindPath neighborhood). Cell value 0 =
    /// unwalkable; 1..N = region id. Mutates state for this <paramref name="map"/> only.</summary>
    private void EnsureRegions(string map, WalkMap walk)
    {
        if (_regionGrid.ContainsKey(map)) return;
        var w = walk.Width;
        var h = walk.Height;
        var grid = new ushort[w * h];
        var stack = new Stack<(int x, int y)>();
        ushort next = 1;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (grid[x + y * w] != 0) continue;          // already labeled
                if (!walk.IsWalkable(x, y)) continue;        // unwalkable cells stay at 0
                // New region — flood-fill from this seed.
                var label = next++;
                grid[x + y * w] = label;
                stack.Push((x, y));
                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop();
                    for (var k = 0; k < 8; k++)
                    {
                        var dx = Dx8[k];
                        var dy = Dy8[k];
                        var nx = cx + dx;
                        var ny = cy + dy;
                        if (!walk.IsWalkable(nx, ny)) continue;
                        if (dx != 0 && dy != 0 && (!walk.IsWalkable(cx + dx, cy) || !walk.IsWalkable(cx, cy + dy)))
                            continue;
                        var idx = nx + ny * w;
                        if (grid[idx] != 0) continue;
                        grid[idx] = label;
                        stack.Push((nx, ny));
                    }
                }
            }
        }
        _regionGrid[map] = grid;
        _mapDims[map] = (w, h);
    }

    private static readonly int[] Dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] Dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };

    /// <summary>True if (x,y) on <paramref name="map"/> is inside the rectangular footprint of ANY warp
    /// on that map — i.e. stepping onto this cell triggers a map change. This is the authoritative
    /// "would I warp here?" check the pathfinder must use; the legacy <c>IsNearPortal(center, d)</c>
    /// only knows distance-to-center and silently misses cells on the long edge of a fat portal
    /// (e.g. pay_fild01's pay_fild07 portal has half-extents 2×6, so the footprint extends 6 tiles
    /// south of the center — well outside any "within 1 tile" check). Optional <paramref name="margin"/>
    /// adds a safety buffer in cells around the rectangle.</summary>
    /// <summary>Enumerate every warp footprint on <paramref name="map"/> as inclusive AABBs. Returns an
    /// empty sequence when the map has no warps or the graph isn't built. Used by per-cell A* cost
    /// closures that need to score distance-to-footprint rather than distance-to-center.</summary>
    public IReadOnlyList<(int minX, int minY, int maxX, int maxY)> PortalFootprintsOn(string map)
    {
        return _portalFootprints.TryGetValue(map, out var list)
            ? list
            : (IReadOnlyList<(int, int, int, int)>)Array.Empty<(int, int, int, int)>();
    }

    public bool IsInPortalFootprint(string map, int x, int y, int margin = 0)
    {
        if (!_portalFootprints.TryGetValue(map, out var list)) return false;
        for (var i = 0; i < list.Count; i++)
        {
            var f = list[i];
            if (x >= f.minX - margin && x <= f.maxX + margin &&
                y >= f.minY - margin && y <= f.maxY + margin)
                return true;
        }
        return false;
    }

    /// <summary>Region id at (x,y) on <paramref name="map"/>, or 0 if unwalkable / unknown.</summary>
    public ushort RegionAt(string map, int x, int y)
    {
        if (!_regionGrid.TryGetValue(map, out var grid)) return 0;
        var (w, h) = _mapDims[map];
        if (x < 0 || y < 0 || x >= w || y >= h) return 0;
        return grid[x + y * w];
    }

    // ---- build ----

    /// <summary>Build the full graph. <paramref name="getWalkMap"/> is a callback to load a map's
    /// walk grid lazily — typically <c>GameDatabase.GetWalkMap</c>. <paramref name="knownMaps"/> is the
    /// allowlist of maps the server actually has registered (typically the keys of <c>maps.json</c>);
    /// edges referencing source OR destination maps NOT in this set are dropped. This is how we prune
    /// warps to maps like <c>pay_gld</c> that exist in the legacy warp scripts but aren't loaded on
    /// this server build. Pass <c>null</c> to disable the filter (every warp is accepted).</summary>
    public void Build(string warpScriptDir, Func<string, WalkMap?> getWalkMap, ISet<string>? knownMaps = null,
        IReadOnlyList<KafraEntry>? kafras = null)
    {
        _nodes.Clear();
        _regionGrid.Clear();
        _mapDims.Clear();
        _edgesBySrcMap.Clear();
        _portalFootprints.Clear();

        var rawWarps = ParseWarpFiles(warpScriptDir);
        if (rawWarps.Count == 0) return;

        // Drop warps whose source or destination map is unknown to the server.
        var dropped = 0;
        if (knownMaps != null)
        {
            var filtered = new List<(string src, int sx, int sy, int sw, int sh, string dst, int dx, int dy)>(rawWarps.Count);
            foreach (var w in rawWarps)
            {
                if (knownMaps.Contains(w.src) && knownMaps.Contains(w.dst)) filtered.Add(w);
                else dropped++;
            }
            rawWarps = filtered;
        }

        // Collect every map mentioned (source OR destination) so we compute regions for all of them.
        var maps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in rawWarps) { maps.Add(w.src); maps.Add(w.dst); }
        // Also pull in maps referenced only by kafra warps, so their regions are computed for edge resolution.
        if (kafras != null)
        {
            foreach (var k in kafras)
            {
                if (knownMaps != null && !knownMaps.Contains(k.Map)) continue;
                maps.Add(k.Map);
                foreach (var w in k.Warps)
                    if (knownMaps == null || knownMaps.Contains(w.DestMap))
                        maps.Add(w.DestMap);
            }
        }
        foreach (var m in maps)
        {
            var walk = getWalkMap(m);
            if (walk != null) EnsureRegions(m, walk);
        }

        // Resolve every warp into a directed Edge with both endpoint regions filled. Also record the
        // real footprint (center ± half-extent) so the pathfinder knows which cells trigger a warp.
        foreach (var (src, sx, sy, sw, sh, dst, dx, dy) in rawWarps)
        {
            var sr = RegionAt(src, sx, sy);
            var dr = RegionAt(dst, dx, dy);
            // If we have a walkmap and the cell isn't walkable, try the 4-cell ring for a walkable seed
            // (the portal "footprint" can include unwalkable trim).
            if (sr == 0) sr = FindNearbyRegion(src, sx, sy, 4);
            if (dr == 0) dr = FindNearbyRegion(dst, dx, dy, 4);
            // Optimistic fallback if a map has no walkmap loaded — every cell is "region 1".
            if (sr == 0 && !_regionGrid.ContainsKey(src)) sr = 1;
            if (dr == 0 && !_regionGrid.ContainsKey(dst)) dr = 1;
            if (sr == 0 || dr == 0) continue; // genuinely off-grid portal — skip

            var edge = new Edge(src, sr, sx, sy, dst, dr, dx, dy);
            if (!_edgesBySrcMap.TryGetValue(src, out var bucket))
                _edgesBySrcMap[src] = bucket = new List<Edge>();
            bucket.Add(edge);

            var srcNode = GetOrCreateNode(src, sr);
            srcNode.Out.Add(edge);
            // Pre-create the destination node so Dijkstra doesn't have to alloc one on first visit.
            GetOrCreateNode(dst, dr);

            // Record the actual warp footprint. Area.CreateAroundPoint(pos, w, h) makes a rectangle
            // (x-w..x+w, y-h..y+h) — i.e. (2w+1) × (2h+1) cells. ANY cell in that rectangle triggers
            // the warp when a player steps onto it.
            if (!_portalFootprints.TryGetValue(src, out var foots))
                _portalFootprints[src] = foots = new List<(int, int, int, int)>();
            foots.Add((sx - sw, sy - sh, sx + sw, sy + sh));
        }

        // Add kafra edges. Each kafra NPC sits in a specific region of its map; from that region the
        // bot can take ANY of the kafra's destinations (after clicking + driving the menu). One Edge
        // per (kafra, warp) so Dijkstra picks the right destination organically.
        var kafraEdgeCount = 0;
        if (kafras != null)
        {
            foreach (var k in kafras)
            {
                if (knownMaps != null && !knownMaps.Contains(k.Map)) continue;
                var kr = RegionAt(k.Map, k.X, k.Y);
                if (kr == 0) kr = FindNearbyRegion(k.Map, k.X, k.Y, 4);
                if (kr == 0 && !_regionGrid.ContainsKey(k.Map)) kr = 1;
                if (kr == 0) continue;

                var srcNode = GetOrCreateNode(k.Map, kr);
                foreach (var w in k.Warps)
                {
                    if (knownMaps != null && !knownMaps.Contains(w.DestMap)) continue;
                    var dr2 = RegionAt(w.DestMap, w.DestX, w.DestY);
                    if (dr2 == 0) dr2 = FindNearbyRegion(w.DestMap, w.DestX, w.DestY, 4);
                    if (dr2 == 0 && !_regionGrid.ContainsKey(w.DestMap)) dr2 = 1;
                    if (dr2 == 0) continue;

                    var edge = new Edge(k.Map, kr, k.X, k.Y, w.DestMap, dr2, w.DestX, w.DestY,
                        new KafraEdgeInfo(k.TopMenuOption, w.OptionIndex, w.Zeny, w.OptionLabel));
                    if (!_edgesBySrcMap.TryGetValue(k.Map, out var bucket))
                        _edgesBySrcMap[k.Map] = bucket = new List<Edge>();
                    bucket.Add(edge);
                    srcNode.Out.Add(edge);
                    GetOrCreateNode(w.DestMap, dr2);
                    kafraEdgeCount++;
                }
            }
        }

        LastBuildDroppedUnknownMaps = dropped;
        LastBuildKafraEdges = kafraEdgeCount;
    }

    /// <summary>How many kafra-warp edges were added during the last <see cref="Build"/>. Exposed via
    /// MCP world_graph_status for diagnostics.</summary>
    public int LastBuildKafraEdges { get; private set; }

    /// <summary>How many raw warps were dropped during the last <see cref="Build"/> because their
    /// source or destination map wasn't in the allowlist. Exposed for diagnostics — surface this via
    /// MCP to see if the prune count looks sane.</summary>
    public int LastBuildDroppedUnknownMaps { get; private set; }

    private ushort FindNearbyRegion(string map, int x, int y, int radius)
    {
        if (!_regionGrid.TryGetValue(map, out var grid)) return 0;
        var (w, h) = _mapDims[map];
        for (var r = 1; r <= radius; r++)
        {
            for (var dx = -r; dx <= r; dx++)
            for (var dy = -r; dy <= r; dy++)
            {
                if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                var v = grid[nx + ny * w];
                if (v != 0) return v;
            }
        }
        return 0;
    }

    private Node GetOrCreateNode(string map, ushort region)
    {
        var key = (map, region);
        if (!_nodes.TryGetValue(key, out var n))
            _nodes[key] = n = new Node { Map = map, Region = region };
        return n;
    }

    // ---- queries ----

    /// <summary>Plan a route from a cell on the start map to a cell on the destination map. Returns the
    /// ordered list of portal edges to traverse, or null if no such route exists in the graph.</summary>
    /// <summary>Plan a route. <paramref name="edgeFilter"/>, when non-null, is called for every candidate
    /// edge — returning false excludes it from the search. Used by the bot to skip kafra edges whose
    /// dialog timed out repeatedly (so Dijkstra picks portal-walking instead of looping on a broken
    /// kafra) without rebuilding the whole graph.</summary>
    public TravelPlan? Plan(string fromMap, int fromX, int fromY, string toMap, int toX, int toY,
        Func<Edge, bool>? edgeFilter = null)
    {
        if (!IsBuilt) return null;
        var srcRegion = RegionAt(fromMap, fromX, fromY);
        if (srcRegion == 0) srcRegion = FindNearbyRegion(fromMap, fromX, fromY, 4);
        if (srcRegion == 0 && !_regionGrid.ContainsKey(fromMap)) srcRegion = 1; // optimistic fallback
        var goalRegion = RegionAt(toMap, toX, toY);
        if (goalRegion == 0) goalRegion = FindNearbyRegion(toMap, toX, toY, 4);
        if (goalRegion == 0 && !_regionGrid.ContainsKey(toMap)) goalRegion = 1;
        if (srcRegion == 0 || goalRegion == 0) return null;

        var src = (fromMap, srcRegion);
        var goal = (toMap, goalRegion);
        if (src.Equals(goal)) return new TravelPlan(Array.Empty<Edge>(), 0f); // same region — no portals

        if (!_nodes.ContainsKey(src) || !_nodes.ContainsKey(goal)) return null;

        // Dijkstra. Cost per edge = approx-walk-from-entry-cell-to-portal-source + EdgeBase.
        // We approximate "where we entered the source region" with the portal cell that brought us in
        // (or the bot's cell for the start region). Chebyshev distance is the natural metric on an
        // 8-connected grid and matches WalkMap.Heuristic; that's all we need for ranking.
        const float EdgeBase = 10f;
        // Extra cost for kafra hops: ~60 cells equivalent for the dialog overhead (click, advance,
        // pick top menu, advance, pick destination, await warp). Roughly the time it takes to walk that
        // many tiles, so the planner prefers a portal that's <60 cells closer than a kafra hop. Above
        // that, kafra wins — which is the right tradeoff since kafra warps are currently free.
        const float KafraDialogOverhead = 60f;
        var dist = new Dictionary<(string, ushort), float>();
        var prevEdge = new Dictionary<(string, ushort), Edge?>();
        // Track the cell we ENTERED each node at, so the next hop's cost reflects in-region walk.
        var entryCell = new Dictionary<(string, ushort), (int x, int y)>();
        dist[src] = 0f;
        entryCell[src] = (fromX, fromY);
        var open = new PriorityQueue<(string map, ushort region), float>();
        open.Enqueue(src, 0f);

        while (open.Count > 0)
        {
            var cur = open.Dequeue();
            if (!_nodes.TryGetValue(cur, out var node)) continue;
            var curDist = dist[cur];
            var (cx, cy) = entryCell[cur];
            if (cur.Equals(goal)) break;
            foreach (var e in node.Out)
            {
                if (edgeFilter != null && !edgeFilter(e)) continue;
                var nbr = (e.DstMap, e.DstRegion);
                var walkIn = ChebyshevDist(cx, cy, e.Sx, e.Sy); // in-region walk cost
                var stepCost = EdgeBase + walkIn;
                if (e.Kafra != null) stepCost += KafraDialogOverhead;
                var alt = curDist + stepCost;
                if (!dist.TryGetValue(nbr, out var existing) || alt < existing)
                {
                    dist[nbr] = alt;
                    prevEdge[nbr] = e;
                    entryCell[nbr] = (e.Dx, e.Dy);
                    open.Enqueue(nbr, alt);
                }
            }
        }

        if (!dist.TryGetValue(goal, out var goalCost)) return null;

        // Reconstruct the edge path by walking prevEdge from goal back to src.
        var edges = new List<Edge>();
        var node2 = goal;
        var guard = 0;
        while (!node2.Equals(src))
        {
            if (++guard > 200) return null; // sanity
            if (!prevEdge.TryGetValue(node2, out var e) || e == null) return null;
            edges.Add(e);
            node2 = (e.SrcMap, e.SrcRegion);
        }
        edges.Reverse();
        return new TravelPlan(edges, goalCost);
    }

    /// <summary>Convenience: the source cell of the next portal to walk to on the way from
    /// (fromMap,fromCell) toward (toMap,toCell). Returns null when no route exists OR the bot is already
    /// on the destination map/region.</summary>
    public Edge? NextPortalToward(string fromMap, int fromX, int fromY, string toMap, int toX, int toY)
    {
        var plan = Plan(fromMap, fromX, fromY, toMap, toX, toY);
        if (plan == null || plan.Edges.Count == 0) return null;
        return plan.Edges[0];
    }

    private static float ChebyshevDist(int ax, int ay, int bx, int by)
    {
        return Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
    }
}
