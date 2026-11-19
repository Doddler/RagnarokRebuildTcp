using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;

namespace RoBotClient.Bot.Behavior;

// Ranged-kite sub-FSM — per Other/RoBotClient/KITING_DESIGN.md.
//
// Architecture in one paragraph: ranged-class bots (Archer / Mage / Hunter / Wizard) often have a
// huge attack range advantage over their target (bow ≈ 9 tiles, mob ≈ 1). Standing still and trading
// hits gives up that advantage. The kite sub-FSM keeps the bot at (myRange - KiteBufferTiles) tiles
// from its target, attack-then-step rhythm, with a fallback step ladder when straight back is
// blocked, a trend-based dance-bug detector that switches to Anchor-and-Trade when the gap closes
// despite kiting, and multi-mob nearest-threat selection. Opt-in via _config.EnableKite — when off
// (default), the bot uses the existing melee-style attack-and-stand path.
//
// Single entry point: TryKiteEngagementAsync. Returns true if the kite handled the tick (stepped,
// fired, or held); false if the parent FSM should fall back to its melee path. Slots between
// TickSkillsAsync (which already drives skill rules) and the bare AttackAsync re-assert.
public sealed partial class BotBehavior
{
    // ---- master switch ----
    private enum KiteMode { Disabled, Active, Suspended }
    private KiteMode _kiteMode = KiteMode.Disabled;
    private DateTime _kiteSuspendedUntil = DateTime.MinValue;

    // ---- rhythm tracking ----
    private DateTime _kiteLastStepAt = DateTime.MinValue;
    private bool _kiteFireAfterStep;
    private int _kiteSweetSpot = 1;
    private int _kiteTargetClassId;       // detects target change → reset state
    private Position _kiteLastSelfPos;    // "did the step land?" via position-delta

    // ---- gap-closing trend ----
    private readonly Queue<(DateTime t, int dist)> _kiteDistanceSamples = new();
    private const int KiteSampleWindow = 6;
    private int _kiteStepsInWindow;

    // ---- perpendicular evade ----
    // Sticky direction so the bot doesn't whirl in place when the primary backward step is blocked.
    // -1/0/+1 per axis; refreshed only when we go several seconds without a perpendicular step.
    private (sbyte dx, sbyte dy) _kiteEvadeBias;
    private DateTime _kiteEvadeBiasSetAt = DateTime.MinValue;

    // ---- step-packet throttle ----
    // The server's path resolver fires on every WalkTo packet; sending more than ~3/sec just wastes
    // bandwidth and never lands. Cap at one step per 320 ms (≈3 Hz), comfortably below the 250 ms
    // tick rate but fast enough to outrun a Mandragora (MoveSpeed -0.001).
    private static readonly TimeSpan KiteStepCooldown = TimeSpan.FromMilliseconds(320);

    /// <summary>Pure helper: ideal Chebyshev distance from the target. Caller passes the bot's effective
    /// attack range, the mob's attack range, and the mob's MoveSpeed. Returns -1 when the bot can't
    /// outrange the mob (mobRange ≥ myRange) — caller treats that as "don't kite, fight normally".</summary>
    internal static int KiteDistanceTarget(int myAttackRange, int mobAttackRange, float mobMoveSpeed,
        int bufferTiles, float fastMobThreshold)
    {
        if (myAttackRange <= mobAttackRange) return -1;        // can't outrange — don't kite
        if (myAttackRange <= 2) return -1;                     // unequipped / melee weapon — not kiteable
        var buffer = Math.Max(0, bufferTiles);
        if (mobMoveSpeed > fastMobThreshold) buffer += 1;      // fast mobs get an extra-tile cushion
        var sweet = myAttackRange - buffer;
        // Floor: keep at least 2 tiles past the mob's reach. Plants (mobRange=1) → floor 3 → sweet ~8
        // for bow=9. Geographer (mobRange=3) → floor 5 → sweet ~8. Mob equal range was filtered above.
        var floor = mobAttackRange + 2;
        if (sweet < floor) sweet = floor;
        if (sweet > myAttackRange) sweet = myAttackRange;
        return sweet;
    }

    /// <summary>Per-target eligibility predicate — distilled §8 negative-space table. Returns false
    /// fast for bots/situations where kiting is the wrong tool (melee, tank, follower, stationary
    /// target, equal-range mob, low HP, recently-suspended). The parent FSM keeps owning the
    /// top-level mode (Fleeing/Shopping/etc.); this predicate just gates per-target eligibility.</summary>
    private bool ShouldKiteThisTarget(Snapshot snap, MonsterInfo target, int myRange, MonsterDbEntry? mDb)
    {
        if (!_config.EnableKite) return false;
        if (Mode is BotMode.Fleeing or BotMode.Shopping or BotMode.JobChange or BotMode.Parked or BotMode.Resting or BotMode.Following) return false;
        if (myRange <= 2) return false;                                  // melee weapon
        if (mDb == null) return false;                                    // unknown mob — be safe, no kite
        if (mDb.Range >= myRange) return false;                          // mob outranges or matches us
        if (mDb.MoveSpeed <= 0) return false;                            // stationary plant — just shoot
        if (_config.ResolveRole(snap.SelfJobId) == PartyRole.Tank) return false;
        if (snap.SelfMaxHp > 0 && snap.SelfHp < snap.SelfMaxHp * _config.FleeHpPercent) return false; // parent flee
        if (_kiteMode == KiteMode.Suspended && DateTime.UtcNow < _kiteSuspendedUntil) return false;   // hysteresis
        return true;
    }

    /// <summary>Drive one kite tick. Returns true if it handled the tick (stepped, fired, or held);
    /// false if the parent FSM should run its melee-attack fallback.</summary>
    private async Task<bool> TryKiteEngagementAsync(Snapshot snap, MonsterInfo target, CancellationToken ct)
    {
        var myRange = InferAttackRange(snap);
        var mDb = _data?.Monster(target.ClassId);
        if (!ShouldKiteThisTarget(snap, target, myRange, mDb)) return false;

        // Re-evaluate sweet spot if the target changed (different mob class → different range / speed).
        if (target.ClassId != _kiteTargetClassId)
        {
            _kiteTargetClassId = target.ClassId;
            _kiteSweetSpot = KiteDistanceTarget(myRange, mDb!.Range, mDb.MoveSpeed,
                _config.KiteBufferTiles, _config.KiteFastMobThreshold);
            ResetKiteSampling();
            _kiteMode = KiteMode.Active;
        }
        if (_kiteSweetSpot < 1) return false; // outranged / unkiteable — let parent FSM melee-fall back

        // Sample current gap; the dance-bug detector reads from this ring buffer.
        var distNow = ChebyshevDist(snap.SelfPos, target.Pos);
        PushDistanceSample(distNow);

        // Gap-closing detection. If positive (gap shrinking despite kiting), switch to Suspended.
        if (_kiteMode == KiteMode.Active && DetectDanceBug(distNow))
        {
            _kiteMode = KiteMode.Suspended;
            _kiteSuspendedUntil = DateTime.UtcNow.AddSeconds(_config.KiteSuspendSeconds);
            OnLog?.Invoke($"Kite suspended on '{target.Name}' — gap closing despite kiting. Anchoring for {_config.KiteSuspendSeconds:F1}s.");
            // Suspended: fall through to "attack on cooldown, don't move".
        }

        // Re-fire attack right after a step landed (the server drops the attack lock during a Walk).
        // SelfPos hasn't changed since the last tick → bot has stopped → ready to re-engage.
        if (_kiteFireAfterStep && snap.SelfPos.X == _kiteLastSelfPos.X && snap.SelfPos.Y == _kiteLastSelfPos.Y)
        {
            _kiteFireAfterStep = false;
            _nextAttack = DateTime.UtcNow.AddSeconds(3);
            if (!_config.NoAutoAttack)
                await _bot.AttackAsync(target.Id, ct);
        }
        _kiteLastSelfPos = snap.SelfPos;

        // Distance regime — decides whether to step, hold, or fire.
        // (a) In sweet spot AND gap stable → hold. (b) Inside sweet spot (mob too close) → step back.
        // (c) Outside max range → step in. Suspended skips stepping entirely.
        if (_kiteMode == KiteMode.Suspended)
        {
            // Anchor-and-trade: stand still, re-fire on _nextAttack like the melee path.
            if (DateTime.UtcNow >= _nextAttack && !_config.NoAutoAttack)
            {
                _nextAttack = DateTime.UtcNow.AddSeconds(3);
                await _bot.AttackAsync(target.Id, ct);
            }
            // Once the gap re-opens (mob's RandomMove drifted away, or stun expired), exit Suspended.
            if (distNow >= _kiteSweetSpot + 2) { _kiteMode = KiteMode.Active; ResetKiteSampling(); }
            return true;
        }

        var stepBackThreshold = _kiteSweetSpot;             // step back when mob reaches the sweet spot
        var maxRange = myRange;                             // step in when outside max range
        var stepCooldownReady = DateTime.UtcNow - _kiteLastStepAt >= KiteStepCooldown;

        // Don't interrupt an imminent swing. If the auto-attack is about to fire (less than
        // attackInterval/3 away), let it land first, then step on the next tick.
        var attackInterval = snap.SelfAttackInterval > 0.05 ? snap.SelfAttackInterval : 1.5;
        var swingImminent = (_nextAttack - DateTime.UtcNow).TotalSeconds < attackInterval / 3.0
                           && _nextAttack > DateTime.UtcNow;

        // MULTI-MOB SAFETY: am I in range of any OTHER immobile ranged mob besides my target? Plant maps
        // (Mandragora, Geographer, Hydra) cluster — the bot might step out of target A's range and into
        // target B's. If so, find a relocation cell that's STILL in my target's range but outside every
        // other immobile mob's range. Skips the regular sweet-spot stepping for this tick.
        if (stepCooldownReady && !swingImminent && IsInAnyOtherMobRange(snap, target))
        {
            if (TryFindSafeEngagementCell(snap, target, myRange, out var sx, out var sy))
            {
                _kiteLastStepAt = DateTime.UtcNow;
                _kiteStepsInWindow++;
                _kiteFireAfterStep = true;
                OnLog?.Invoke($"Kite: relocating to ({sx},{sy}) — currently in range of another immobile mob.");
                await _bot.WalkToAsync(Math.Max(1, sx), Math.Max(1, sy), ct);
                return true;
            }
            // No safe cell exists — every reachable cell in target's range is also in some other mob's
            // range. Fall through to standard kite behavior; the bot will take hits but still progress.
        }

        if (distNow > maxRange)
        {
            // TOO FAR — close in. Use the bot's existing WalkPathToward (handles portals/hazards).
            if (stepCooldownReady)
            {
                _kiteLastStepAt = DateTime.UtcNow;
                await WalkPathTowardAsync(snap, target.Pos.X, target.Pos.Y, 6, ct);
                _kiteFireAfterStep = true;
                _kiteStepsInWindow++;
            }
            return true;
        }

        if (distNow <= stepBackThreshold && !swingImminent && stepCooldownReady)
        {
            // TOO CLOSE — step back one tile (or sideways if back is blocked).
            if (TryPickKiteStep(snap, target, out var gx, out var gy))
            {
                _kiteLastStepAt = DateTime.UtcNow;
                _kiteStepsInWindow++;
                _kiteFireAfterStep = true;
                await _bot.WalkToAsync(Math.Max(1, gx), Math.Max(1, gy), ct);
                return true;
            }
            // No backward cell available — face-tank this tick; the dance-bug detector will
            // eventually flip us to Suspended if this becomes the norm.
        }

        // SETTLED — at sweet spot OR swing-imminent OR step-cooldown-cooling. Fire on cooldown.
        if (DateTime.UtcNow >= _nextAttack && !_config.NoAutoAttack)
        {
            _nextAttack = DateTime.UtcNow.AddSeconds(3);
            await _bot.AttackAsync(target.Id, ct);
        }
        return true;
    }

    /// <summary>Cell-picker: returns the best backward step. Walks the fallback ladder — primary
    /// backward → diagonal neighbours → perpendicular (with sticky evade-bias) → forward-and-around.
    /// Returns false when nothing acceptable exists; caller face-tanks this tick.</summary>
    private bool TryPickKiteStep(Snapshot snap, MonsterInfo target, out int gx, out int gy)
    {
        gx = gy = 0;
        var self = snap.SelfPos;
        var mob = target.Pos;
        var bdx = Math.Sign(self.X - mob.X);
        var bdy = Math.Sign(self.Y - mob.Y);
        // Bot exactly on top of mob (distNow == 0): pick any direction. Use the evade bias if we have
        // one, else north — arbitrary stable choice prevents Brownian flailing.
        if (bdx == 0 && bdy == 0)
        {
            bdx = _kiteEvadeBias.dx != 0 ? _kiteEvadeBias.dx : (sbyte)0;
            bdy = _kiteEvadeBias.dy != 0 ? _kiteEvadeBias.dy : (sbyte)-1;
        }

        // Candidate ladder, in priority order.
        var candidates = new (int dx, int dy)[]
        {
            (bdx, bdy),                              // 1. straight back
            (bdx, 0),                                // 2a. diagonal-neighbour: drop the y component
            (0, bdy),                                // 2b. diagonal-neighbour: drop the x component
            (-bdy, bdx),                             // 3a. perpendicular (rotate +90°)
            ( bdy,-bdx),                             // 3b. perpendicular (rotate -90°)
            ( bdx,-bdy),                             // 4a. forward-and-around (flip y)
            (-bdx, bdy),                             // 4b. forward-and-around (flip x)
        };

        // Sticky perpendicular bias: if we used a perpendicular in the last few seconds, try that
        // side first this time too.
        var biasFresh = DateTime.UtcNow - _kiteEvadeBiasSetAt < TimeSpan.FromSeconds(1.5);
        if (biasFresh && _kiteEvadeBias.dx != 0 && _kiteEvadeBias.dy != 0)
        {
            // Swap (3a/3b) so the biased side is tried first.
            if (_kiteEvadeBias.dx == -bdy && _kiteEvadeBias.dy == bdx)
                (candidates[3], candidates[4]) = (candidates[4], candidates[3]);
        }

        var walk = _data?.GetWalkMap(snap.Map);

        for (var i = 0; i < candidates.Length; i++)
        {
            var (dx, dy) = candidates[i];
            if (dx == 0 && dy == 0) continue;
            var cx = self.X + dx;
            var cy = self.Y + dy;
            if (cx <= 0 || cy <= 0) continue;
            if (walk != null && !walk.IsWalkable(cx, cy)) continue;
            if (CellNearHazard(cx, cy)) continue;
            if (_data != null && _data.World.IsInPortalFootprint(snap.Map, cx, cy, margin: 1)) continue;
            // Don't step into another mob's attack reach. (The CURRENT target is fine — we want to
            // be in OUR range, not theirs.)
            if (StepCellNearOtherMob(snap, target, cx, cy)) continue;
            gx = cx; gy = cy;
            // If we just chose a perpendicular (#3a/b) or fwd-around (#4a/b), record the bias.
            if (i >= 3)
            {
                _kiteEvadeBias = ((sbyte)dx, (sbyte)dy);
                _kiteEvadeBiasSetAt = DateTime.UtcNow;
            }
            return true;
        }
        return false;
    }

    /// <summary>True if cell (cx, cy) is within reach of any OTHER monster on the map — kiting back
    /// from target A into target B's reach is a no-op. The current target is excluded; stepping into
    /// our own target's range is fine because that's where we want to be (we just don't want to be
    /// THERE while THEY can hit us — handled by the sweet-spot logic).</summary>
    private bool StepCellNearOtherMob(Snapshot snap, MonsterInfo currentTarget, int cx, int cy)
    {
        if (_data == null) return false;
        for (var i = 0; i < snap.Monsters.Count; i++)
        {
            var m = snap.Monsters[i];
            if (m.Id == currentTarget.Id) continue;
            if (m.Hp <= 0) continue;
            var db = _data.Monster(m.ClassId);
            var range = db?.Range ?? 1;
            var d = Math.Max(Math.Abs(m.Pos.X - cx), Math.Abs(m.Pos.Y - cy));
            if (d <= range + 1) return true;
        }
        return false;
    }

    /// <summary>True when the bot's CURRENT cell is in attack range of any non-target immobile ranged
    /// mob. Mobile mobs are handled by the hazard / flee system; here we only care about plants and
    /// AiAggressiveImmobile sprites whose attack zones are fixed and additive.</summary>
    private bool IsInAnyOtherMobRange(Snapshot snap, MonsterInfo currentTarget)
    {
        if (_data == null) return false;
        var sx = snap.SelfPos.X;
        var sy = snap.SelfPos.Y;
        for (var i = 0; i < snap.Monsters.Count; i++)
        {
            var m = snap.Monsters[i];
            if (m.Id == currentTarget.Id) continue;
            if (m.Hp <= 0) continue;
            var db = _data.Monster(m.ClassId);
            if (!IsMonsterImmobile(db)) continue;
            var range = db?.Range ?? 1;
            if (range < 2) continue; // melee plant has no projectile zone to dodge
            var d = Math.Max(Math.Abs(m.Pos.X - sx), Math.Abs(m.Pos.Y - sy));
            if (d <= range) return true;
        }
        return false;
    }

    /// <summary>Find the closest cell (to the bot) that satisfies:
    /// (a) Chebyshev distance to <paramref name="target"/> is in <c>[mobRange+2, myAttackRange]</c> —
    ///     bot can hit the target, target can't hit the bot.
    /// (b) Outside every OTHER visible immobile ranged mob's attack zone.
    /// (c) Walkable, not on a portal footprint, not in a hazard.
    /// Scans rings around the target from <c>myAttackRange</c> down to <c>mobRange+2</c>; prefers
    /// outer rings (max range = max safety) then nearest to bot to minimise the walk. Returns false
    /// when no cell satisfies all constraints — caller falls through to existing kite behavior.</summary>
    private bool TryFindSafeEngagementCell(Snapshot snap, MonsterInfo target, int myRange, out int gx, out int gy)
    {
        gx = gy = 0;
        if (_data == null) return false;
        var targetDb = _data.Monster(target.ClassId);
        var targetRange = targetDb?.Range ?? 1;
        var minD = Math.Max(1, targetRange + 2);
        var maxD = Math.Max(minD, myRange);
        if (minD > maxD) return false; // can't outrange this target

        // Snapshot the hostile zones (other immobile ranged mobs) once.
        var hostiles = new List<(int x, int y, int range)>(snap.Monsters.Count);
        for (var i = 0; i < snap.Monsters.Count; i++)
        {
            var m = snap.Monsters[i];
            if (m.Id == target.Id || m.Hp <= 0) continue;
            var db = _data.Monster(m.ClassId);
            if (!IsMonsterImmobile(db)) continue;
            var r = db?.Range ?? 1;
            if (r < 2) continue;
            hostiles.Add((m.Pos.X, m.Pos.Y, r));
        }

        var walk = _data.GetWalkMap(snap.Map);
        var self = snap.SelfPos;

        // Scan from outer ring (max safety) inward. Stop at the first ring that yields a cell.
        for (var d = maxD; d >= minD; d--)
        {
            var bestScore = int.MaxValue;
            var bestX = 0; var bestY = 0;
            for (var ox = -d; ox <= d; ox++)
            for (var oy = -d; oy <= d; oy++)
            {
                if (Math.Max(Math.Abs(ox), Math.Abs(oy)) != d) continue; // perimeter only
                var cx = target.Pos.X + ox;
                var cy = target.Pos.Y + oy;
                if (cx <= 0 || cy <= 0) continue;
                if (walk != null && !walk.IsWalkable(cx, cy)) continue;
                if (CellNearHazard(cx, cy)) continue;
                if (_data.World.IsInPortalFootprint(snap.Map, cx, cy, margin: 1)) continue;
                // Disqualify if inside any hostile zone.
                var safe = true;
                for (var i = 0; i < hostiles.Count; i++)
                {
                    var h = hostiles[i];
                    var hd = Math.Max(Math.Abs(h.x - cx), Math.Abs(h.y - cy));
                    if (hd <= h.range) { safe = false; break; }
                }
                if (!safe) continue;
                // Score: walk distance from current cell — minimise the relocation step.
                var score = Math.Max(Math.Abs(cx - self.X), Math.Abs(cy - self.Y));
                if (score < bestScore) { bestScore = score; bestX = cx; bestY = cy; }
            }
            if (bestScore != int.MaxValue) { gx = bestX; gy = bestY; return true; }
        }
        return false;
    }

    private void PushDistanceSample(int distNow)
    {
        _kiteDistanceSamples.Enqueue((DateTime.UtcNow, distNow));
        while (_kiteDistanceSamples.Count > KiteSampleWindow) _kiteDistanceSamples.Dequeue();
    }

    private void ResetKiteSampling()
    {
        _kiteDistanceSamples.Clear();
        _kiteStepsInWindow = 0;
    }

    /// <summary>Returns true when the bot has stepped ≥3 times in the sample window AND the gap is
    /// shrinking faster than -0.3 tile/sec. The "we tried" guard prevents a single missed step from
    /// flipping us into Suspended.</summary>
    private bool DetectDanceBug(int distNow)
    {
        if (_kiteDistanceSamples.Count < 3) return false;
        if (_kiteStepsInWindow < 3) return false;
        var oldest = _kiteDistanceSamples.Peek();
        var dt = (DateTime.UtcNow - oldest.t).TotalSeconds;
        if (dt < 0.5) return false; // not enough time to be a trend
        var slope = (distNow - oldest.dist) / dt;
        return slope < -0.3;
    }

    private static int ChebyshevDist(Position a, Position b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
