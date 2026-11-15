using RoBotClient.Bot.Behavior;

namespace RoBotClient.Bot.Manager;

/// <summary>
/// Periodically re-clusters running bots into squads based on what each bot can actually SEE in its own
/// <c>World.Entities</c> — not just position-proximity. If bot A's view contains bot B's entity id (or
/// vice versa), they share an undirected edge in the visibility graph. Connected components become
/// squads; each component elects its leader via <see cref="SquadRanking"/>.
///
/// Manual squads (SquadId not prefixed with <c>auto-</c>) are off-limits — the auto-former never touches
/// them. Solo bots (no visible peer) are left at SquadId="" so the legacy / solo paths still fire.
/// </summary>
public sealed class SquadAutoFormer
{
    private const string AutoPrefix = "auto-";

    private readonly BotManager _bots;
    private readonly Timer _timer;
    private volatile bool _enabled = true;

    public bool Enabled { get => _enabled; set => _enabled = value; }
    public TimeSpan Interval { get; }
    public DateTime LastRunUtc { get; private set; }
    public int LastSquadsFormed { get; private set; }
    public int LastBotsAssigned { get; private set; }

    public SquadAutoFormer(BotManager bots, TimeSpan? interval = null)
    {
        _bots = bots;
        Interval = interval ?? TimeSpan.FromSeconds(8);
        _timer = new Timer(_ => SafeTick(), null, Interval, Interval);
    }

    private void SafeTick()
    {
        try { if (_enabled) Tick(); }
        catch { /* never let the timer die — diagnose via log otherwise */ }
    }

    /// <summary>Force one immediate clustering pass. Returns the same stats <see cref="LastRunUtc"/>
    /// records — useful from MCP for the agent to see the effect of a toggle without waiting.</summary>
    public (int squads, int bots) ForceRun()
    {
        Tick();
        return (LastSquadsFormed, LastBotsAssigned);
    }

    private void Tick()
    {
        // Pull live state for every running bot: its own entity id, what entity ids it can see, the rank
        // inputs needed if it's elected leader, its current squad assignment, AND its party identity so we
        // only cluster bots from the SAME party (squads are intra-party — never cross). A bot not in a
        // party is skipped entirely; the legacy/solo path stays in charge of it.
        var snapshots = new List<BotVisibility>();
        foreach (var id in _bots.AllBotIds())
        {
            var session = _bots.GetSession(id);
            var cfg = _bots.GetBehaviorConfig(id);
            var cand = _bots.GetSquadCandidate(id);
            if (session == null || cfg == null || cand == null) continue;
            if (!cand.Value.InGame) continue;
            if (!session.InParty) continue;
            // Bots in MANUAL squads (anything not auto-prefixed and non-empty) are immutable from this side.
            // We don't even cluster them — if you want them in a dynamic cluster, clear_squad first.
            if (!string.IsNullOrEmpty(cfg.SquadId) && !cfg.SquadId.StartsWith(AutoPrefix, StringComparison.Ordinal))
                continue;

            // Party identity: the leader is the canonical name (leader's own name OR its PartyLeaderName,
            // which is "" precisely when this bot IS the leader). Two bots share a party iff this key
            // matches.
            var partyKey = session.IsPartyLeader ? cand.Value.CharacterName : session.PartyLeaderName;
            if (string.IsNullOrEmpty(partyKey)) continue; // race window: in-party bit set but leader name not yet known

            var (selfId, visibleIds, map) = session.WithState(w =>
            {
                var visible = new HashSet<int>();
                foreach (var e in w.Entities.Values)
                    if (e.IsPlayer && e.Id != w.Self.EntityId) visible.Add(e.Id);
                return (w.Self.EntityId, visible, w.Self.Map);
            });

            snapshots.Add(new BotVisibility(id, selfId, map, partyKey, visibleIds, cand.Value));
        }

        // Per-map entity-id → botId index so we can collapse "visible entity 1234" into "that's bot7".
        // Bots on different maps are never in the same squad — we cluster within each map's index.
        var byMap = snapshots.GroupBy(s => s.Map).ToList();
        var newAssignments = new Dictionary<string, (string squadId, bool isLeader, string leaderName, int slot)>();

        foreach (var mapGroup in byMap)
        {
            var byEntity = new Dictionary<int, BotVisibility>();
            foreach (var s in mapGroup) byEntity[s.SelfEntityId] = s;

            // Union-find over the visibility graph. Edges are symmetric: A↔B if either A sees B's entity
            // or B sees A's entity. A bot with no peer edges stays solo (parent==itself).
            var parent = mapGroup.ToDictionary(s => s.BotId, s => s.BotId);
            string Find(string x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Union(string a, string b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }

            foreach (var s in mapGroup)
                foreach (var vid in s.VisibleEntityIds)
                    if (byEntity.TryGetValue(vid, out var other)
                        && string.Equals(s.PartyKey, other.PartyKey, StringComparison.Ordinal))
                        Union(s.BotId, other.BotId);

            // Walk components. A component with 1 bot is solo — leave SquadId empty so the legacy/solo
            // path runs. A component with 2+ bots gets an auto- squad id derived from the lex-smallest
            // botId in it (stable across passes — bots staying in the same cluster keep the same id).
            var groups = mapGroup.GroupBy(s => Find(s.BotId)).ToList();
            foreach (var g in groups)
            {
                var members = g.ToList();
                if (members.Count < 2)
                {
                    // Solo — clear any prior auto- assignment so we don't keep dragging a stale squad
                    // header on a now-alone bot.
                    var only = members[0];
                    newAssignments[only.BotId] = ("", false, "", 0);
                    continue;
                }
                var leader = SquadRanking.PickLeader(members.Select(m => m.Rank).ToList())!.Value;
                var anchor = members.MinBy(m => m.BotId, StringComparer.OrdinalIgnoreCase)!.BotId;
                var sqid = $"{AutoPrefix}{mapGroup.Key}-{anchor}";
                // Stable slot ordering: leader = 0, followers numbered by botId lex order so a member's
                // slot stays put across passes (no formation churn when somebody moves slightly).
                var followerSlot = 1;
                var sorted = members.OrderBy(m => m.BotId, StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var m in sorted)
                {
                    var slot = m.BotId == leader.BotId ? 0 : followerSlot++;
                    newAssignments[m.BotId] = (sqid, m.BotId == leader.BotId, leader.CharacterName, slot);
                }
            }
        }

        // Apply diffs only — avoid burning the persistent BotConfigStore on a no-op pass. The dashboard
        // log only records the diff so a manual review can see "squad reshuffled at HH:MM:SS".
        var squadsTouched = new HashSet<string>(StringComparer.Ordinal);
        var changed = 0;
        foreach (var kv in newAssignments)
        {
            var cfg = _bots.GetBehaviorConfig(kv.Key);
            if (cfg == null) continue;
            var (sqid, isLeader, leaderName, slot) = kv.Value;
            if (cfg.SquadId == sqid && cfg.IsSquadLeader == isLeader
                && cfg.SquadLeaderName == leaderName && cfg.SquadSlot == slot) continue;
            cfg.SquadId = sqid;
            cfg.IsSquadLeader = isLeader;
            cfg.SquadLeaderName = leaderName;
            cfg.SquadSlot = slot;
            _bots.SaveConfig(kv.Key);
            changed++;
            if (!string.IsNullOrEmpty(sqid)) squadsTouched.Add(sqid);
        }

        LastRunUtc = DateTime.UtcNow;
        LastSquadsFormed = squadsTouched.Count;
        LastBotsAssigned = changed;
    }

    private sealed record BotVisibility(string BotId, int SelfEntityId, string Map, string PartyKey, HashSet<int> VisibleEntityIds, SquadCandidate Rank);
}
