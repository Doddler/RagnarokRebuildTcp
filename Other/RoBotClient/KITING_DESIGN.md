# Kiting design — ranged bot classes (Archer, Mage)

Design notes for adding a kiting sub-FSM to `BotBehavior`. The existing FSM is a tick-based brain (`TickAsync`, 250 ms cadence) that already commits to a target, runs `BattleSimulator` forecasts, paths over the walkmap, and avoids hazards. This document specifies a self-contained "engaged-with-target" sub-FSM that owns positioning while the parent FSM still owns target selection, healing, fleeing, and shopping.

Nothing here proposes new packet types. Kiting is built on the existing primitives:

- `_bot.AttackAsync(targetId, ct)` — re-asserts the attack lock against a target id.
- `_bot.WalkToAsync(x, y, ct)` — sends `WalkTo`; the server pathfinder resolves.
- `_bot.UseSkillOnTargetAsync(skill, level, targetId, ct)` — single-target cast.
- The per-tick `Snapshot` already exposes self pos, monsters with positions/HP, known skills, attack interval, and statuses.
- `_data.Monster(classId)` returns the full `MonsterDbEntry`: `.Range`, `.ScanDist`, `.MoveSpeed`, `.Ai`.

The kiter does NOT replace `Engaging`/`Hunting`. It is a *positioning sub-mode* that the hunting branch consults instead of "stand still and attack." When the bot is committed to a target and the situation matches the kite preconditions, the sub-FSM picks the cell to move to (or to *not* move to) each tick.

---

## 1. Kite-attack decision loop

### Per-tick FSM

The kite sub-FSM is a small state machine layered on top of the existing hunting branch in `TickAsync`. Pseudocode:

```
// Called from the hunting branch instead of the bare `AttackAsync(_targetId)` line.
// Returns true if the kite handled this tick (either fired or repositioned);
// false if the caller should fall back to plain melee approach.
TryKiteEngagement(snap, target):
    if !ShouldKiteThisTarget(snap, target):
        return false                       // melee bot, immobile target, or kiting disabled

    state = ResolveKiteState(snap, target)

    switch state:
        case Settled:                      // at sweet-spot distance, mob not closing
            if AttackReady(snap):
                FireAttackOrSkill(snap, target)
            // else: hold position, do nothing this tick.
            return true

        case TooClose:                     // mob is at or inside step-back threshold
            if CanStepBack(snap, target):
                StepBackOneTile(snap, target)
                _kiteLastStepAt = now
            else if MoveBlocked():
                state = PerpendicularEvade  // wall behind us; circle-strafe
                StepPerpendicular(snap, target)
            else:
                // No room to kite — accept face-tank, fire on cooldown.
                if AttackReady(snap): FireAttackOrSkill(snap, target)
            return true

        case TooFar:                       // out of attack range; close in
            if AttackReady(snap) && CastTimeIsZero(rule):
                // E.g. a 9-range FireBolt: still in range, no need to close.
                FireAttackOrSkill(snap, target)
            else:
                StepTowardSweetSpot(snap, target)
            return true

        case GapClosing:                   // distance shrinking despite kiting — see §6
            if DanceBugDetected():
                AbortKiteFaceTank()
            return true

        case Abandon:                      // multi-aggro, low HP, etc. — let parent FSM handle it
            return false
```

### State the sub-FSM owns

These fields belong on `BotBehavior` next to `_targetId` / `_nextAttack`:

| Field | Purpose |
|---|---|
| `_kiteMode` (enum: `Disabled`, `Active`, `Suspended`) | Master switch. Suspended = kiting was tried and failed (dance-bug), face-tank instead. |
| `_kiteLastStepAt` (DateTime) | Throttles step packets — we send at most one `WalkTo` per ~300 ms regardless of tick rate. |
| `_kiteSweetSpot` (int) | Cached ideal distance for the current target. Recomputed on target change. |
| `_kiteLastDistanceSamples` (small ring buffer of recent self-target distances + timestamps) | Gap-closing detection (§6). Three or four samples is enough. |
| `_kiteEvadeBias` (sbyte, -1/0/+1 per axis) | Persisted perpendicular direction when circle-strafing — prevents oscillating left/right each tick. |
| `_kiteFireAfterStep` (bool) | "Attack next tick" flag, set whenever a step completes so we resume the attack rhythm cleanly (§5). |

### Pre-emption rules (when to abandon and let the parent FSM take over)

The kite sub-FSM returns `false` (abandon) when ANY of these is true:

1. **Multi-aggro emergency.** `EngagementGroup(snap, target).Count > 1` AND the group forecast says we'd lose — same check that's already in `PickTarget`. Hazards already short-circuit at the top of `TickAsync`, so this catches mid-fight assist-mob arrivals.
2. **HP danger.** `selfHpPct < _config.FleeHpPercent`. Kiting can't outrun a missed potion timing. Let `AvoidHazards` / parent flee path drive.
3. **Target stopped existing.** `target == null` — back to `PickTarget`.
4. **Stuck.** Same `IdleSeconds > StuckSeconds` rule the parent already runs. A failing kite is just a stuck bot.
5. **Skill cast is pending and uninterruptible.** Don't step while a FireBolt cast is in flight (§4) — let it land and re-evaluate next tick.
6. **Squad-follower mode.** Followers' positions are dictated by `SquadSlot` formation; they don't kite independently. (The leader, when a ranged class, kites freely.)

### Hand-off contract

The kite call site is the existing target-committed branch of `TickAsync`:

```csharp
if (current != null && IsHuntable(current))
{
    // ... existing stuck / mode setup ...
    if (await TickSkillsAsync(snap, current, ct)) return;

    if (await TryKiteEngagementAsync(snap, current, ct)) return;

    // Melee fallback path — what the FSM does today.
    if (DateTime.UtcNow >= _nextAttack)
    {
        _nextAttack = DateTime.UtcNow.AddSeconds(3);
        await _bot.AttackAsync(_targetId, ct);
    }
    return;
}
```

Skills still run first (`TickSkillsAsync`), because a user-written FireBolt rule firing IS the kite's "attack" — the kiter just needs to position so the rule can fire. The skill DSL doesn't know about positioning; the kite does.

---

## 2. Optimum-range definition

Let:

- `myRange` = effective attack range (bow item range, or skill range; ~9 in this server for both).
- `mobRange` = `_data.Monster(target.ClassId).Range`. Plant types like Mandragora/Geographer: 1 (melee).
- `mobSpeed` = `_data.Monster(target.ClassId).MoveSpeed` (tiles/sec).
- `myStepCadence` = approximate tile/sec we can move (bounded by 250–500 ms action throttle and server walk speed).

Two candidate definitions for "best range":

### A. Maximum-range — `kiteDist = myRange`

The farthest cell from which we can still hit. Mob has to traverse the entire gap before it can reciprocate. **Best DPS uptime** on slow targets: every tick we're firing and the mob is walking.

Failure mode: a single tile of slip — server desync, path detour around a tree, a step missed because we were locked in an animation — and the mob enters its own `Range`. Now we're trading.

### B. Sweet-spot — `kiteDist = myRange - 1` (preferred default)

One tile inside max range. If the mob closes by 1, we still have time to step back to `myRange` before its next swing. The buffer makes the system tolerant of one cell of error per kite cycle.

DPS cost: minor. The mob still has to walk `myRange - 1` cells before hitting us, which is many ticks against a slow mob.

### C. Adaptive — `kiteDist = clamp(myRange - 1 - mobSpeedFudge, mobRange + 2, myRange)`

For mobs with a slight speed advantage we *increase* the kite distance to give ourselves more reaction headroom. For mobs that are extremely slow (Mandragora) we *could* shrink the buffer back to `myRange` because the gap-closing-per-tick is so low — but the win is tiny and the failure cost is real, so default sticks with `myRange - 1`.

### Recommendation: option B as default, option C as a tunable

`KiteDistanceTarget(myRange, mobRange, mobSpeed)` returns:

```
let buffer = mobSpeed > FastMobThresholdTilesPerSec ? 2 : 1
let target = clamp(myRange - buffer, mobRange + 2, myRange)
return target
```

`FastMobThresholdTilesPerSec` ≈ 2.0 (Mandragora is around 0.5; fast wolves are 3+).

Floor of `mobRange + 2` keeps us out of any future melee/ranged mob's own reach by at least 2 cells. Plants (`mobRange = 1`) → floor is 3 → typical sweet spot 8 with bow range 9. Mob with `mobRange = 5` → floor is 7 → sweet spot still 8. Mob with `mobRange = 9` (ranged equal to us): floor is 11, max is 9 — clamps to 9 (no buffer possible). The kite sub-FSM should detect that case and refuse to kite — fight on equal footing instead.

### Tradeoffs cheat-sheet

| Range | Pro | Con |
|---|---|---|
| `myRange` (max) | DPS uptime; mob never in reciprocal range while we hold | Single slip = trade; sensitive to lag |
| `myRange - 1` | One-step buffer; tolerant of small drift | One less attack per kite cycle vs max |
| `myRange - 2` | Comfortable; survives lag spikes | Wastes DPS uptime; mob's slow approach still triggers a step |
| `myRange / 2` | Trivial to maintain | Defeats the point of being ranged |

### Edge case: bot range upgrades

`Self.AttackRange` isn't currently surfaced in the snapshot. The kiter has two options:

1. Hardcode `myRange = 9` for Archer (bow) / Mage (bolt) classes and accept that an unequipped Mage with no bow falls into melee fallback.
2. Add `SelfAttackRange` to `Snapshot` (server has it via `SelfState`; needs a wire path or a static lookup table by job + equipped weapon). This is the cleaner long-term move.

For the first cut, option 1 — a `static int RangedClassAttackRange(int jobId) => 9` keyed by Archer/Mage/Wizard/Hunter — is fine.

---

## 2.5. Closer / gap-closing monsters

When `mobSpeed > kiteableSpeed`, a naive kiter loses ground every cycle. The bot still needs a strategy.

### Classification by relative speed

Let `kiteSpeedHeadroom = myStepCadence - mobSpeed` (tiles/sec the gap grows per second of kiting).

| Regime | `kiteSpeedHeadroom` | Strategy |
|---|---|---|
| **Comfortably slower mob** | > 1.0 (Mandragora, Plant types) | Kite freely. Lazy step rate is fine. |
| **Roughly equal** | -0.5..1.0 | Kite aggressively, but be ready to switch to face-tank if dance-bug detected (§6). |
| **Mob faster but close** | -1.5..-0.5 | Hybrid: kite for the FIRST salvo (free hits), then face-tank when caught. |
| **Mob much faster** | < -1.5 | Don't bother with positioning — fire on cooldown, accept melee, let the BattleSimulator forecast decide whether we engage at all. |

These thresholds come from intuition, not measurement. Tune after the first stress run.

### Status-effect kiting (the secret weapon)

The kite sub-FSM gets *much* more powerful when paired with snare/slow skills. The Skill DSL already supports `target.status.<name>` checks, so a Mage script can be:

```
use FrostDiver 0 enemy if target.status.frozen == 0 and target.dist <= 9
use ColdBolt 0 enemy if target.status.frozen == 1
```

The kiter doesn't fire FrostDiver itself — the skill script does — but the kiter should be *aware* of slows when picking a strategy. Suggested LhsValue extension (later, optional):

```
target.slowed   → 1 if Frozen, Stone, Sleep, or any status with movement-disabling category
target.no_threat → 1 if slowed AND mobRange < my distance
```

When the target is slowed, treat speed as 0 for the kite regime classification — fast mobs become trivially kiteable while iced.

### Trade-DPS-for-survival when caught

If the regime is "Mob faster but close" and the mob is already in its own attack range:

- **Stop kiting.** Stepping back gives one missed swing (animation interrupted) and the mob walks the cell back anyway.
- **Switch to "anchor and trade."** Stand still, fire on every cooldown. Heal from inventory when triggered.
- **Re-enable kiting** the moment the gap reopens — e.g. the mob got stunned, knockback'd, or its `RandomMove` AI rolled away. Recheck distance every tick; once `selfTargetDist >= mySweetSpot`, resume.

This is what the design doc means by "still try to maintain max range but acknowledge defeat against gap-closers" — we don't abandon the kite, we suspend it.

### Flee threshold

If `selfHpPct < _config.FleeHpPercent` while face-tanking a gap-closer, parent FSM `AvoidHazards` already kicks in (the mob will be classed as a hazard once the forecast flips). We don't need a separate flee path in the kite.

---

## 3. Step-direction choice

Kiting is "move along the vector pointing from mob to self." On an 8-connected grid, that vector quantizes to one of 8 unit directions plus stay-put.

### Primary kite direction

```
let dx = sign(self.X - mob.X)
let dy = sign(self.Y - mob.Y)
// (0,0) impossible if a step is needed — at distance 0 mob is on our cell.
// (±1, 0) / (0, ±1) / (±1, ±1) — eight candidate steps.
let stepTarget = (self.X + dx, self.Y + dy)
```

### Fallback ladder

Pure "step backward" fails when the obvious cell is unwalkable (wall, water, NPC), unsafe (hazard footprint, portal apron), or off the map. Order of fallbacks:

1. **Primary backward step** — `(self + (dx, dy))`. Walkable + not in `_hazards` + not in portal footprint.
2. **Adjacent backward steps** — try the two diagonals on either side of the primary. E.g. if primary is `(+1, +1)`, also try `(+1, 0)` and `(0, +1)`. Prefer the one that doesn't *decrease* distance to mob.
3. **Perpendicular kite (circle-strafe)** — when straight back is blocked but sideways is open. Compute the two perpendicular directions to the (dx, dy) vector; pick the one biased by `_kiteEvadeBias` (set on first perpendicular step, sticky for ~1 s so the bot doesn't whirl in place).
4. **Forward-and-around** — if all back/sideways cells are blocked: step *toward* the mob by one diagonal that opens up a path around it. Brief commitment until a clear backward cell appears.
5. **Bail to AvoidHazards path** — if even forward-and-around fails, set `_kiteMode = Suspended` and return false to the parent FSM. Parent runs the normal stuck-recovery / wander loop.

### Hazard / mob awareness behind

The intended step cell must be checked against the existing `_hazards` list (it already covers aggressive mobs whose forecast we lose). A backward step *toward* another hostile mob is worse than face-tanking the current one. Reuse `CellNearHazard(x, y)`.

In addition, any mob — hazard or not — within `mobRange + 1` of the destination cell should disqualify that cell. Stepping back from Mandragora A into Mandragora B's reach is a no-op.

```
DisqualifyCell(cx, cy, snap, currentTarget):
    if !map.IsWalkable(cx, cy): return true
    if CellNearHazard(cx, cy): return true
    if _data.World.IsInPortalFootprint(snap.Map, cx, cy, margin: 1): return true
    foreach m in snap.Monsters:
        if m.Id == currentTarget.Id: continue
        if m.IsAlive && Chebyshev(m.Pos, (cx, cy)) <= MobRangeOf(m) + 1: return true
    return false
```

### Map-edge / portal handling

The existing `_data.World.IsInPortalFootprint(...)` and `PortalSafeDistance` config already cover "don't kite onto a portal cell." Reuse without modification.

Map edges are walkability bits — `IsWalkable` returns false outside the grid. The fallback ladder naturally handles this case.

### Why not full A*?

The walkmap's `FindPath` is the right tool for routes, not for one-tile kite steps. Two reasons:

1. **Cost.** Pathfinding every tick at 4 Hz across 6 bots is overkill when 90 % of kite ticks need a single cell decision.
2. **Behavior.** A* finds the shortest path to a *goal*. Kiting doesn't have a goal cell — it has a *direction*. Encoding that as an A* problem requires synthesizing a goal cell that's "far from the mob," which is exactly what we're trying to do in O(8) candidate checks.

A* still gets used inside `TickAsync` for travel and reachability; the kiter just doesn't invoke it on the fast path.

---

## 4. Cast-time vs movement

Skills with cast time root the caster. Stepping cancels the cast. The interaction between the kite step rhythm and a pending cast needs an explicit policy.

### The decision when the mob is "close enough to be a problem"

State: cast was started `t_cast` seconds ago, cast time is `T`, current self–target distance is `d`, mob range is `mobRange`. Three branches:

1. **`t_cast / T >= 0.7`** — Past the point of no return. Cast almost done; stepping wastes the entire cast plus its SP cost. **Finish the cast.** Accept the incoming hit if `d <= mobRange` next tick.
2. **`t_cast / T < 0.3` AND `d <= mobRange`** — Cast just started, mob already in reach. **Cancel and step.** The cast cost is low (a fraction of SP), the missed hit on the mob is recovered by being alive to fire next cycle.
3. **`d > mobRange + 1`** — Mob can't hit us this tick anyway. **Always finish the cast.** This is the cleanest kiting state and the common case for slow plants.

In the middle band (30 %–70 %) decide on cast value:

- If the cast is a *finisher* (target HP will go to 0): finish it. The reward is closing the fight.
- Else: cancel and step.

### "But the bot doesn't know cast progress"

True today — `BotBehavior` fires a skill via `UseSkillOnTargetAsync` and assumes the server eats it. Cast time isn't exposed in `Snapshot`. Two options:

1. **Pessimistic (start now):** treat any active cast as "uninterruptible until `_skillCooldown[skill]` elapses." This loses some kite responsiveness but is safe and trivial — `if (DateTime.UtcNow < _skillCooldown[lastCastSkill]) holdPosition()`.
2. **Cast-aware (later):** stamp `_castStartedAt` and a per-skill expected cast time table when we send the packet. Then the policy above works.

Option 1 is enough for the first cut.

### Practical: snap a single "no-step" window after each cast send

```csharp
await _bot.UseSkillOnTargetAsync(skill, level, target.Id, ct);
_kiteNoStepUntil = DateTime.UtcNow.AddSeconds(EstimatedCastTime(skill, snap));
```

While `DateTime.UtcNow < _kiteNoStepUntil`, the kiter holds position even if the mob enters its attack range. After that window elapses, kiting resumes normally.

`EstimatedCastTime` is a small static table. FireBolt L1 is ~0.7 s; L10 closer to 1.0 s. Conservative defaults are fine.

---

## 5. Animation lock / GCD alignment

Auto-attacks have a per-attack delay (`Snapshot.SelfAttackInterval` — `AttackSpeed` in seconds). Sending `WalkTo` during a swing interrupts the swing. A naive kiter that steps every tick at 4 Hz with a 1.5 s attack interval would fire ZERO auto-attacks — every swing gets cancelled by the next step.

### Rhythm: attack-then-step, not step-then-attack

The standard ranged kite pattern:

```
state: WaitForAttackLand
  on attack-landed (or _nextAttack elapsed):
    if mobDist < kiteSweetSpot:
      step back one cell
      state = WaitForStepComplete
    else:
      stand still until next attack
      (the server will fire the next swing automatically)

state: WaitForStepComplete
  when WalkTo finishes (or we've moved by 1 tile):
    re-send AttackAsync(target)  -- re-establish lock; mob may have moved
    state = WaitForAttackLand
```

### Re-asserting the attack after a step

Stepping cancels the attack lock server-side (the bot is now in `Walk` state, not `Attack`). The kite must re-send `AttackAsync(_targetId)` immediately after a step finishes — otherwise the bot stands at the new cell, never firing. The existing 3-second re-assert on `_nextAttack` is too slow for the kite rhythm.

Suggested implementation: after issuing a step, set `_kiteFireAfterStep = true`. On the next tick (or next-but-one to give the server time to land the move):

```csharp
if (_kiteFireAfterStep && SelfFinishedStepping(snap)) {
    _kiteFireAfterStep = false;
    _nextAttack = DateTime.UtcNow.AddSeconds(_kiteAttackReassertCooldown);
    await _bot.AttackAsync(_targetId, ct);
}
```

`SelfFinishedStepping` can be a simple "self pos hasn't moved since last tick" check — the bot's `WalkTo` packet completes when it reaches the cell.

### When to NOT step (let the swing land)

If `_nextAttack - now < attackInterval / 3` (i.e. an auto-attack is about to fire), hold position even if the mob just entered the step-back threshold. Eat the swing-and-half-step trade for the one missed cycle, then step. Otherwise you lose the swing AND the mob still closes by 1 tile.

This means the step-back threshold should be the sweet spot itself, *not* `sweetSpot - 1`. By the time the mob is at `sweetSpot`, the bot has between 0 and `attackInterval` seconds to fire one more time before stepping. That extra hit matters over a fight.

### Skills follow the same rhythm

A skill cast occupies its own cooldown window in `_skillCooldown[skill]`. Treat it as the attack rhythm for steady-state kiting — fire skill, *then* step (or hold), *then* fire next skill on the global skill throttle (`_nextSkill` = 250 ms between casts).

---

## 6. Dance-bug detection

If the mob's effective movement speed (cells closed per second toward the bot) exceeds the bot's effective step cadence (cells stepped back per second), kiting is a loss: the bot loses one tile per cycle, never fires (the step interrupts the swing), and dies to a sit-and-take-it loop.

### Detection: gap-trend ring buffer

Sample self–target distance every tick. Keep the last N (say 6) samples with timestamps. Compute the trend:

```
trendSlope = (distance[now] - distance[now - 1.5s]) / 1.5
```

(Approximate; use the oldest sample in the buffer as the denominator.)

Classification:

- `trendSlope >= 0` — kiting is working (gap stable or growing). Continue.
- `-0.3 <= trendSlope < 0` — gap closing slowly. Tolerable; mob might be running a `RandomMove` AI cycle. Don't react yet.
- `trendSlope < -0.3` — mob is closing despite kiting AND we've stepped at least 3 times in the window. Dance-bug — abort.

Three steps in the window is the "we tried" predicate — without it, a single missed step could trigger the abort.

### Action on detection: `Suspended` mode

```
state Suspended:
  // Don't kite. Anchor and trade.
  if AttackReady(snap): FireAttackOrSkill(snap, target)
  // Stay suspended for K seconds OR until gap re-opens via mob's own AI movement.
  if mobDist >= mySweetSpot + 2: _kiteMode = Active   // restored
  if now > _kiteSuspendedUntil:
    _kiteSuspendedUntil = now + 5s; recheck trend
```

Suspended doesn't mean disengage. It means "fire on cooldown, don't move." If the BattleSimulator forecast said we win face-tanking, we still win face-tanking. Letting the parent FSM handle this is wrong — parent will just see "no progress, walk somewhere" and lose the target.

### Why a hysteresis?

Once dance-bug is detected, the bot must NOT immediately re-enable kiting on the next tick where the gap happens to be one cell wider — the mob's random-walk noise would make this happen constantly. A `_kiteSuspendedUntil` window of a few seconds prevents the suspended/active oscillation.

### Logging

Log the transition once:

```
OnLog?.Invoke($"Kite suspended on '{target.Name}' — gap closing at {-trendSlope:F2} tile/s despite {steps} steps. Anchoring.");
```

The user can grep these to identify mobs that should be added to a "don't try to kite" classId set.

---

## 7. Multi-mob kiting

Pulling two mobs is the failure mode of single-target kiting. The optimum step is no longer "away from THE mob" but "away from ALL threats while still in range of the chosen target."

### Defining "away" with multiple sources

Two simple aggregations to consider:

**A. Centroid retreat.** Step opposite the mean position of all threatening mobs.

```
let cx = mean(m.X for m in threats)
let cy = mean(m.Y for m in threats)
let dx = sign(self.X - cx)
let dy = sign(self.Y - cy)
```

Cheap, but biases toward the geometric center, which can be wrong when threats are spread (e.g. one in front, one behind — centroid is on the bot, vector is (0,0)).

**B. Nearest-threat retreat.** Step opposite only the closest threat.

```
let nearest = argmin(threats, by chebyshev distance to self)
let dx = sign(self.X - nearest.X)
let dy = sign(self.Y - nearest.Y)
```

Robust against "pincer" cases — always opens the gap with whichever mob is actually about to hit us. The cost: the second mob might be getting closer at the same time we're stepping away from the first. Combined with cell-disqualification (don't step toward any mob that would now be in its own reach), this is usually safe.

**Recommendation: hybrid.** Use nearest-threat as the primary direction, but disqualify any candidate cell where the *distance to the SECOND nearest threat* would drop below its `mobRange + 1`. The fallback ladder (§3) then naturally picks a perpendicular or alternate cell.

### Target rotation

If two mobs are equally close and one is at low HP (`hpPct < 30 %`), switch the kite target to the low-HP one to remove it from the pool faster. The parent FSM commits to a target until it dies, so this needs a "kite-target preemption" knob — but it pays off: removing a mob is worth more than 1 second of DPS lost to retargeting.

This is an extension of `PickTarget` — the existing scoring sorts by distance ascending. For multi-mob kite, the *secondary* sort key should be `hpPct` ascending. (Distance still primary; we're not switching to a far mob to finish it.)

### When kiting breaks down

Multi-mob kiting fails when:

1. **Three or more mobs all in `mobRange + 2` of the bot.** No backward cell satisfies all the disqualification checks. → Disengage to a safe corner, regroup, re-engage one mob at a time.
2. **The chosen target is at low HP but on the FAR side of the pack from the bot.** Stepping back from the pack moves us *away* from the chosen target faster than the target can chase. → Switch target to one of the nearer mobs.
3. **One mob has `mobRange >= myRange`.** We can't outrange it. → Treat that mob as a hazard (it's not kiteable), use it as a flee anchor in the AvoidHazards path. The kite continues against the kiteable mobs only after the threat-mob is gone.

The parent FSM already classifies "swarm we can't win" via the `EngagementGroup` check. If a multi-mob situation is unwinnable per the simulator, the kite sub-FSM should return `false` immediately and let the parent run the flee path. The kite only kicks in for *winnable* groups.

---

## 8. When NOT to kite

Negative space — every kite tick should start by asking "is kiting the right behavior here?" rather than blindly trying.

| Situation | Behavior |
|---|---|
| **Melee bot** (Knight, Thief, Swordsman) | Kite is N/A. The melee fallback (chase + swing) is correct. `ShouldKiteThisTarget` returns false on `myRange <= 2`. |
| **Solo on a safe map** (no aggressive mobs visible, full HP, plenty of potions) | Kite to save potions. The DPS-per-potion-consumed ratio improves. |
| **Squad tank** (`PartyRole.Tank` and not the leader) | Tank holds threats on its body. Kiting peels aggro off the tank's range. **Disable kite for the tank entirely** even if it carries a bow. |
| **Squad DPS follower** (`SquadId set, not leader`) | Follow formation slot. Kite *within* the slot offset window — a 2-tile micro-kite around the assigned cell is fine; full retreat is not. |
| **Squad leader (ranged class)** | Kite freely. The formation slots are relative to the leader, so followers follow the kite. Be aware the followers may not have line of sight after a step — but that's their problem. |
| **Bot is the healer** (`PartyRole.Healer`) | Kite is conditional. Healer is usually at the rear; if a mob breaks through, kite to stay in heal range of the tank. Don't kite away from the party. |
| **Stationary target** (training dummy, plant root mob with no movement) | Kite is pointless — there's no closer to evade. Stand still, fire. |
| **Bot is sitting / resting** | Kite is off; the rest sub-FSM owns this state. |
| **Bot is fleeing** | Kite is off; the flee sub-FSM owns this. |
| **Bot is shopping / job-changing / parked / following** | Kite is off; respective sub-FSMs own these. |
| **Cast just started** | Don't step until cast finishes (§4). |
| **HP < FleeHpPercent** | Don't kite; flee. Parent FSM handles. |
| **Stuck timer firing** | Don't kite; parent FSM owns recovery. |
| **Mob has `mobRange >= myRange`** | Kiting doesn't help. Fight as melee or disengage. |
| **Map is too dense / no walkable cells in back arc** | Kite is impossible; face-tank. |

The `ShouldKiteThisTarget(snap, target)` predicate compresses most of these into one boolean. The parent FSM still owns the high-level mode (Fleeing/Shopping/etc.); the predicate handles per-target eligibility.

```
ShouldKiteThisTarget(snap, target):
    if !IsRangedClass(snap.SelfJobId): return false
    if _config.SquadId != "" && !_config.IsSquadLeader && IsTankRole(_config): return false
    if MyAttackRange(snap) <= 2: return false                  // unequipped Archer
    if MobRangeOf(target) >= MyAttackRange(snap): return false // can't outrange
    if MonsterMoveSpeedOf(target) <= 0.1: return false         // stationary; just shoot
    if _kiteMode == Suspended && now < _kiteSuspendedUntil: return false
    if Mode is Fleeing | Shopping | JobChange | Parked | Resting | Following: return false
    return true
```

---

## 9. Concrete code-shape suggestions

Three method signatures the FSM should expose. None implemented here — these are interface stubs.

### 9.1. The single integration point

```csharp
// Returns true if the kite sub-FSM owned this tick (whether it stepped, fired, or held).
// Returns false if the caller should fall back to the existing melee-style attack path.
//
// Side-effects: may send WalkTo, AttackAsync, or UseSkill packets. Updates _kiteMode,
// _kiteLastStepAt, _kiteFireAfterStep, _kiteSweetSpot.
private async Task<bool> TryKiteEngagementAsync(Snapshot snap, MonsterInfo target, CancellationToken ct);
```

Called from the hunting branch in `TickAsync`, exactly as outlined in §1. Position-of-call:

```
existing: TickSkillsAsync → AttackAsync
proposed: TickSkillsAsync → TryKiteEngagementAsync → (if false) AttackAsync
```

Skills still run first because a fired skill IS the kiter's attack — the kiter's role is to ensure the bot is in the right cell to fire.

### 9.2. Range math

```csharp
// Pure function — depends only on the input ints. No state read, no side effects.
// Returns the desired Chebyshev distance from target.
private static int KiteDistanceTarget(int myAttackRange, int mobAttackRange, float mobMoveSpeed);
```

Logic in §2. Returns the sweet-spot tile distance. Throws no exceptions; clamps to `[mobAttackRange + 2, myAttackRange]`. Returns `-1` (or `0`) as a sentinel for "can't outrange this mob" — caller treats that as "don't kite."

### 9.3. Step selection

```csharp
// Picks the best backward cell. Returns false if no acceptable cell exists (caller should
// switch to Suspended). Out params: the chosen cell coordinates. The function consults
// the walkmap, hazard list, portal footprints, and other-monster reach.
private bool TryPickKiteStep(Snapshot snap, MonsterInfo target, out int gx, out int gy);
```

Internally walks the fallback ladder from §3 (primary backward → diagonals → perpendicular → forward-and-around). Returns the first acceptable cell. The caller (`TryKiteEngagementAsync`) sends the `WalkTo` and stamps `_kiteLastStepAt`.

### Optional helpers

```csharp
// Trend detection for §6. Stateful — reads/writes _kiteLastDistanceSamples.
private bool DetectDanceBug(Snapshot snap, MonsterInfo target);

// Pure predicate from §8. No side effects.
private bool ShouldKiteThisTarget(Snapshot snap, MonsterInfo target);

// "An attack/skill cast is OK to send right now." Combines _nextAttack, _skillCooldown,
// and _kiteNoStepUntil checks into one boolean.
private bool AttackReady(Snapshot snap);
```

### Config knobs to add to `BotBehaviorConfig`

Minimal new tunables; defaults chosen to match the design above. Match the existing style (public fields, doc comments, `CopyFrom` entries):

```csharp
/// <summary>Enable ranged kiting on Archer/Mage/Wizard/Hunter classes. When false, ranged bots
/// fall through to the same melee-style attack-and-stand-still path as everyone else.</summary>
public bool EnableKite = true;

/// <summary>Tile buffer inside attack range that defines the kite sweet spot. 1 = step back when
/// mob enters myRange (gives one tile of slip tolerance). 0 = kite at max range (highest DPS,
/// most fragile). 2+ = give up DPS for survivability.</summary>
public int KiteBufferTiles = 1;

/// <summary>Mob MoveSpeed (tiles/sec) above which the kite uses a +1 extra buffer tile. Mobs
/// slower than this are treated as comfortably kiteable; faster mobs get more headroom.</summary>
public float KiteFastMobThreshold = 2.0f;

/// <summary>How long the kite stays in Suspended mode after a dance-bug detection before
/// re-evaluating. Prevents oscillation when mob speed barely exceeds kite cadence.</summary>
public double KiteSuspendSeconds = 5.0;
```

No new fields are needed on the `Snapshot` for the first cut; the kite reads everything from existing snapshot data + the static `MonsterDbEntry` lookup. Long-term, exposing `SelfAttackRange` would let the table of "ranged class ranges" go away.

---

## 10. References

Original synthesis with cross-checks against a few well-known prior arts:

- **OpenKore** (decade-old Ragnarok Online bot). Has a `tankMode` and `attackBeyondMaxDistance` config but no proper kiting state machine — it relies on `route()` to walk back and `attack()` to re-engage, alternating per server response. The dance-bug failure mode is endemic in OpenKore against fast mobs; the community workaround is "don't kite Hunters / Whisper-class enemies." The lesson taken into this design: explicit detection (§6) is necessary, not optional.

- **Eathena / rAthena server-side `homunculus` AI**. Homunculi have a basic "stay-at-distance" mode (`HOM_ST_ATTACK` with `homtype == HT_VANILMIRTH`) that uses a similar primary-direction-back algorithm. Their bug: they don't track gap-closing trend, so a faster mob locks the homunculus into a step-back loop with zero attacks landed. Same lesson as OpenKore.

- **WoW kiting macros / community guides (Hunter class)** are useful for the *attack-then-step* rhythm — the "Disengage / Aspect of the Cheetah" community consensus from circa 2010 is the source of "always fire before stepping, never step before firing." The DPS uptime argument in §5 is straight from that body of player wisdom.

- **Brood War (StarCraft) mutalisk micro / Carbot-style kiting** — the conceptual home of "stop attack, move one tile, restart attack" as a discrete cycle. The animation-lock interaction (§5) maps directly: every `Attack` command resets a swing timer, every `Move` interrupts it, so the optimal micro is to issue Attack right when the swing animation completes. The same logic applies to this codebase's `_nextAttack` throttle.

- **MoBA bot literature (Dota Auto Chess / autochess engines circa 2019)** — the formulation of optimum range as `kiteDist = max(myRange - buffer, mobRange + safety)` clamp comes from there. The "centroid vs nearest threat" decision in §7 also has prior art in MoBA "rotate around the closest danger" heuristics.

- **None of the above** tackle the squad / formation interaction directly. Section 8's table (tank doesn't kite, healer kites only inside heal-range envelope, follower kites within slot) is project-specific synthesis informed by the existing `PartyRole` / `SquadSlot` machinery in `BotBehaviorConfig`.

Where this design diverges from received wisdom:

- **No reliance on a "step cancel" client command.** RO Rebuild's WalkTo + server pathfinder makes step-cancellation implicit — the moment we send a new WalkTo or AttackAsync, the previous intent is overridden. OpenKore had to send explicit cancel packets and that's where most of its kite jitter came from. We don't have that problem.

- **The trend-based dance-bug detector is original.** Most prior kiting bots either (a) trust the user's config to identify un-kiteable mobs, or (b) detect "I took damage while at max range" as the proxy. Trend-based detection catches the failure earlier (before the first hit lands) and doesn't require the bot to take damage to learn.

- **Status-aware kiting** (`target.slowed` etc.) leverages the project's relatively recent skill DSL extension (`target.status.<name>`, 2026-05-28). This is unusual — most kiters treat the target as a constant-velocity mover. Knowing the target is Frozen converts a face-tank into a kite for free.

---

## Implementation order (suggested)

If you build this incrementally:

1. **`ShouldKiteThisTarget` + `KiteDistanceTarget`** — pure predicates / math. Add the config flags. Run with the predicate as a no-op (always false). Unit-test the math.
2. **`TryPickKiteStep`** — the cell-picker. Test by logging the chosen cell every tick without sending the packet.
3. **`TryKiteEngagementAsync`** — wire the step-and-attack rhythm. Disable dance-bug detection initially; just step back when in range, re-fire after step. Tune on Mandragora.
4. **`DetectDanceBug`** — add the trend buffer and the Suspended mode. Validate against a faster mob like Wolf or Picky.
5. **Multi-mob handling.** Switch from "vector away from target" to nearest-threat in the cell picker. Add the second-nearest disqualification check.
6. **Squad-aware gating.** Disable kite for tanks, restrict for followers. Test in a 6-bot party.
7. **Cast-time interaction.** Add `_kiteNoStepUntil` and the `EstimatedCastTime` table. Test on Mage with FireBolt.
8. **Tuning pass.** Adjust `KiteBufferTiles`, `KiteFastMobThreshold`, `KiteSuspendSeconds` based on telemetry. Log kite transitions verbosely until stable, then quiet the logs.

Each step is a couple-hundred-line PR that doesn't break the existing FSM. The kite sub-FSM is opt-in via `EnableKite` and class check — leaving the field false reverts to today's behavior exactly.
