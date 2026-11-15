namespace RoBotClient.Bot.Behavior;

// Follower mode (#6+): when the bot is in a party AND is NOT the leader AND we know the leader's name from
// the invite, defer most decisions to the leader — attack what the leader's attacking, otherwise stay close.
// The leader bot acts normally; everything routes through the existing PickTarget / wander / travel paths.
//
// Leader location strategy, in priority order:
//   1. EntityView lookup by name (close range): gives us the leader's id + position AND a live target id
//      (from EntityView.CurrentTargetId, which BotSession.ReadAttack updates from every Attack packet we
//      see). Cache the leader's entity id once we see them this way.
//   2. Minimap fallback (off-screen but same map): the server broadcasts every active player's position via
//      PacketType.UpdateMapImportantEntityTracking every ~4 steps; we look the cached leader id up there
//      and walk toward it. No combat mirroring possible at this range — we just close the gap.
//   3. Truly lost (no EntityView, no minimap hit): we sit idle for up to FollowerLostFallthroughSeconds,
//      logging a one-shot "lost track of leader" message. After that we RETURN FALSE so normal FSM logic
//      (PickTarget / wander / travel-home) takes over instead of leaving the bot stuck in Idle forever —
//      this is the user-visible "FSM leaves bots on Idle a lot" symptom. When the leader becomes visible
//      again the fallback flag clears and follower mode resumes seamlessly.
public sealed partial class BotBehavior
{
    private const int FollowRadius = 4;

    private enum LeaderViewState { Unknown, Seen, OffScreen, Lost }

    private DateTime _nextFollowMove = DateTime.MinValue;
    private int _cachedLeaderEntityId; // sticky once seen in EntityView, cleared on map change
    private LeaderViewState _leaderState = LeaderViewState.Unknown;
    private DateTime? _leaderLostSince;
    private bool _fellThroughOnLostLeader; // true while we're letting normal FSM run because leader is lost
    private string _lastFollowerDiag = ""; // dedupes the "leader has target X but…" diagnostic logs

    private async Task<bool> TickAsFollowerAsync(Snapshot snap, CancellationToken ct)
    {
        // Resolve the follow-target: squad-leader assignment takes priority over the legacy party-leader.
        //   1. If this bot has a SquadId AND is NOT the squad leader, follow SquadLeaderName.
        //   2. Otherwise, if it's in a party AND is not the party leader, fall back to PartyLeaderName.
        //   3. Otherwise it's solo — let the normal FSM run.
        string leaderName;
        var hasSquad = !string.IsNullOrEmpty(_config.SquadId);
        if (hasSquad && !_config.IsSquadLeader && !string.IsNullOrEmpty(_config.SquadLeaderName))
        {
            leaderName = _config.SquadLeaderName;
        }
        else if (!hasSquad && _bot.InParty && !_bot.IsPartyLeader && !string.IsNullOrEmpty(_bot.PartyLeaderName))
        {
            // Legacy fallback for unsquadded party members. Once squads are assigned by MCP this branch
            // stops firing because squad-of-one members keep IsSquadLeader=true and skip follower-mode.
            leaderName = _bot.PartyLeaderName;
        }
        else
        {
            ResetFollowerLeaderState();
            return false;
        }

        // (1) Try EntityView first — gives target info too.
        var leaderView = _bot.WithState(w =>
        {
            foreach (var e in w.Entities.Values)
                if (e.IsPlayer && string.Equals(e.Name, leaderName, StringComparison.Ordinal))
                    return (found: true, id: e.Id, pos: e.Position, target: e.CurrentTargetId);
            return (found: false, id: 0, pos: default(RebuildSharedData.Data.Position), target: 0);
        });
        if (leaderView.found)
        {
            _cachedLeaderEntityId = leaderView.id;
            TransitionLeaderState(LeaderViewState.Seen, $"Follower: leader '{leaderName}' in view.");

            // Mirror the leader's current target if it's alive and in our view.
            if (leaderView.target != 0)
            {
                var m = FindMonster(snap, leaderView.target);
                if (m == null)
                {
                    // Leader is attacking something we don't have in EntityView yet — usually means we're
                    // at the edge of view range. For DPS/Tank/Utility don't just sit waiting; pick up a
                    // nearby mob ourselves while we close the gap. Healer / Buffer stay supportive.
                    // (Dedup key is the GENERIC reason, not the specific target id — the leader's target
                    // cycles fast, so keying on the id was producing one log line per cycle. Once per
                    // out-of-view streak is enough.)
                    var rOffView = _config.ResolveRole(snap.SelfJobId);
                    if (rOffView != PartyRole.Healer && rOffView != PartyRole.Buffer)
                    {
                        var aux = PickOwnTargetNearLeader(snap, leaderView.pos);
                        if (aux != null)
                        {
                            var switched = _targetId != aux.Id;
                            if (switched)
                            {
                                _targetId = aux.Id; _targetClass = aux.ClassId; TargetName = aux.Name;
                                ResetStuck(snap.SelfPos, aux.Hp);
                                OnLog?.Invoke($"Follower: leader's target off-screen, attacking {aux.Name} near leader.");
                                _lastFollowerDiag = "";
                            }
                            Mode = BotMode.Hunting;
                            if (await TickSkillsAsync(snap, aux, ct)) return true;
                            // On target SWITCH attack immediately. On a continuing target, the server keeps
                            // the auto-attack going server-side; only re-send Attack after the 3s safety
                            // cooldown to keep the packet rate down.
                            if (switched || DateTime.UtcNow >= _nextAttack)
                            {
                                _nextAttack = DateTime.UtcNow.AddSeconds(3);
                                await _bot.AttackAsync(_targetId, ct);
                            }
                            return true;
                        }
                    }
                    LogFollowerDiagOnce("leader_target_not_in_view",
                        $"Follower: leader's target not in our view yet — closing distance.");
                }
                else if (m.MaxHp > 0 && m.Hp <= 0)
                {
                    LogFollowerDiagOnce($"leader_target_{m.Id}_dead",
                        $"Follower: leader's target '{m.Name}' is dead — waiting for next.");
                }
                else
                {
                    var switched = _targetId != m.Id;
                    if (switched)
                    {
                        _targetId = m.Id;
                        _targetClass = m.ClassId;
                        TargetName = m.Name;
                        ResetStuck(snap.SelfPos, m.Hp);
                        OnLog?.Invoke($"Follower: mirroring {leaderName}'s target — {m.Name}.");
                        _lastFollowerDiag = "";
                    }
                    Mode = BotMode.Hunting;
                    if (await TickSkillsAsync(snap, m, ct)) return true;
                    if (switched || DateTime.UtcNow >= _nextAttack)
                    {
                        _nextAttack = DateTime.UtcNow.AddSeconds(3);
                        await _bot.AttackAsync(_targetId, ct);
                    }
                    return true;
                }
            }
            else
            {
                // Leader visible but no CurrentTargetId — they haven't fired an Attack packet recently, OR
                // they're using a skill type that doesn't broadcast via PacketType.Attack (ground AoE, buffs).
                // For DPS / Tank / Utility roles we don't wait — the follower picks its own target near the
                // leader. In practice the leader usually engages whatever's closest to them too, so the
                // choice converges and the follower stops feeling sluggish. Healer / Buffer skip this so
                // they prioritize support actions over punching mobs.
                var role = _config.ResolveRole(snap.SelfJobId);
                if (role != PartyRole.Healer && role != PartyRole.Buffer)
                {
                    var ownTarget = PickOwnTargetNearLeader(snap, leaderView.pos);
                    if (ownTarget != null)
                    {
                        var switched = _targetId != ownTarget.Id;
                        if (switched)
                        {
                            _targetId = ownTarget.Id;
                            _targetClass = ownTarget.ClassId;
                            TargetName = ownTarget.Name;
                            ResetStuck(snap.SelfPos, ownTarget.Hp);
                            OnLog?.Invoke($"Follower: leader has no broadcast target, attacking {ownTarget.Name} near them.");
                            _lastFollowerDiag = "";
                        }
                        Mode = BotMode.Hunting;
                        if (await TickSkillsAsync(snap, ownTarget, ct)) return true;
                        if (switched || DateTime.UtcNow >= _nextAttack)
                        {
                            _nextAttack = DateTime.UtcNow.AddSeconds(3);
                            await _bot.AttackAsync(_targetId, ct);
                        }
                        return true;
                    }
                }
                LogFollowerDiagOnce("leader_no_target",
                    $"Follower: '{leaderName}' visible but no current target broadcast — staying close.");
            }

            return await CloseGapAsync(snap, leaderView.pos, ct);
        }

        // (2) EntityView miss — try the minimap broadcast for the cached leader id.
        if (_cachedLeaderEntityId != 0)
        {
            var mapPos = _bot.GetMapPosition(_cachedLeaderEntityId);
            if (mapPos.HasValue)
            {
                TransitionLeaderState(LeaderViewState.OffScreen,
                    $"Follower: leader '{leaderName}' off-screen — tracking via minimap at ({mapPos.Value.X},{mapPos.Value.Y}).");
                return await CloseGapAsync(snap, mapPos.Value, ct);
            }
        }

        // (3) Truly lost — neither EntityView nor minimap has the leader on our current map.
        TransitionLeaderState(LeaderViewState.Lost, $"Follower: lost track of leader '{leaderName}'.");
        _leaderLostSince ??= DateTime.UtcNow;
        var lostSecs = (DateTime.UtcNow - _leaderLostSince.Value).TotalSeconds;
        if (lostSecs >= _config.FollowerLostFallthroughSeconds)
        {
            if (!_fellThroughOnLostLeader)
            {
                _fellThroughOnLostLeader = true;
                OnLog?.Invoke($"Follower: leader '{leaderName}' lost for {lostSecs:F0}s — falling back to normal hunting.");
            }
            return false; // hand off to PickTarget / wander / travel-home — don't stay stuck on Idle
        }

        // Within the (short) grace window: idle in place, don't engage on our own. Defaults to 5 s; bump it
        // via BotBehaviorConfig.FollowerLostFallthroughSeconds if you want more patience.
        Mode = BotMode.Idle;
        _targetId = 0;
        _targetClass = -1;
        return true;
    }

    // Sub-PickTarget for followers: only takes a monster that the SAME forecast we use solo says we can win,
    // and that's within 'NearLeaderRadius' tiles of the leader. We deliberately don't reuse the leader's
    // forecast — a fragile leader could be in range of mobs we shouldn't take, so we re-check from our own
    // stats. The narrow radius keeps followers clustered around the leader instead of fanning out into a
    // pull they aren't tanking.
    private const int NearLeaderRadius = 9;

    private MonsterInfo? PickOwnTargetNearLeader(Snapshot snap, RebuildSharedData.Data.Position leaderPos)
    {
        MonsterInfo? best = null;
        var bestScore = int.MaxValue;
        foreach (var m in snap.Monsters)
        {
            if (!IsHuntable(m)) continue;
            // Mirror PickTarget's time-decay logic so a follower doesn't engage a class that just killed
            // its squad leader (the avoidance entry is keyed by classId, not by bot).
            if (_avoidClasses.TryGetValue(m.ClassId, out var avoidUntil) && DateTime.UtcNow < avoidUntil) continue;
            if (_unreachable.Contains(m.Id)) continue;
            if (CellNearHazard(m.Pos.X, m.Pos.Y)) continue;
            // Don't chase a mob standing on a portal apron — same safety as solo PickTarget.
            if (_config.PortalSafeDistance > 0 && _data != null
                && _data.IsNearPortal(snap.Map, m.Pos.X, m.Pos.Y, _config.PortalSafeDistance)) continue;
            var distToLeader = Math.Max(Math.Abs(m.Pos.X - leaderPos.X), Math.Abs(m.Pos.Y - leaderPos.Y));
            if (distToLeader > NearLeaderRadius) continue;
            // Force-hunt mirrors the solo PickTarget override: operator's allowlist beats the forecast veto.
            if (!_config.ForceHuntClassIds.Contains(m.ClassId) && !Forecast(snap, m).CanWin) continue;
            var distToMe = Dist(snap.SelfPos, m.Pos);
            // Mild bias toward the mob the leader is most likely to pick (i.e. closest to them).
            var score = distToMe + distToLeader / 2;
            if (score < bestScore) { bestScore = score; best = m; }
        }
        return best;
    }

    private void LogFollowerDiagOnce(string key, string message)
    {
        if (_lastFollowerDiag == key) return;
        _lastFollowerDiag = key;
        OnLog?.Invoke(message);
    }

    private void TransitionLeaderState(LeaderViewState next, string logIfChanged)
    {
        if (_leaderState == next) return;
        _leaderState = next;
        if (next == LeaderViewState.Seen || next == LeaderViewState.OffScreen)
        {
            _leaderLostSince = null;
            _fellThroughOnLostLeader = false;
        }
        OnLog?.Invoke(logIfChanged);
    }

    private void ResetFollowerLeaderState()
    {
        _leaderState = LeaderViewState.Unknown;
        _leaderLostSince = null;
        _fellThroughOnLostLeader = false;
    }

    // Formation offsets per slot index (slot 0 = leader's own cell — leader doesn't follow itself).
    // Indexed by SquadSlot mod the length so an oversized squad still gets a coordinate without crashing.
    // Layout: slot 1 = left rear, slot 2 = right rear, slot 3 = directly behind, then a wider second row.
    private static readonly (int dx, int dy)[] FormationOffsets =
    {
        ( 0,  0),  // slot 0 — leader (unused)
        (-2, -1),  // slot 1
        ( 2, -1),  // slot 2
        ( 0, -2),  // slot 3
        (-3, -2),  // slot 4
        ( 3, -2),  // slot 5
        (-1, -3),  // slot 6
        ( 1, -3),  // slot 7
    };

    private (int x, int y) ComputeFollowCell(RebuildSharedData.Data.Position leaderPos)
    {
        // Without a SquadSlot (i.e. legacy follower-mode or solo squad), fall back to the old "walk to
        // leader's cell" behaviour. The walkable-search in TryFindWalkableNear handles the edge case where
        // the offset lands on a wall.
        var slot = _config.SquadSlot;
        if (slot <= 0 || slot >= FormationOffsets.Length)
            return (leaderPos.X, leaderPos.Y);
        var (dx, dy) = FormationOffsets[slot];
        var tx = leaderPos.X + dx;
        var ty = leaderPos.Y + dy;
        var wm = _data?.GetWalkMap(_bot.WithState(w => w.Self.Map));
        if (wm != null && wm.TryFindWalkableNear(tx, ty, 3, out var fx, out var fy))
            return (fx, fy);
        return (tx, ty);
    }

    // Walk toward <paramref name="leaderPos"/> in steps; idle when within the follow radius. Throttled so we
    // don't spam WalkTo packets every tick — the server will keep us moving once a path is in flight.
    // Followers with a SquadSlot walk to (leader + slot offset) so 6 bots don't pile onto one cell.
    private async Task<bool> CloseGapAsync(Snapshot snap, RebuildSharedData.Data.Position leaderPos, CancellationToken ct)
    {
        _targetId = 0;
        _targetClass = -1;
        var (followX, followY) = ComputeFollowCell(leaderPos);
        var dist = Math.Max(Math.Abs(snap.SelfPos.X - followX), Math.Abs(snap.SelfPos.Y - followY));
        if (dist > FollowRadius)
        {
            Mode = BotMode.Following;
            if (DateTime.UtcNow >= _nextFollowMove)
            {
                _nextFollowMove = DateTime.UtcNow.AddMilliseconds(700);
                await _bot.WalkToAsync(Math.Max(1, followX), Math.Max(1, followY), ct);
            }
        }
        else
        {
            // Close enough — shadow the leader but don't pretend to be doing nothing.
            Mode = BotMode.Following;
        }
        return true;
    }
}
