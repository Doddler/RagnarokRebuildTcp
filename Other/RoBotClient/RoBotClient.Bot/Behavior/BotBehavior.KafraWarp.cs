using RoBotClient.Bot.GameData;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Behavior;

// When the WorldGraph plan's next hop is a kafra-warp edge, the bot can't just "walk to a portal cell"
// — it has to drive the kafra NPC's dialog. This sub-FSM walks to the NPC, clicks, picks the top-menu
// "Teleport Service" option, then picks the destination option. The server fires MoveTo when the
// destination option is picked, which warps the bot. The existing apron-departure logic on the new map
// takes over from there.
//
// State is per-trip: cleared on warp (map change), cleared on deadline, cleared when StepTowardMapAsync
// stops picking a kafra edge. Multiple ticks drive this — each phase has a short throttle to give the
// server room to push new dialog state.
public sealed partial class BotBehavior
{
    private enum KafraStep { Idle, WalkToNpc, ClickNpc, SelectTopMenu, SelectDestination, AwaitWarp }

    private KafraStep _kafraStep = KafraStep.Idle;
    private int _kafraTopMenuOption;
    private int _kafraDestOption;
    private int _kafraNpcX, _kafraNpcY;
    private string _kafraStartMap = "";
    private DateTime _kafraNextAction;
    private DateTime _kafraDeadline;
    private int _kafraLastSeq;

    // Per-bot kafra-failure blacklist. Key is the kafra's (map, x, y) — when a kafra interaction times
    // out (typically because the bot can't actually walk to the NPC, e.g. they're in different
    // connected sub-regions and the WorldGraph collapsed them), we add the kafra here for 5 minutes.
    // While blacklisted, TryPickReachablePortalTowardGraph filters out the edge so the planner picks
    // portal-walking (or a different kafra) instead of looping on the same broken kafra.
    private readonly Dictionary<(string Map, int X, int Y), DateTime> _kafraBlacklist = new();
    private static readonly TimeSpan KafraBlacklistTtl = TimeSpan.FromMinutes(5);

    /// <summary>True if the given kafra cell is currently blacklisted. Expired entries are cleaned up
    /// lazily on read.</summary>
    private bool IsKafraBlacklisted(string map, int x, int y)
    {
        var key = (map, x, y);
        if (!_kafraBlacklist.TryGetValue(key, out var expires)) return false;
        if (DateTime.UtcNow >= expires) { _kafraBlacklist.Remove(key); return false; }
        return true;
    }

    private void BlacklistKafra(string map, int x, int y, string reason)
    {
        _kafraBlacklist[(map, x, y)] = DateTime.UtcNow + KafraBlacklistTtl;
        OnLog?.Invoke($"Kafra blacklisted: {map} ({x},{y}) — {reason}. Planner will route around for {KafraBlacklistTtl.TotalMinutes:F0} min.");
    }

    /// <summary>Reset the kafra sub-FSM. Called when a trip ends, when the planner stops picking kafra
    /// edges, or when a warp completes. Idempotent.</summary>
    private void ResetKafraStep()
    {
        _kafraStep = KafraStep.Idle;
        _kafraNpcX = _kafraNpcY = 0;
        _kafraStartMap = "";
        _kafraLastSeq = 0;
    }

    /// <summary>Drive one tick of the kafra teleport interaction. Caller has already verified the
    /// next planned hop is a kafra edge and that (npcX, npcY) is the kafra's map cell on the bot's
    /// current map. Returns immediately after issuing one action — caller will re-enter on the next
    /// tick. When the warp completes (map changes), state is cleared and the normal apron-departure
    /// logic handles arrival.</summary>
    private async Task StepKafraInteractionAsync(Snapshot snap, int npcX, int npcY,
        WorldGraph.KafraEdgeInfo info, CancellationToken ct)
    {
        // Fresh interaction — initialize. We re-init whenever the targeted NPC cell changes too, since
        // the planner may switch kafras between ticks if e.g. mob danger shifts the route.
        if (_kafraStep == KafraStep.Idle || _kafraNpcX != npcX || _kafraNpcY != npcY)
        {
            _kafraStep = KafraStep.WalkToNpc;
            _kafraTopMenuOption = info.TopMenuOption;
            _kafraDestOption = info.DestOptionIndex;
            _kafraNpcX = npcX;
            _kafraNpcY = npcY;
            _kafraStartMap = snap.Map;
            _kafraDeadline = DateTime.UtcNow.AddSeconds(45);
            _kafraNextAction = DateTime.MinValue;
            _kafraLastSeq = 0;
            OnLog?.Invoke($"Kafra warp: heading to NPC ({npcX},{npcY}) on {snap.Map} to teleport to '{info.OptionLabel}' (option {info.DestOptionIndex}).");
        }

        if (DateTime.UtcNow > _kafraDeadline)
        {
            // Whatever phase we failed in, the kafra is currently unusable from the bot's position.
            // Blacklist the NPC so the planner stops picking it; otherwise the next tick re-Plans,
            // gets the same kafra edge, and we loop forever.
            var reason = _kafraStep == KafraStep.WalkToNpc ? "couldn't walk to NPC in time"
                : _kafraStep == KafraStep.ClickNpc ? "NPC out of view after walk"
                : "dialog stalled past deadline";
            BlacklistKafra(snap.Map, npcX, npcY, reason);
            OnLog?.Invoke("Kafra warp aborted: trip deadline exceeded.");
            await _bot.ShopCloseAsync(ct); // closes any open shop / nudges dialog away (harmless if none)
            ResetKafraStep();
            return;
        }

        if (DateTime.UtcNow < _kafraNextAction) return;

        switch (_kafraStep)
        {
            case KafraStep.WalkToNpc:
            {
                var dist = Math.Max(Math.Abs(npcX - snap.SelfPos.X), Math.Abs(npcY - snap.SelfPos.Y));
                if (dist <= 3)
                {
                    _kafraStep = KafraStep.ClickNpc;
                    _kafraNextAction = DateTime.MinValue;
                    return;
                }
                _kafraNextAction = DateTime.UtcNow.AddSeconds(1.2);
                await WalkPathTowardAsync(snap, npcX, npcY, 16, ct);
                return;
            }

            case KafraStep.ClickNpc:
            {
                var npcId = _bot.WithState(w => FindKafraEntityId(w, npcX, npcY));
                if (npcId == 0)
                {
                    // Not in view yet — walk a little closer and retry.
                    _kafraNextAction = DateTime.UtcNow.AddMilliseconds(600);
                    await WalkPathTowardAsync(snap, npcX, npcY, 6, ct);
                    return;
                }
                _kafraNextAction = DateTime.UtcNow.AddMilliseconds(800);
                _kafraLastSeq = _bot.SnapshotNpc().Seq;
                _kafraStep = KafraStep.SelectTopMenu;
                await _bot.NpcClickAsync(npcId, ct);
                return;
            }

            case KafraStep.SelectTopMenu:
            {
                var npc = _bot.SnapshotNpc();
                if (npc.Seq == _kafraLastSeq)
                {
                    // No new state yet.
                    _kafraNextAction = DateTime.UtcNow.AddMilliseconds(300);
                    return;
                }
                switch (npc.Phase)
                {
                    case NpcPhase.Dialog:
                        _kafraNextAction = DateTime.UtcNow.AddMilliseconds(450);
                        _kafraLastSeq = npc.Seq;
                        await _bot.NpcAdvanceAsync(ct);
                        return;
                    case NpcPhase.Option:
                        // First menu — pick "Teleport Service" at _kafraTopMenuOption.
                        _kafraNextAction = DateTime.UtcNow.AddMilliseconds(500);
                        _kafraLastSeq = npc.Seq;
                        _kafraStep = KafraStep.SelectDestination;
                        await _bot.NpcSelectOptionAsync(_kafraTopMenuOption, ct);
                        return;
                    case NpcPhase.Ended:
                    case NpcPhase.None:
                        // NPC closed unexpectedly. Re-click.
                        _kafraLastSeq = npc.Seq;
                        _kafraStep = KafraStep.ClickNpc;
                        _kafraNextAction = DateTime.UtcNow.AddMilliseconds(400);
                        return;
                    default:
                        _kafraNextAction = DateTime.UtcNow.AddMilliseconds(400);
                        return;
                }
            }

            case KafraStep.SelectDestination:
            {
                var npc = _bot.SnapshotNpc();
                if (npc.Seq == _kafraLastSeq)
                {
                    _kafraNextAction = DateTime.UtcNow.AddMilliseconds(300);
                    return;
                }
                switch (npc.Phase)
                {
                    case NpcPhase.Dialog:
                        _kafraNextAction = DateTime.UtcNow.AddMilliseconds(450);
                        _kafraLastSeq = npc.Seq;
                        await _bot.NpcAdvanceAsync(ct);
                        return;
                    case NpcPhase.Option:
                        if (_kafraDestOption < 0 || _kafraDestOption >= npc.Options.Count)
                        {
                            OnLog?.Invoke($"Kafra warp aborted: destination option {_kafraDestOption} out of range " +
                                          $"({npc.Options.Count} options shown). Server menu shape may have changed.");
                            await _bot.ShopCloseAsync(ct); // closes any open shop / nudges dialog away (harmless if none)
                            ResetKafraStep();
                            return;
                        }
                        _kafraNextAction = DateTime.UtcNow.AddMilliseconds(600);
                        _kafraLastSeq = npc.Seq;
                        _kafraStep = KafraStep.AwaitWarp;
                        await _bot.NpcSelectOptionAsync(_kafraDestOption, ct);
                        return;
                    case NpcPhase.Ended:
                    case NpcPhase.None:
                        OnLog?.Invoke("Kafra warp aborted: NPC closed before destination menu shown.");
                        ResetKafraStep();
                        return;
                    default:
                        _kafraNextAction = DateTime.UtcNow.AddMilliseconds(400);
                        return;
                }
            }

            case KafraStep.AwaitWarp:
            {
                // Wait until our map changes — the server's MoveTo fires after our destination pick.
                if (!string.Equals(snap.Map, _kafraStartMap, StringComparison.OrdinalIgnoreCase))
                {
                    OnLog?.Invoke($"Kafra warp complete — arrived on {snap.Map}.");
                    // Successful traversal — clear any stale blacklist entry for this kafra so a
                    // one-off transient failure doesn't lock the planner out of the route for 5 min.
                    _kafraBlacklist.Remove((_kafraStartMap, _kafraNpcX, _kafraNpcY));
                    ResetKafraStep();
                    return;
                }
                _kafraNextAction = DateTime.UtcNow.AddMilliseconds(500);
                return;
            }
        }
    }

    /// <summary>Find the kafra's entity id in the visible world by matching (X, Y). Kafras are NPCs;
    /// we match within a small Chebyshev radius so the position can drift slightly on the server side
    /// without breaking the click.</summary>
    private static int FindKafraEntityId(WorldState w, int kx, int ky)
    {
        var best = 0;
        var bestD = int.MaxValue;
        foreach (var e in w.Entities.Values)
        {
            if (!e.IsNpc) continue;
            var d = Math.Max(Math.Abs(e.Position.X - kx), Math.Abs(e.Position.Y - ky));
            if (d <= 2 && d < bestD) { bestD = d; best = e.Id; }
        }
        return best;
    }
}
