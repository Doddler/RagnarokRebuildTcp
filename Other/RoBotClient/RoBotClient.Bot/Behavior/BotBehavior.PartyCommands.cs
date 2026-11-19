using System.Globalization;

namespace RoBotClient.Bot.Behavior;

// Chat-driven party coordination. Squad leaders ANNOUNCE target picks ("!engage <classId>") so followers
// can switch onto the new target before the server's CurrentTargetId broadcast catches up. Squad
// followers LISTEN for leader chat and execute one of a small command set:
//
//   !engage <classId>   pick the nearest visible monster of <classId> as your own target.
//   !flee               drop everything and step away from the current hazard / target.
//   !hold               freeze in place; ignore monsters until !hunt / !follow.
//   !regroup            walk back to the leader's cell at "march" speed (legacy CloseGap).
//   !stop               stop moving + clear current target for one tick.
//   !hunt               clear !hold; resume normal follower behavior.
//   !follow             alias for !hunt — explicit "resume shadowing the leader."
//
// Filter: only chat from the configured SquadLeaderName (or PartyLeaderName fallback for unsquadded
// followers) is honored. Bots ignore their own echoes (BotSession.ReadSay drops those upstream).
public sealed partial class BotBehavior
{
    public enum PartyCommandKind { None, Engage, Flee, Hold, Regroup, Stop, Hunt, Follow }

    /// <summary>Most-recent leader command this follower received. Tick logic checks
    /// <see cref="_partyCmdExpiresAt"/> to know if it's still active.</summary>
    private PartyCommandKind _partyCmd = PartyCommandKind.None;
    private int _partyCmdArg;
    private DateTime _partyCmdReceivedAt;
    private DateTime _partyCmdExpiresAt;
    // Dedup: identical commands within the cooldown window are dropped (a chatty leader spamming !engage
    // shouldn't reset our timers).
    private string _lastCmdKey = "";
    // Throttle for outbound leader announces — at most one !engage per 2 seconds, and we dedup on the
    // class id so a target switch BACK to a recently-announced class doesn't re-broadcast.
    private int _lastAnnouncedTargetClass = -1;
    private DateTime _nextAnnounceAt;

    /// <summary>Leader-side: announce the current target via chat as <c>!engage &lt;classId&gt;</c> so
    /// follower bots running B2 can switch onto it. No-op unless this bot is a squad leader AND
    /// AnnounceTargetInChat is enabled. Dedup on class id + 2s throttle so we don't spam map chat.</summary>
    private async Task MaybeAnnounceTargetAsync(MonsterInfo target, CancellationToken ct)
    {
        if (!_config.AnnounceTargetInChat) return;
        if (!_config.IsSquadLeader && _bot.IsPartyLeader == false) return; // not leading anything
        if (_lastAnnouncedTargetClass == target.ClassId) return;
        if (DateTime.UtcNow < _nextAnnounceAt) return;

        _lastAnnouncedTargetClass = target.ClassId;
        _nextAnnounceAt = DateTime.UtcNow.AddSeconds(2);
        var line = $"!engage {target.ClassId}";
        try { await _bot.SayAsync(line, ct); }
        catch { /* outbound chat failure shouldn't break the tick */ }
    }

    /// <summary>Follower-side: chat heard from somebody else. Filter to squad-leader / party-leader
    /// senders, parse the leading <c>!cmd</c>, stamp <see cref="_partyCmd"/> for the tick loop.</summary>
    private void OnChatHeard(string senderName, string text, byte chatType)
    {
        try
        {
            if (!_config.ListenToPartyChat) return;
            if (string.IsNullOrEmpty(text) || text[0] != '!') return;

            // Only the squad leader (or party leader, when no squad is set) drives this. Anything from
            // strangers, monsters, or random players is ignored — same filter as the squad FSM uses.
            var allowedSender = !string.IsNullOrEmpty(_config.SquadLeaderName) && !_config.IsSquadLeader
                ? _config.SquadLeaderName
                : (_bot.InParty && !_bot.IsPartyLeader ? _bot.PartyLeaderName : "");
            if (string.IsNullOrEmpty(allowedSender)) return;
            if (!string.Equals(senderName, allowedSender, StringComparison.Ordinal)) return;

            if (!TryParseCommand(text, out var kind, out var arg)) return;

            // Dedup: a chatty leader sending !engage every 200ms shouldn't reset our combat clocks.
            var key = $"{kind}:{arg}";
            if (key == _lastCmdKey && DateTime.UtcNow - _partyCmdReceivedAt < TimeSpan.FromSeconds(1)) return;

            _partyCmd = kind;
            _partyCmdArg = arg;
            _partyCmdReceivedAt = DateTime.UtcNow;
            _partyCmdExpiresAt = DateTime.UtcNow + KindTtl(kind);
            _lastCmdKey = key;
            OnLog?.Invoke($"Party cmd from '{senderName}': {kind}{(arg != 0 ? $" {arg}" : "")} (active {KindTtl(kind).TotalSeconds:F0}s).");
        }
        catch { /* never let the receive-thread handler escape */ }
    }

    private static bool TryParseCommand(string text, out PartyCommandKind kind, out int arg)
    {
        kind = PartyCommandKind.None;
        arg = 0;
        // Strip the leading '!' and split on whitespace.
        var rest = text.Substring(1).Trim();
        if (rest.Length == 0) return false;
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var head = parts[0].ToLowerInvariant();
        kind = head switch
        {
            "engage"  => PartyCommandKind.Engage,
            "flee"    => PartyCommandKind.Flee,
            "hold"    => PartyCommandKind.Hold,
            "regroup" => PartyCommandKind.Regroup,
            "stop"    => PartyCommandKind.Stop,
            "hunt"    => PartyCommandKind.Hunt,
            "follow"  => PartyCommandKind.Follow,
            "release" => PartyCommandKind.Hunt, // operator-friendly alias
            _         => PartyCommandKind.None,
        };
        if (kind == PartyCommandKind.None) return false;
        // Engage carries an int arg (the monster class id).
        if (kind == PartyCommandKind.Engage && parts.Length >= 2
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            arg = n;
        return true;
    }

    private static TimeSpan KindTtl(PartyCommandKind kind) => kind switch
    {
        PartyCommandKind.Engage  => TimeSpan.FromSeconds(10),  // give followers ~10s to lock onto the call
        PartyCommandKind.Flee    => TimeSpan.FromSeconds(8),
        PartyCommandKind.Hold    => TimeSpan.FromMinutes(5),   // a sticky park; cleared by another command
        PartyCommandKind.Regroup => TimeSpan.FromSeconds(20),
        PartyCommandKind.Stop    => TimeSpan.FromSeconds(2),
        PartyCommandKind.Hunt    => TimeSpan.FromSeconds(2),
        PartyCommandKind.Follow  => TimeSpan.FromSeconds(2),
        _                        => TimeSpan.Zero,
    };

    /// <summary>Tick-side: returns true if a chat command is currently in effect AND the tick should
    /// short-circuit (the helper performed the action for us). False means "no active command, run normal
    /// FSM" or "the command is informational and combat should continue."</summary>
    private async Task<bool> TickPartyCommandAsync(Snapshot snap, CancellationToken ct)
    {
        if (_partyCmd == PartyCommandKind.None) return false;
        if (DateTime.UtcNow >= _partyCmdExpiresAt)
        {
            _partyCmd = PartyCommandKind.None;
            return false;
        }

        switch (_partyCmd)
        {
            case PartyCommandKind.Engage:
            {
                // Find the nearest visible mob of the announced class. If we already have a target of that
                // class, leave it alone (avoid thrashing). If we don't, switch.
                if (_targetClass == _partyCmdArg && _targetId != 0) return false;
                MonsterInfo? best = null;
                var bestDist = int.MaxValue;
                foreach (var m in snap.Monsters)
                {
                    if (m.ClassId != _partyCmdArg) continue;
                    if (!IsHuntable(m)) continue;
                    var d = Dist(snap.SelfPos, m.Pos);
                    if (d < bestDist) { bestDist = d; best = m; }
                }
                if (best == null) return false; // nothing matching in view yet; keep doing what we were doing
                _targetId = best.Id;
                _targetClass = best.ClassId;
                TargetName = best.Name;
                Mode = BotMode.Hunting;
                _nextAttack = DateTime.UtcNow.AddSeconds(3);
                OnLog?.Invoke($"Following !engage — switching to '{best.Name}'.");
                await _bot.AttackAsync(best.Id, ct);
                return true;
            }

            case PartyCommandKind.Flee:
            {
                // Pretend the current target is a hazard for the duration. AvoidHazardsAsync logic walks
                // away from current cell; we replicate the minimum here so we don't have to surface the
                // private hazard list.
                if (Mode != BotMode.Fleeing) OnLog?.Invoke("Following !flee — backing away.");
                Mode = BotMode.Fleeing;
                _targetId = 0;
                _targetClass = -1;
                // Pick a step away from the squad leader's cell if we can see them — otherwise a random
                // 6-cell offset; the regular wander/movement code will take over once flee expires.
                var dx = _rng.Next(-1, 2);
                var dy = _rng.Next(-1, 2);
                if (dx == 0 && dy == 0) dx = 1;
                var tx = Math.Max(1, snap.SelfPos.X + dx * 6);
                var ty = Math.Max(1, snap.SelfPos.Y + dy * 6);
                await _bot.WalkToAsync(tx, ty, ct);
                return true;
            }

            case PartyCommandKind.Hold:
                Mode = BotMode.Parked;
                _targetId = 0;
                _targetClass = -1;
                return true;

            case PartyCommandKind.Stop:
                _targetId = 0;
                _targetClass = -1;
                await _bot.StopAsync(ct);
                _partyCmd = PartyCommandKind.None; // one-shot
                return true;

            case PartyCommandKind.Regroup:
                // Use the existing CloseGapAsync path against the leader's cell — same as the squad FSM
                // when the leader is visible.
                if (_cachedLeaderEntityId != 0)
                {
                    var mapPos = _bot.GetMapPosition(_cachedLeaderEntityId);
                    if (mapPos.HasValue)
                    {
                        await CloseGapAsync(snap, mapPos.Value, ct);
                        return true;
                    }
                }
                return false; // leader not locatable — fall through to normal FSM

            case PartyCommandKind.Hunt:
            case PartyCommandKind.Follow:
                // Inform the FSM that any prior !hold should be released. The actual behavior comes from
                // the normal tick path; just consume the command so the next tick runs default logic.
                _partyCmd = PartyCommandKind.None;
                OnLog?.Invoke($"Following {_lastCmdKey} — releasing prior hold/flee.");
                return false;
        }
        return false;
    }
}
