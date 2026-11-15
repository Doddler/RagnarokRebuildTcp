using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.Session;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Behavior;

public enum BotMode { Idle, Hunting, Dead, Traveling, Looting, Fleeing, Shopping, JobChange, Parked, Resting, Following }

/// <summary>
/// Tick-based brain. It does NOT flee: before engaging it runs a <see cref="BattleSimulator"/> forecast
/// from the player's stats and the monster database, and only attacks monsters it projects it can kill.
/// Once engaged it commits to that target until it dies. If the bot itself dies (a fight the forecast got
/// wrong, or a swarm), it marks that monster class as off-limits, respawns, and resumes. With nothing
/// winnable in view it roams to find some.
/// </summary>
public sealed partial class BotBehavior
{
    private readonly BotSession _bot;
    private readonly BotBehaviorConfig _config;
    private readonly GameDatabase? _data;
    private readonly Random _rng = new();
    // Per-class death blacklist with time-decay. Dying to a mob class adds it here; PickTarget skips
    // anything with a not-yet-expired entry. Decays after AvoidClassDuration so a fluke death doesn't
    // permanently ban a class — particularly important for low-level bots that may die once and then be
    // unable to hunt the most common mob on their map for the rest of the session.
    private readonly Dictionary<int, DateTime> _avoidClasses = new();
    private static readonly TimeSpan AvoidClassDuration = TimeSpan.FromMinutes(5);

    private int _targetId;
    private int _targetClass = -1;
    private DateTime _nextWander = DateTime.MinValue;
    private DateTime _nextRespawn = DateTime.MinValue;
    private DateTime _nextAttack = DateTime.MinValue;
    private (int dx, int dy) _heading;
    private Position _lastWanderPos;
    private BotMode _lastAnnouncedMode = BotMode.Idle;
    private DateTime _nextHeal = DateTime.MinValue;
    private DateTime _nextLoot = DateTime.MinValue;
    private bool _resting;
    private DateTime _nextSit = DateTime.MinValue;
    private int _lootTargetId;
    private readonly HashSet<int> _skipDrops = new();
    private readonly List<(Position pos, int radius, string name)> _hazards = new();

    // Stuck failsafe: "progress" = moving OR the current target losing HP. No progress for StuckSeconds
    // means the target/route is unreachable (e.g. behind a wall), so we abandon it and relocate.
    private Position _lastProgressPos;
    private DateTime _lastProgressAt = DateTime.UtcNow;
    private int _lastTargetHp = -1;
    private string _lastMap = "";
    private readonly HashSet<int> _unreachable = new();

    public BotMode Mode { get; private set; } = BotMode.Idle;
    public int TargetId => _targetId;
    public string TargetName { get; private set; } = "";
    // Synchronization for the (_targetId, _targetClass, TargetName) group when the receive-thread Threat
    // handler writes them concurrently with the tick loop. Cheap monitor-only lock — no async work inside.
    internal readonly object _targetLock = new();

    // Per-map static danger score, relative to THIS bot's level. Built from MonsterDb.Spawns; higher
    // means "more / stronger aggressive mobs spawn here vs my level." Cross-map routing adds this along
    // the path so a level-15 bot detours around moc_fild04 (level-50 mobs) on the way to Morroc, even
    // though the direct route would be one map shorter.
    private Dictionary<string, float> _mapDanger = new(StringComparer.OrdinalIgnoreCase);
    private int _mapDangerLastLevel = -1;
    // Recompute trigger: when bot level changes by this much, the danger map is rebuilt.
    private const int DangerLevelStep = 3;
    public IReadOnlyDictionary<string, float> MapDanger => _mapDanger;
    /// <summary>Wall-clock seconds since the bot last made "progress" (moved or damaged its current target).
    /// Useful at-a-glance signal for wedged bots — anything climbing above StuckSeconds is in or about to
    /// be in the stuck-recover branch. Surfaced in BotSnapshot for `list_bots`.</summary>
    public double IdleSeconds => (DateTime.UtcNow - _lastProgressAt).TotalSeconds;
    public event Action<string>? OnLog;

    public BotBehavior(BotSession bot, BotBehaviorConfig config, GameDatabase? data = null)
    {
        _bot = bot;
        _config = config;
        _data = data;
        _bot.OnAllyHit += OnAllyHit;
        _bot.OnChatHeard += OnChatHeard;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await TickAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { OnLog?.Invoke($"behavior error: {ex.Message}"); }

            // Tick cadence — every 250ms (was 400). Halves the latency on combat reactions, follower
            // target mirroring, threat switches, hazard avoidance. 6 bots × 4 Hz = 24 ticks/sec total —
            // still cheap, since most ticks are short-circuit returns (e.g. squad followers running the
            // mirror-leader path don't run the heavy Pickup/PickTarget machinery).
            try { await Task.Delay(250, ct); }
            catch { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        if (!_config.Enabled) return;
        var snap = _bot.WithState(Snapshot.From);

        await AnnounceModeAsync(ct);

        // Drain operator-requested ground drops. Done up here so it runs even when the bot is parked /
        // shopping / job-changing — drops aren't gated on autonomous behavior. One drop per tick to
        // give the server room to process and acknowledge.
        await DrainPendingDropAsync(ct);

        // Forget unreachable targets when we change maps.
        if (!string.Equals(snap.Map, _lastMap, StringComparison.OrdinalIgnoreCase))
        {
            var arrivedFrom = _lastMap;
            _lastMap = snap.Map;
            _unreachable.Clear();
            _skipDrops.Clear();
            ResetStuck(snap.SelfPos);
            _cachedLeaderEntityId = 0; // entity ids are per-map; the cached value is stale on the new map
            ResetFollowerLeaderState();
            _lastIdleReason = ""; // log a fresh PickTarget reason on the new map
            // Apron-departure (Phase 1): we just landed on a new map at snap.SelfPos. The cheapest portal
            // from here is almost certainly the back-portal we came through, which would bounce us. Record
            // the landing cell + arrival map so StepTowardMapAsync can force a step-away walk and soft-
            // penalize the back-portal for the next few seconds. Also clear any soft penalty against the
            // portal we just successfully traversed — a one-off trap shouldn't carry bias.
            BeginApronDeparture(snap.SelfPos, arrivedFrom, snap.Map);
            OnLog?.Invoke($"Changed maps → {snap.Map}.");
        }

        // Refresh per-map danger when our level moves enough to change which mobs threaten us. Step-based
        // so we don't burn CPU rebuilding on every tick during exp-rich training.
        if (_mapDangerLastLevel < 0 || Math.Abs(snap.SelfLevel - _mapDangerLastLevel) >= DangerLevelStep)
            RecomputeMapDanger(snap.SelfLevel);

        // Stuck detection: moving, or the current target losing HP, counts as progress.
        var targetHpNow = _targetId != 0 ? (FindMonster(snap, _targetId)?.Hp ?? -1) : -1;
        if (snap.SelfPos.X != _lastProgressPos.X || snap.SelfPos.Y != _lastProgressPos.Y ||
            (targetHpNow >= 0 && targetHpNow < _lastTargetHp))
        {
            _lastProgressPos = snap.SelfPos;
            if (targetHpNow >= 0) _lastTargetHp = targetHpNow;
            _lastProgressAt = DateTime.UtcNow;
        }
        var stuck = (DateTime.UtcNow - _lastProgressAt).TotalSeconds > _config.StuckSeconds;

        // Stuck escalation: if NudgeAsync hasn't helped after a sustained no-progress window, try a Fly Wing
        // (and ultimately a Butterfly Wing). Runs before everything else so a wedged bot doesn't sit there
        // attempting hopeless heals/hazard-flees/etc. — but after the snap so we have current data.
        if (stuck && await TryEscapeStuckAsync(snap, ct)) return;

        // Dead -> blacklist the killer's class, stop, and respawn (retry on a cooldown). Never flee.
        if (snap.SelfDead || (snap.SelfMaxHp > 0 && snap.SelfHp <= 0))
        {
            if (Mode != BotMode.Dead)
            {
                Mode = BotMode.Dead;
                // Attribute to whoever ACTUALLY hit us last (assist mobs / aggressor pile-ons are usually NOT
                // our committed target). Fall back to the committed target only when the last-attacker entity
                // is gone from view by death-handling time.
                var killerClass = -1;
                var killerName = "";
                var attackerName = _bot.LastAttackerName;
                if (!string.IsNullOrEmpty(attackerName))
                {
                    var info = _bot.WithState(w =>
                    {
                        foreach (var e in w.Entities.Values)
                            if (e.IsMonster && string.Equals(e.Name, attackerName, StringComparison.Ordinal))
                                return (cls: e.ClassId, name: e.Name);
                        return (cls: -1, name: "");
                    });
                    if (info.cls >= 0) { killerClass = info.cls; killerName = info.name; }
                }
                if (killerClass < 0 && _targetClass >= 0) { killerClass = _targetClass; killerName = TargetName; }
                if (killerClass >= 0)
                {
                    var expires = DateTime.UtcNow + AvoidClassDuration;
                    var fresh = !_avoidClasses.ContainsKey(killerClass);
                    _avoidClasses[killerClass] = expires;
                    if (fresh)
                        OnLog?.Invoke($"Died to '{killerName}' (class {killerClass}) — avoiding it for the next {AvoidClassDuration.TotalMinutes:F0} min.");
                    else
                        OnLog?.Invoke($"Died to '{killerName}' (class {killerClass}) again — avoidance timer extended.");
                }
                else
                    OnLog?.Invoke($"Died (couldn't attribute the kill; last attacker '{attackerName}', committed target '{TargetName}').");
                _targetId = 0;
                _targetClass = -1;
                await _bot.StopAsync(ct);
            }
            if (DateTime.UtcNow >= _nextRespawn)
            {
                // Quicker respawn retry — 1.5s instead of 3s. The server rejects respawn requests during
                // the death-animation lockout anyway, so retrying sooner just shortens the wait once the
                // server is ready to accept it.
                _nextRespawn = DateTime.UtcNow.AddSeconds(1.5);
                await _bot.RespawnAsync(inPlace: false, ct);
            }
            return;
        }

        // Item-rule DSL goes first: when the user configured an ItemScript, it takes priority over the
        // hard-coded HealHpPercent logic. The script can fire a potion, Fly Wing, etc. and signal "done
        // for this tick". When ItemScript is empty, this is a no-op and MaybeHealAsync handles potions.
        if (await TickItemRulesAsync(snap, ct)) return;

        // Heal mid-anything (never when dead — handled above) before deciding what to do next.
        await MaybeHealAsync(snap, ct);

        // Steer clear of aggressive monsters the forecast says we'd lose to: if one's aggro range
        // already covers us, drop everything and flee away from it.
        ComputeHazards(snap);
        if (await AvoidHazardsAsync(snap, ct)) return;

        // Chat-driven leader commands (!engage / !flee / !hold / !regroup / !stop / !hunt / !follow).
        // Runs AFTER real-hazard avoidance — a !engage shouldn't pull a follower into an aggressive
        // monster they can't fight — but BEFORE ally rules / shopping / hunting, so the leader's
        // instructions take priority over default behavior.
        if (await TickPartyCommandAsync(snap, ct)) return;

        // User-queued skill casts from MCP queue_skill — highest priority among skill paths so an agent's
        // explicit plan beats any script-driven cast that tick. Fire-and-forget; doesn't preempt the tick.
        await TickQueuedSkillsAsync(snap, ct);

        // Ally support (#7): heal/buff visible party members per the bot's skill script before doing anything
        // else, so a hurt ally gets attention even if we're mid-shopping or hunting.
        if (await TickAllyRulesAsync(snap, ct)) return;

        // A manual "go here and wait" order overrides everything except fleeing danger.
        if (_parkActive) { await TickParkAsync(snap, stuck, ct); return; }

        // A pending job change (a milestone) takes priority over shopping and hunting.
        if (JobChangeActive) { await TickJobChangeAsync(snap, stuck, ct); return; }
        if (WantsJobChange(snap))
        {
            if (snap.SelfJobId == 0 && snap.SelfJobLevel >= 10 && snap.SelfSkillPoints > 0)
            {
                if (!await SpendOneSkillPointAsync(snap, ct))
                {
                    _jobCooldownUntil = DateTime.UtcNow.AddSeconds(60);
                    OnLog?.Invoke("Job change blocked: leftover skill points with nothing left to spend on.");
                }
                return;
            }
            if (snap.SelfJobId != 0 || snap.SelfJobLevel >= 10)
            {
                StartJobChange(snap);
                await TickJobChangeAsync(snap, stuck, ct);
                return;
            }
            // Novice below job level 10: not eligible yet — fall through and keep hunting.
        }

        // A shopping trip (restock potions / sell junk) takes priority over hunting and home-travel —
        // UNLESS this bot is a squad follower, in which case the leader drives the schedule and we don't
        // want a Merchant to peel off to town mid-fight just because its weight crossed the threshold.
        // Forced shop trips (_forceShop, set by MCP) still go through; the gate only blocks the AUTO
        // trigger so a follower won't auto-detach. A squad leader / solo bot keeps the legacy behavior.
        var isSquadFollower = !string.IsNullOrEmpty(_config.SquadId) && !_config.IsSquadLeader;
        if (ShopTripActive) { await TickShopTripAsync(snap, stuck, ct); return; }
        if (_forceShop || (!isSquadFollower && ShouldStartShopTrip(snap)))
        {
            StartShopTrip(snap, _forceShop);
            _forceShop = false;
            if (ShopTripActive) { await TickShopTripAsync(snap, stuck, ct); return; }
        }

        // Out of healing items and hurt? Sit to recover (when able + safe) instead of fighting without sustain.
        if (await TickRestAsync(snap, ct)) return;

        // Follower mode: in a party, NOT the leader, leader visible — mirror leader's target / stay close.
        // When the leader isn't in view this returns false and we fall through to normal travel/wander.
        if (await TickAsFollowerAsync(snap, ct)) return;

        // Travel to the hunting map if we're elsewhere (e.g. respawned in town).
        if (!string.IsNullOrEmpty(_config.HomeMap) &&
            !string.Equals(snap.Map, _config.HomeMap, StringComparison.OrdinalIgnoreCase))
        {
            Mode = BotMode.Traveling;
            _targetId = 0;
            _targetClass = -1;
            if (stuck) { ResetStuck(snap.SelfPos); await NudgeAsync(snap, ct); return; } // blocked en route
            await TravelAsync(snap, ct);
            return;
        }

        // Commit to the current target until it dies or leaves view.
        var current = _targetId != 0 ? FindMonster(snap, _targetId) : null;
        if (current != null && IsHuntable(current))
        {
            if (stuck)
            {
                OnLog?.Invoke($"Can't reach '{current.Name}' — abandoning it as unreachable.");
                _unreachable.Add(_targetId);
                _targetId = 0;
                _targetClass = -1;
                ResetStuck(snap.SelfPos);
                await NudgeAsync(snap, ct);
                return;
            }
            Mode = BotMode.Hunting;
            TargetName = current.Name;
            if (await TickSkillsAsync(snap, current, ct)) return; // a skill rule fired — skip the basic attack this tick
            // Kite sub-FSM: ranged classes with EnableKite set get positional management here. If it
            // owned the tick (stepped / fired / held), we're done; otherwise fall through to the
            // melee-style attack re-assert below. The predicate inside ShouldKiteThisTarget gates all
            // negative-space cases (melee, tank, stationary mob, equal-range mob, etc.).
            if (await TryKiteEngagementAsync(snap, current, ct)) return;
            // NoAutoAttack: pure casters (Mage / Wizard / skill-only builds) skip the auto re-assert.
            // The skill script is the entire combat loop in that mode. Without an auto-attack the bot
            // also can't waste its swing animation on a wrong-element target while the skill cooldown
            // ticks. Skills, heals, walking, looting still work the same.
            if (_config.NoAutoAttack) return;
            if (DateTime.UtcNow >= _nextAttack)
            {
                _nextAttack = DateTime.UtcNow.AddSeconds(3);
                await _bot.AttackAsync(_targetId, ct); // re-assert in case the server dropped the attack lock
            }
            return;
        }

        // Grab nearby drops before seeking a new target (we aren't committed to a live target here).
        if (_config.AutoLoot)
        {
            var drop = _bot.WithState(w => NearestLoot(w, snap.SelfPos));
            if (drop != 0)
            {
                Mode = BotMode.Looting;
                if (drop != _lootTargetId) { _lootTargetId = drop; ResetStuck(snap.SelfPos); _nextLoot = DateTime.MinValue; }
                else if (stuck) { _skipDrops.Add(drop); _lootTargetId = 0; ResetStuck(snap.SelfPos); await NudgeAsync(snap, ct); return; }
                if (DateTime.UtcNow >= _nextLoot)
                {
                    _nextLoot = DateTime.UtcNow.AddSeconds(_config.LootCooldownSeconds);
                    await _bot.PickUpAsync(drop, ct);
                }
                return;
            }
            _lootTargetId = 0;
        }

        // Pick the nearest monster the battle forecast says we can win against.
        var target = PickTarget(snap, out var forecast);
        if (target == null)
        {
            Mode = BotMode.Idle;
            _targetId = 0;
            _targetClass = -1;
            await WanderAsync(snap, ct);
            return;
        }

        _targetId = target.Id;
        _targetClass = target.ClassId;
        TargetName = target.Name;
        Mode = BotMode.Hunting;
        ResetStuck(snap.SelfPos, target.Hp); // start the progress clock for this target
        _nextAttack = DateTime.UtcNow.AddSeconds(3);
        OnLog?.Invoke($"Engaging {target.Name} (hp {target.MaxHp}): my hit ~{forecast.MyDamagePerHit}@{forecast.MyHitChance:P0}, " +
                      $"its hit ~{forecast.MonsterDamagePerHit}@{forecast.MonsterHitChance:P0}, " +
                      $"kill ~{forecast.SecondsToKillMonster:F1}s vs my death ~{forecast.SecondsToKillMe:F1}s.");
        await MaybeAnnounceTargetAsync(target, ct);
        // Skill-only bots still need the server to know who their target is (skills auto-target the
        // bot's current target id when no explicit target is passed in the rule). Just don't issue the
        // basic attack — the next tick's TickSkillsAsync will fire the right rule.
        if (!_config.NoAutoAttack)
            await _bot.AttackAsync(target.Id, ct);
    }

    private string _lastIdleReason = "";

    private MonsterInfo? PickTarget(Snapshot snap, out BattleForecast chosen)
    {
        chosen = default;
        var candidates = new List<(int score, MonsterInfo m, BattleForecast f)>();
        int notHuntable = 0, avoided = 0, blacklisted = 0, hazardous = 0, unwinnable = 0, swarmed = 0, portalRisk = 0;
        foreach (var m in snap.Monsters)
        {
            if (!IsHuntable(m)) { notHuntable++; continue; }
            // Time-decayed avoidance: drop the entry once it's older than AvoidClassDuration so a fluke
            // death doesn't permanently exclude a mob from the rotation.
            if (_avoidClasses.TryGetValue(m.ClassId, out var avoidUntil))
            {
                if (DateTime.UtcNow >= avoidUntil) _avoidClasses.Remove(m.ClassId);
                else { avoided++; continue; }
            }
            if (_unreachable.Contains(m.Id)) { blacklisted++; continue; }
            if (CellNearHazard(m.Pos.X, m.Pos.Y)) { hazardous++; continue; } // inside an aggressive monster's zone
            // Portal safety: don't engage a mob standing on a portal apron, or the chase will warp us
            // off the map mid-fight. Buffer is configurable via BotBehaviorConfig.PortalSafeDistance;
            // 0 disables. Uses the existing GameDatabase.IsNearPortal check.
            if (_config.PortalSafeDistance > 0 && _data != null
                && _data.IsNearPortal(snap.Map, m.Pos.X, m.Pos.Y, _config.PortalSafeDistance))
            { portalRisk++; continue; }
            var forecast = Forecast(snap, m);
            // Failsafe override: operator-set ForceHuntClassIds bypasses the forecast veto. The bot
            // still consults the forecast (numbers go in the Engaging log), but the unwinnable gate
            // and the group-swarm gate both yield to the override. Useful when the simulator under-
            // estimates a fight (e.g. element gear it can't see, or until the kite FSM is on for a
            // ranged build) and the operator wants the bot to commit anyway.
            var forced = _config.ForceHuntClassIds.Contains(m.ClassId);
            if (!forced && !forecast.CanWin) { unwinnable++; continue; }
            var group = EngagementGroup(snap, m);
            if (!forced && group.Count > 1 && !ForecastGroupOf(snap, group).CanWin) { swarmed++; continue; }
            var score = Dist(snap.SelfPos, m.Pos);
            var mlvl = _data?.Monster(m.ClassId)?.Level ?? 0;
            if (mlvl > 0 && mlvl <= snap.SelfLevel - 15) score += 100; // grey mob: poor exp, deprioritize
            candidates.Add((score, m, forecast));
        }

        // Take the nearest target we can actually path to (A* over the walkmap). Checking reachability up
        // front avoids committing to monsters behind walls and waiting on the stuck timeout to bail out.
        candidates.Sort(static (a, b) => a.score.CompareTo(b.score));
        var unreachable = 0;
        foreach (var c in candidates)
        {
            if (!IsReachable(snap, c.m.Pos)) { unreachable++; continue; }
            chosen = c.f;
            _lastIdleReason = ""; // armed for the next idle window
            return c.m;
        }

        // No target. Log WHY (deduped — only fire when the reason string actually changes), so the user can
        // diagnose "FSM keeps the bot Idle" without enabling Verbose chat.
        var reason = $"PickTarget idle — {snap.Monsters.Count} mon in view (not-huntable={notHuntable}, avoided-class={avoided}, blacklisted={blacklisted}, in-hazard={hazardous}, near-portal={portalRisk}, unwinnable={unwinnable}, group-swarm={swarmed}, unreachable={unreachable}).";
        if (reason != _lastIdleReason)
        {
            _lastIdleReason = reason;
            OnLog?.Invoke(reason);
        }
        return null;
    }

    // True if the bot can walk to a cell next to the target (bounded A* over the walkmap). With no walkmap
    // loaded we optimistically defer to the server's own pathing.
    private bool IsReachable(Snapshot snap, Position target)
    {
        var map = _data?.GetWalkMap(snap.Map);
        if (map == null) return true;
        if (!map.TryFindWalkableNear(target.X, target.Y, 4, out var gx, out var gy)) return false;
        // Reachability check uses the danger overlay too — a target that's only reachable through a kill-
        // you mob's aggro should be treated as effectively unreachable for safety.
        return map.FindPath(snap.SelfPos.X, snap.SelfPos.Y, gx, gy, 6000, MakePathCost(snap.Map)) != null;
    }

    /// <summary>Build a per-cell HAZARD-ONLY cost closure — for path searches where the goal itself is
    /// a portal cell (Shopping.cs reachable-portal checks). Including portal avoidance would inflate
    /// every portal's path cost since the destination IS a portal. Hazards still apply because we still
    /// want to walk around aggressive mobs on the way to the portal.</summary>
    private Func<int, int, float>? MakeHazardOnlyCost()
    {
        if (_hazards.Count == 0) return null;
        var hz = new List<(int x, int y, int radius)>(_hazards.Count);
        foreach (var h in _hazards) hz.Add((h.pos.X, h.pos.Y, h.radius));
        return (x, y) =>
        {
            var cost = 0f;
            for (var i = 0; i < hz.Count; i++)
            {
                var h = hz[i];
                var d = Math.Max(Math.Abs(h.x - x), Math.Abs(h.y - y));
                if (d <= h.radius) cost += 8f;
                else if (d <= h.radius + 3) cost += 2f;
            }
            return cost;
        };
    }

    /// <summary>Build a per-cell extra-cost closure for WalkMap.FindPath. Combines the visible hazard
    /// list (mob aggro cones) and the static portal layout (so the bot doesn't path through warp cells
    /// while traveling to a non-portal goal). Returns null when there's nothing to bias — that lets the
    /// callee skip the danger pass entirely.</summary>
    private Func<int, int, float>? MakePathCost(string map)
    {
        if (_data == null) return null;
        // Snapshot inputs into locals so the closure doesn't keep re-evaluating volatile fields.
        var hzList = new List<(int x, int y, int radius)>(_hazards.Count);
        foreach (var h in _hazards) hzList.Add((h.pos.X, h.pos.Y, h.radius));
        // Immobile-ranged-mob attack zones: Mandragora et al. don't chase, but if the bot walks into
        // their attack radius they shoot. Treat their fixed attack circles as soft hazards so paths
        // naturally bend around them when possible. (For approaching to engage, the kite FSM picks a
        // cell on the OUTER edge; this overlay only nudges; it doesn't make engagement impossible.)
        AppendImmobileMobZones(hzList);
        var portalBuffer = Math.Max(0, _config.PortalSafeDistance);
        // Snapshot the REAL warp footprints (rectangles, not points) so cost is correct for fat warps.
        // The legacy distance-to-center version silently approved cells on the long edge of warps like
        // pay_fild01's 5×13 pay_fild07 portal.
        List<(int minX, int minY, int maxX, int maxY)>? footprints = null;
        if (portalBuffer > 0)
        {
            footprints = new List<(int, int, int, int)>();
            foreach (var f in _data.World.PortalFootprintsOn(map)) footprints.Add(f);
        }
        if (hzList.Count == 0 && (footprints == null || footprints.Count == 0)) return null;
        return (x, y) =>
        {
            var cost = 0f;
            for (var i = 0; i < hzList.Count; i++)
            {
                var h = hzList[i];
                var d = Math.Max(Math.Abs(h.x - x), Math.Abs(h.y - y));
                if (d <= h.radius) cost += 8f;          // inside aggro — strong penalty (path detours unless no choice)
                else if (d <= h.radius + 3) cost += 2f; // 3-cell warning buffer
            }
            if (footprints != null)
            {
                for (var i = 0; i < footprints.Count; i++)
                {
                    var f = footprints[i];
                    // Chebyshev distance from (x,y) to the AABB of the footprint. 0 = inside.
                    var dx = Math.Max(Math.Max(f.minX - x, 0), x - f.maxX);
                    var dy = Math.Max(Math.Max(f.minY - y, 0), y - f.maxY);
                    var d = Math.Max(dx, dy);
                    if (d == 0) { cost += 50f; break; }       // inside footprint = guaranteed warp
                    if (d == 1) cost += 6f;                    // adjacent — one step from triggering
                    else if (d <= portalBuffer) cost += 1.5f;  // soft buffer fades smoothly
                }
            }
            return cost;
        };
    }

    /// <summary>Public MCP entry — runs the bot's full Forecast pipeline against a database mob class.
    /// Same pipeline TickAsync uses for PickTarget, so MCP `simulate_fight` reflects every per-bot
    /// modifier: NoAutoAttack zeros melee, SkillScript drives ExtraSkillDps, ItemScript potions add an
    /// HP buffer, immobile-mob free-win rule applies. Returns a default BattleForecast (CanWin=false)
    /// when the class id is unknown.</summary>
    public BattleForecast ForecastAgainst(int monsterClassId)
    {
        var db = _data?.Monster(monsterClassId);
        if (db == null) return default;
        var snap = _bot.WithState(Snapshot.From);
        // MonsterInfo is the internal target type; Forecast() reads only ClassId / MaxHp from it. The
        // position is irrelevant — the sim doesn't pathfind, it just decides if the fight is winnable
        // given stats + ranges + script.
        var mi = new MonsterInfo(0, monsterClassId, db.Name, default, db.HP, db.HP);
        return Forecast(snap, mi);
    }

    /// <summary>What the simulator is actually using for a forecast — exposed so the MCP / debug UI can
    /// show "why did canWin come out as X?". Captures every input the script-aware pipeline depends on
    /// so a wrong answer can be diagnosed without re-reading server packets or guessing config.</summary>
    public sealed record SimDiagnostics(
        bool NoAutoAttack,
        int AttackRange,
        int MagicAtkMin,
        int MagicAtkMax,
        double PotionHpBuffer,
        IReadOnlyList<(string Skill, int Level, string Target)> OffensiveScriptedSkills,
        IReadOnlyList<(int ItemId, int InventoryCount, int ApproxHealPerUse)> HpRescueItems);

    /// <summary>Build the diagnostics blob. Cheap — runs the same parsing the Forecast does, no packet
    /// I/O. Safe to call from MCP responses.</summary>
    public SimDiagnostics BuildSimDiagnostics()
    {
        var snap = _bot.WithState(Snapshot.From);
        var rules = GetSkillRules();
        var offensive = new List<(string, int, string)>();
        var maxKnown = new Dictionary<CharacterSkill, int>();
        foreach (var (s, lv) in snap.KnownSkills)
            if (lv > maxKnown.GetValueOrDefault(s)) maxKnown[s] = lv;
        foreach (var r in rules)
        {
            if (r.Target == SkillCastTarget.SelfCast || r.Target == SkillCastTarget.Ally) continue;
            if (!maxKnown.TryGetValue(r.Skill, out var known) || known <= 0) continue;
            var effective = r.Level <= 0 ? known : Math.Min(r.Level, known);
            offensive.Add((r.Skill.ToString(), effective, r.Target.ToString()));
        }
        ReloadItemScriptIfChanged();
        var rescues = new List<(int, int, int)>();
        var invCounts = _bot.WithState(w =>
        {
            var equipped = new HashSet<int>();
            foreach (var bid in w.Self.EquippedBagIds) if (bid != 0) equipped.Add(bid);
            var counts = new Dictionary<int, int>();
            foreach (var it in w.Self.Inventory)
                if (!equipped.Contains(it.BagId))
                    counts[it.ItemId] = counts.GetValueOrDefault(it.ItemId) + it.Count;
            return counts;
        });
        foreach (var r in _itemRules)
        {
            var hpTrig = false;
            for (var i = 0; i < r.Conditions.Count; i++)
            {
                var c = r.Conditions[i];
                if (string.Equals(c.Lhs, "hppct", StringComparison.OrdinalIgnoreCase) && (c.Op == "<" || c.Op == "<="))
                { hpTrig = true; break; }
            }
            if (!hpTrig) continue;
            if (!PotionHealApprox.TryGetValue(r.ItemId, out var heal)) continue;
            var count = invCounts.GetValueOrDefault(r.ItemId);
            rescues.Add((r.ItemId, count, heal));
        }
        return new SimDiagnostics(
            _config.NoAutoAttack,
            InferAttackRange(snap),
            snap.SelfMagicAtkMin,
            snap.SelfMagicAtkMax,
            EstimatePotionHpBuffer(snap),
            offensive,
            rescues);
    }

    private BattleForecast Forecast(Snapshot snap, MonsterInfo m)
    {
        var me = MeCombatant(snap);
        var (demonBane, beastBane) = MasteryLevels(snap);
        var mDb = _data?.Monster(m.ClassId);
        // Effective HP buffer from healing potions actually in inventory AND covered by an item-script
        // rule that'll fire during the fight. Extends secsToKillMe — a Mage carrying 30 White Potions and
        // a `use 504 if hppct < 60` rule is way harder to kill than the raw HP number suggests.
        var hpBuffer = (int)EstimatePotionHpBuffer(snap);
        if (hpBuffer > 0) me = me with { Hp = me.Hp + hpBuffer };
        var mods = BuildMonsterMods(m) with
        {
            AttackerElement = SimElement.Neutral,
            AttackerWeapon = SimWeaponClass.OneHandSword, // TODO read equipped weapon class once exposed
            AttackerLuk = snap.SelfLuk,
            AttackerAddCrit = snap.SelfAddCrit,
            AttackerDemonBaneLevel = demonBane,
            AttackerBeastBaneLevel = beastBane,
            // Skill DPS comes from the user's actual SkillScript rules (level + skill that'd fire here),
            // not "every known skill counts". Falls back to the legacy heuristic when no script is set.
            ExtraSkillDps = EstimateScriptedDps(snap, me),
            AttackerAttackRange = InferAttackRange(snap),
            DefenderAttackRange = mDb?.Range ?? 1,
            DefenderIsImmobile = IsMonsterImmobile(mDb),
        };
        return BattleSimulator.Forecast(me, MonsterCombatant(m), mods, _config.WinMargin, _config.MaxRoundsToKill);
    }

    /// <summary>Effective attack range for the bot. Prefers the server-synced <c>CharacterStat.Range</c>
    /// (which reflects the equipped weapon — bow = ~9, melee weapon = 1). When not synced (older server
    /// build or before first stat-update), falls back to a skill heuristic: a bot with bow skills
    /// (DoubleStrafe / ArrowShower) or any magic Bolt skill is treated as ranged at 9 tiles, otherwise
    /// melee at 1.</summary>
    private static int InferAttackRange(Snapshot snap)
    {
        if (snap.SelfAttackRange > 0) return snap.SelfAttackRange;
        foreach (var (skill, _) in snap.KnownSkills)
        {
            switch (skill)
            {
                case CharacterSkill.DoubleStrafe:
                case CharacterSkill.ArrowShower:
                case CharacterSkill.FireBolt:
                case CharacterSkill.ColdBolt:
                case CharacterSkill.LightningBolt:
                case CharacterSkill.SoulStrike:
                case CharacterSkill.FireBall:
                case CharacterSkill.FrostDiver:
                case CharacterSkill.NapalmBeat:
                case CharacterSkill.HeavensDrive:
                case CharacterSkill.HolyLight:
                case CharacterSkill.LexDivina:
                    return 9;
            }
        }
        return 1;
    }

    /// <summary>True when the monster is a fixed-position mob — Mandragora-style plants (negative
    /// MoveSpeed) and Geographer-style AiAggressiveImmobile sprites. The simulator zeros incoming
    /// damage when this is true and the bot can shoot from outside the mob's attack range.</summary>
    private static bool IsMonsterImmobile(MonsterDbEntry? db)
    {
        if (db == null) return false;
        if (db.MoveSpeed <= 0) return true;
        if (!string.IsNullOrEmpty(db.Ai) && db.Ai.IndexOf("Immobile", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
    }

    /// <summary>Add every visible immobile-ranged mob's attack circle to <paramref name="hzList"/> as a
    /// soft hazard. Cost-overlay readers (<see cref="MakePathCost"/>) treat the circle's interior as
    /// "inside aggro" (high cost) and the 3-tile fringe as a warning band — so passive travel routes
    /// around the area when there's room, but engagement A* can still find a path to the outer edge.
    /// Only fires when the mob's data confirms it's immobile (Mandragora MoveSpeed -0.001, Geographer
    /// AiAggressiveImmobile, etc.); mobile rangers are already covered by the aggro hazard tracker.</summary>
    private void AppendImmobileMobZones(List<(int x, int y, int radius)> hzList)
    {
        if (_data == null) return;
        var snap = _bot.WithState(Snapshot.From);
        foreach (var m in snap.Monsters)
        {
            var db = _data.Monster(m.ClassId);
            if (!IsMonsterImmobile(db)) continue;
            var range = db?.Range ?? 0;
            if (range < 2) continue; // melee plant has no projectile zone worth detouring around
            hzList.Add((m.Pos.X, m.Pos.Y, range));
        }
    }

    /// <summary>Look up DemonBane / BeastBane max-learned level from the bot's KnownSkills snapshot. The
    /// server adds (DemonBane*3) pre-DEF damage vs Demon/Undead and (BeastBane*5) vs Beast/Insect — see
    /// <see cref="BattleSimulator.RaceMasteryBonus"/>. Levels max out around 10 in this server.</summary>
    private static (int demonBane, int beastBane) MasteryLevels(Snapshot snap)
    {
        var demon = 0;
        var beast = 0;
        foreach (var (skill, level) in snap.KnownSkills)
        {
            if (skill == CharacterSkill.DemonBane && level > demon) demon = level;
            if (skill == CharacterSkill.BeastBane && level > beast) beast = level;
        }
        return (demon, beast);
    }

    private static double MeleeDps(Combatant me)
        => me.AtkMin > 0 && me.AttackInterval > 0 ? (me.AtkMin + me.AtkMax) / 2.0 / me.AttackInterval : 0;

    private Combatant MeCombatant(Snapshot snap)
    {
        // NoAutoAttack: bot is a pure caster — the simulator must NOT count basic-attack damage. Zeroing
        // AtkMin/AtkMax collapses melee DPS to 0; the fight is decided entirely by ExtraSkillDps.
        var atkMin = _config.NoAutoAttack ? 0 : snap.SelfAtkMin;
        var atkMax = _config.NoAutoAttack ? 0 : snap.SelfAtkMax;
        return new Combatant(
            snap.SelfLevel, atkMin, atkMax, snap.SelfDef,
            snap.SelfVit, snap.SelfDex, snap.SelfAgi, snap.SelfHit, snap.SelfFlee,
            snap.SelfAttackInterval, snap.SelfHp, snap.SelfMaxHp);
    }

    private Combatant MonsterCombatant(MonsterInfo m)
    {
        var db = _data?.Monster(m.ClassId);
        var hp = m.MaxHp > 0 ? m.MaxHp : (db?.HP ?? 0);
        return db != null
            ? new Combatant(db.Level, db.AtkMin, db.AtkMax, db.Def, db.Vit, db.Dex, db.Agi, 0, 0, 0, hp, hp)
            : new Combatant(1, 1, 1, 0, 0, 0, 0, 0, 0, 0, hp, hp);
    }

    /// <summary>Look up monster element/size/race from the exported DB and stash them on the modifier
    /// record. Defaults to "no effect" if the DB is missing or the strings don't match the enum.</summary>
    private CombatModifiers BuildMonsterMods(MonsterInfo m)
    {
        var db = _data?.Monster(m.ClassId);
        if (db == null) return CombatModifiers.Default;
        var (defEle, defLvl) = ElementChart.Parse(db.Element ?? "");
        return new CombatModifiers(
            DefenderElement: defEle,
            DefenderElementLevel: defLvl,
            DefenderSize: WeaponSizeChart.ParseSize(db.Size ?? ""),
            DefenderRace: WeaponSizeChart.ParseRace(db.Race ?? ""));
    }

    /// <summary>Build the player-side attacker modifier for a group forecast (where defender mods are per-
    /// monster and supplied separately). Folds in attacker element + weapon class + crit + skill DPS.</summary>
    private CombatModifiers BuildPlayerMods(Snapshot snap, Combatant me)
    {
        var (demon, beast) = MasteryLevels(snap);
        return new CombatModifiers(
            AttackerElement: SimElement.Neutral,
            AttackerWeapon: SimWeaponClass.OneHandSword, // TODO read equipped weapon class once exposed
            AttackerLuk: snap.SelfLuk,
            AttackerAddCrit: snap.SelfAddCrit,
            AttackerDemonBaneLevel: demon,
            AttackerBeastBaneLevel: beast,
            ExtraSkillDps: EstimateScriptedDps(snap, me));
    }

    /// <summary>Estimate the DPS bonus from the bot's offensive skill rotation. This is a heuristic — the
    /// real number depends on cast time, SP cost, cooldown, MATK vs ATK, and target elemental matchup.
    /// We trade precision for "does this bot's skill kit meaningfully contribute" so the forecast and the
    /// agent's `simulate_fight` MCP output reflect that an Acolyte with Heal vs Undead, or a Mage with
    /// FireBolt, kills faster than naive melee predicts.
    ///
    /// Two damage scales feed into the bonus: melee-based skills (Bash, DoubleStrafe, SonicBlow) multiply
    /// the bot's melee DPS; magic-based skills (FireBolt, NapalmBeat, HolyLight, SoulStrike) multiply a
    /// magic DPS derived from MagicAtkMin/Max — server reports the real number so a Mage doesn't get its
    /// FireBolt damage estimated from 0 melee.</summary>
    private static double EstimateSkillDps(Snapshot snap, double meleeDps)
    {
        var magicAtkAvg = (snap.SelfMagicAtkMin + snap.SelfMagicAtkMax) / 2.0;
        var attackInterval = snap.SelfAttackInterval > 0.05 ? snap.SelfAttackInterval : 1.5;
        // Magic skills don't share the basic-attack cooldown, but they have their own cast time. ~2s is a
        // reasonable per-cast cadence for low-level bolts; gets shorter once Wizard's casting reduction
        // perks kick in. Captures the rough rate without re-implementing cast-time tables.
        var magicCastInterval = 2.0;
        var magicDps = magicAtkAvg > 0 ? magicAtkAvg / magicCastInterval : 0;

        var meleeBonus = 0.0;
        var magicBonus = 0.0;
        foreach (var (skill, level) in snap.KnownSkills)
        {
            switch (skill)
            {
                // Melee — scale melee DPS.
                case CharacterSkill.Bash:            meleeBonus += 0.15 * level; break;
                case CharacterSkill.MagnumBreak:     meleeBonus += 0.10 * level; break;
                case CharacterSkill.DoubleStrafe:    meleeBonus += 0.30 * level; break;
                case CharacterSkill.ArrowShower:     meleeBonus += 0.10 * level; break;
                case CharacterSkill.SonicBlow:       meleeBonus += 0.20 * level; break;
                // Magic — scale magic DPS using the bot's real MATK.
                case CharacterSkill.FireBolt:        magicBonus += 0.40 * level; break;
                case CharacterSkill.ColdBolt:        magicBonus += 0.40 * level; break;
                case CharacterSkill.LightningBolt:   magicBonus += 0.40 * level; break;
                case CharacterSkill.SoulStrike:      magicBonus += 0.30 * level; break;
                case CharacterSkill.FireBall:        magicBonus += 0.20 * level; break;
                case CharacterSkill.NapalmBeat:      magicBonus += 0.20 * level; break;
                case CharacterSkill.HolyLight:       magicBonus += 0.30 * level; break;
            }
        }
        var fromMelee = Math.Clamp(meleeBonus, 0, 3) * meleeDps;
        var fromMagic = Math.Clamp(magicBonus, 0, 3) * magicDps;
        return fromMelee + fromMagic;
    }

    /// <summary>Estimate DPS bonus from the bot's ACTUAL SkillScript rules, not "every skill known".
    /// Walks the user's parsed offensive rules (target = Enemy or Ground) and credits each at its
    /// rule-specified level — a Mage with `use FireBolt 5 enemy if target.dist < 9` only gets FireBolt-5
    /// credit, not the maximum learned level. Falls back to the legacy <see cref="EstimateSkillDps"/>
    /// heuristic when the bot has no script (so existing un-scripted setups still get a forecast).</summary>
    private double EstimateScriptedDps(Snapshot snap, Combatant me)
    {
        var meleeDps = MeleeDps(me);
        var rules = GetSkillRules();
        if (rules.Count == 0) return EstimateSkillDps(snap, meleeDps);

        // Bot's max known level per skill — for rules that specify level 0 ("use whatever I have").
        var maxKnown = new Dictionary<CharacterSkill, int>();
        foreach (var (s, lv) in snap.KnownSkills)
            if (lv > maxKnown.GetValueOrDefault(s)) maxKnown[s] = lv;

        var magicAtkAvg = (snap.SelfMagicAtkMin + snap.SelfMagicAtkMax) / 2.0;
        var magicDps = magicAtkAvg > 0 ? magicAtkAvg / 2.0 : 0;

        var meleeBonus = 0.0;
        var magicBonus = 0.0;
        // De-duplicate rules that target the same skill — the script may have multiple conditional
        // variants of one skill (different levels, different conditions). Credit the HIGHEST level rule
        // since that's what'd fire under good conditions.
        var bestLevel = new Dictionary<CharacterSkill, int>();
        foreach (var r in rules)
        {
            if (r.Target == SkillCastTarget.SelfCast || r.Target == SkillCastTarget.Ally) continue;
            if (!maxKnown.TryGetValue(r.Skill, out var known) || known <= 0) continue;
            var effective = r.Level <= 0 ? known : Math.Min(r.Level, known);
            if (effective > bestLevel.GetValueOrDefault(r.Skill)) bestLevel[r.Skill] = effective;
        }
        foreach (var (skill, level) in bestLevel)
        {
            switch (skill)
            {
                case CharacterSkill.Bash:            meleeBonus += 0.15 * level; break;
                case CharacterSkill.MagnumBreak:     meleeBonus += 0.10 * level; break;
                case CharacterSkill.DoubleStrafe:    meleeBonus += 0.30 * level; break;
                case CharacterSkill.ArrowShower:     meleeBonus += 0.10 * level; break;
                case CharacterSkill.SonicBlow:       meleeBonus += 0.20 * level; break;
                case CharacterSkill.FireBolt:        magicBonus += 0.40 * level; break;
                case CharacterSkill.ColdBolt:        magicBonus += 0.40 * level; break;
                case CharacterSkill.LightningBolt:   magicBonus += 0.40 * level; break;
                case CharacterSkill.SoulStrike:      magicBonus += 0.30 * level; break;
                case CharacterSkill.FireBall:        magicBonus += 0.20 * level; break;
                case CharacterSkill.NapalmBeat:      magicBonus += 0.20 * level; break;
                case CharacterSkill.HolyLight:       magicBonus += 0.30 * level; break;
            }
        }
        var fromMelee = Math.Clamp(meleeBonus, 0, 3) * meleeDps;
        var fromMagic = Math.Clamp(magicBonus, 0, 3) * magicDps;
        return fromMelee + fromMagic;
    }

    /// <summary>Approximate per-use HP heal for common potions. Server doesn't expose the heal value via
    /// any client data, so we mirror the canonical RO ranges. Used by the forecaster to estimate how
    /// much "extra HP" the bot effectively has when it'll pop potions mid-fight per its ItemScript.</summary>
    private static readonly Dictionary<int, int> PotionHealApprox = new()
    {
        [501] = 50,    // Red Potion (~30-65 mean ~50)
        [502] = 110,   // Orange Potion
        [503] = 175,   // Yellow Potion
        [504] = 425,   // White Potion
        [507] = 30,    // Apple
        [512] = 17,    // Banana
        [515] = 40,    // Carrot
        [521] = 60,    // Pumpkin
        [545] = 80,    // Condensed Red Potion
        [546] = 200,   // Condensed Yellow Potion
        [547] = 475,   // Condensed White Potion
        [569] = 100,   // Novice Potion
    };

    /// <summary>Estimate an "effective HP buffer" the bot gets from healing potions it actually carries
    /// AND has an item-script rule for. A Mage with 50 White Potions and `use 504 if hppct < 60` is much
    /// harder to kill than the raw HP number — this is how the forecast acknowledges that. Returns 0
    /// when there's no item script, no matching rule, or no inventory. Capped at 1.5×MaxHp so a hoarder
    /// doesn't get treated as effectively immortal.</summary>
    private double EstimatePotionHpBuffer(Snapshot snap)
    {
        if (string.IsNullOrEmpty(_config.ItemScript)) return 0;
        ReloadItemScriptIfChanged();
        if (_itemRules.Count == 0) return 0;
        // Tally inventory by item id (skip equipped — they're not consumable here).
        var invByItem = _bot.WithState(w =>
        {
            var equipped = new HashSet<int>();
            foreach (var bid in w.Self.EquippedBagIds) if (bid != 0) equipped.Add(bid);
            var counts = new Dictionary<int, int>();
            foreach (var it in w.Self.Inventory)
            {
                if (equipped.Contains(it.BagId)) continue;
                counts[it.ItemId] = counts.GetValueOrDefault(it.ItemId) + it.Count;
            }
            return counts;
        });
        var buffer = 0.0;
        foreach (var r in _itemRules)
        {
            // Only count rules that fire on LOW HP (hppct < / <= X). Stuck-only / status-only rules
            // (e.g. Fly Wing on stuck>=8) aren't a survivability factor in a 1v1 forecast.
            var triggersOnLowHp = false;
            for (var i = 0; i < r.Conditions.Count; i++)
            {
                var c = r.Conditions[i];
                if (!string.Equals(c.Lhs, "hppct", StringComparison.OrdinalIgnoreCase)) continue;
                if (c.Op == "<" || c.Op == "<=") { triggersOnLowHp = true; break; }
            }
            if (!triggersOnLowHp) continue;
            if (!PotionHealApprox.TryGetValue(r.ItemId, out var healPerUse) || healPerUse <= 0) continue;
            if (!invByItem.TryGetValue(r.ItemId, out var count) || count <= 0) continue;
            // Cap usable potions per fight by inventory AND by a realistic 5-uses-per-fight ceiling —
            // even an infinite stack runs into cast-time / animation throttling.
            var usable = Math.Min(count, 5);
            buffer += usable * healPerUse;
        }
        return Math.Min(buffer, snap.SelfMaxHp * 1.5);
    }

    // The pack that piles on if we engage this target: same-type assist monsters that can see it, plus any
    // already-aggressive monster whose aggro range covers us. Lets us refuse mobs that are individually
    // winnable but collectively deadly (#8).
    private const int AssistRadius = 10;

    private bool IsAssist(int classId)
    {
        var ai = _data?.Monster(classId)?.Ai;
        return !string.IsNullOrEmpty(ai) && ai.Contains("Assist", StringComparison.Ordinal);
    }

    private List<MonsterInfo> EngagementGroup(Snapshot snap, MonsterInfo target)
    {
        var group = new List<MonsterInfo> { target };
        var targetAssists = IsAssist(target.ClassId);
        foreach (var m in snap.Monsters)
        {
            if (m.Id == target.Id) continue;
            if (m.MaxHp > 0 && m.Hp <= 0) continue;
            var joins = (targetAssists && m.ClassId == target.ClassId && Dist(m.Pos, target.Pos) <= AssistRadius)
                        || (IsAggressive(m.ClassId) && Dist(snap.SelfPos, m.Pos) <= HazardRadius(m.ClassId));
            if (joins) group.Add(m);
        }
        return group;
    }

    private GroupForecast ForecastGroupOf(Snapshot snap, List<MonsterInfo> group)
    {
        var mons = new List<Combatant>(group.Count);
        var mods = new List<CombatModifiers>(group.Count);
        foreach (var g in group)
        {
            mons.Add(MonsterCombatant(g));
            mods.Add(BuildMonsterMods(g));
        }
        var me = MeCombatant(snap);
        return BattleSimulator.ForecastGroup(me, mons, mods, BuildPlayerMods(snap, me),
            _config.WinMargin, _config.MaxRoundsToKill);
    }

    private bool IsHuntable(MonsterInfo m)
    {
        if (m.MaxHp > 0 && m.Hp <= 0) return false;
        if (_config.IgnoreClassIds.Contains(m.ClassId)) return false;
        if (_config.HuntClassIds.Count > 0 && !_config.HuntClassIds.Contains(m.ClassId)) return false;
        if (_config.MaxTargetHp > 0 && m.MaxHp > _config.MaxTargetHp) return false;
        return true;
    }

    // ---- movement ----

    private async Task TravelAsync(Snapshot snap, CancellationToken ct)
    {
        if (DateTime.UtcNow < _nextWander) return; // reuse the move throttle
        _nextWander = DateTime.UtcNow.AddSeconds(1.0); // pick a new wander target faster — wander cells are cheap
        if (_data == null || (snap.SelfPos.X == 0 && snap.SelfPos.Y == 0)) return;
        await StepTowardMapAsync(snap, _config.HomeMap, ct);
    }

    private async Task StepTowardAsync(Snapshot snap, int tx, int ty, CancellationToken ct)
    {
        var self = snap.SelfPos;
        var dx = tx - self.X;
        var dy = ty - self.Y;
        int gx, gy;
        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= 18) { gx = tx; gy = ty; }
        else { gx = self.X + Math.Clamp(dx, -18, 18); gy = self.Y + Math.Clamp(dy, -18, 18); }

        var map = _data?.GetWalkMap(snap.Map);
        if (map != null && !map.IsWalkable(gx, gy) && map.TryFindWalkableNear(gx, gy, 6, out var fx, out var fy))
        {
            gx = fx;
            gy = fy;
        }
        await _bot.WalkToAsync(Math.Max(1, gx), Math.Max(1, gy), ct);
    }

    // Roam in a persistent direction (turning when blocked or arrived) so we cover ground and find
    // monsters, avoiding portals so we don't accidentally leave the map.
    // Wander uses a "sticky far destination + small server-friendly hops" model. Pick a target ~35 cells
    // out, hold it for ~15 s, and each wander tick walk 12 cells further along the A* path toward it. That
    // gives wide coverage (the bot doesn't pace in a 16-cell box) while respecting the server's per-WalkTo
    // distance cap. The target re-uses across ticks so headings stay coherent — the bot commits to going
    // somewhere instead of jittering.
    private Position? _wanderTarget;
    private DateTime _wanderTargetUntil = DateTime.MinValue;
    private DateTime _nextWanderWedgeLog = DateTime.MinValue;
    private const int WanderStep = 35;       // far-destination offset in cells (~7 s of walking)
    private const int WanderHopCells = 12;   // per-WalkTo waypoint distance — safely under the server cap

    private async Task WanderAsync(Snapshot snap, CancellationToken ct)
    {
        if (DateTime.UtcNow < _nextWander) return;
        _nextWander = DateTime.UtcNow.AddSeconds(0.8);

        var self = snap.SelfPos;
        if (self.X == 0 && self.Y == 0) return;

        var map = _data?.GetWalkMap(snap.Map);
        if (map == null)
        {
            // No walkmap loaded — naive walk in the current heading, let the server pathfind. Just keep us
            // doing SOMETHING so we're not Idle on a never-loaded map.
            if (_heading.dx == 0 && _heading.dy == 0) _heading = RandomHeading();
            await _bot.WalkToAsync(Math.Max(1, self.X + _heading.dx * 12), Math.Max(1, self.Y + _heading.dy * 12), ct);
            return;
        }

        // Need a fresh target if we've never picked one, the previous one expired, or we've arrived.
        var needTarget = _wanderTarget == null
                         || DateTime.UtcNow > _wanderTargetUntil
                         || Dist(self, _wanderTarget.Value) <= 3;

        if (needTarget)
        {
            // Portal-away if we're hugging one; otherwise pick a fresh random heading so successive targets
            // don't all point the same direction.
            if (_data != null && TryNearestPortal(snap.Map, self, 8, out var npx, out var npy))
            {
                var hx = Math.Sign(self.X - npx);
                var hy = Math.Sign(self.Y - npy);
                _heading = (hx == 0 && hy == 0) ? RandomHeading() : (hx, hy);
            }
            else
            {
                _heading = RandomHeading();
            }

            for (var attempt = 0; attempt < 6; attempt++)
            {
                var tx = self.X + _heading.dx * WanderStep;
                var ty = self.Y + _heading.dy * WanderStep;
                if (!map.TryFindWalkableNear(tx, ty, 8, out var fx, out var fy) || (fx == self.X && fy == self.Y))
                {
                    _heading = RandomHeading();
                    continue;
                }
                var blocked = CellNearHazard(fx, fy)
                              || (_data != null && (_data.World.IsInPortalFootprint(snap.Map, fx, fy, margin: 3) || PathCrossesPortal(snap.Map, self, fx, fy)));
                if (blocked) { _heading = RandomHeading(); continue; }
                // A*-verify reachability with a generous budget — far targets need more nodes than the old
                // 2000 cap allowed, but a 6000 budget still completes in well under a millisecond.
                var pathCost = MakePathCost(snap.Map);
                var p = map.FindPath(self.X, self.Y, fx, fy, 6000, pathCost);
                if (p == null) { _heading = RandomHeading(); continue; }
                _wanderTarget = new Position(fx, fy);
                _wanderTargetUntil = DateTime.UtcNow.AddSeconds(15);
                OnLog?.Invoke($"Wander: heading to ({fx},{fy}) — {p.Count} cells via A*.");
                break;
            }
            if (_wanderTarget == null)
            {
                if (DateTime.UtcNow >= _nextWanderWedgeLog)
                {
                    _nextWanderWedgeLog = DateTime.UtcNow.AddSeconds(15);
                    OnLog?.Invoke($"Wander: no reachable destination in 6 attempts from ({self.X},{self.Y}) — wedged.");
                }
                return;
            }
        }

        // Step toward the sticky target via a short server-acceptable hop. Re-pathfind every tick so we
        // adapt to a moving fog of new hazards / unwalkable cells without going through stuck recovery.
        // Re-uses the danger overlay so a freshly-spawned aggressive mob in our path triggers a detour
        // mid-walk without needing to enter AvoidHazards / flee mode.
        var path = map.FindPath(self.X, self.Y, _wanderTarget!.Value.X, _wanderTarget!.Value.Y, 6000, MakePathCost(snap.Map));
        if (path == null || path.Count <= 1)
        {
            // Mid-route path break — drop the target so next tick picks a fresh one rather than thrashing.
            _wanderTarget = null;
            return;
        }
        var wp = path[Math.Min(WanderHopCells, path.Count - 1)];
        await _bot.WalkToAsync(Math.Max(1, wp.x), Math.Max(1, wp.y), ct);
    }

    // Nearest portal on the map within `within` Chebyshev tiles of `self`.
    private bool TryNearestPortal(string map, Position self, int within, out int px, out int py)
    {
        px = 0; py = 0;
        var best = int.MaxValue;
        foreach (var p in _data!.PortalsOn(map))
        {
            var d = Math.Max(Math.Abs(p.X - self.X), Math.Abs(p.Y - self.Y));
            if (d <= within && d < best) { best = d; px = p.X; py = p.Y; }
        }
        return best != int.MaxValue;
    }

    // True if the straight line from `from` to (tx,ty) steps onto any real warp footprint.
    private bool PathCrossesPortal(string map, Position from, int tx, int ty)
    {
        var steps = Math.Max(Math.Abs(tx - from.X), Math.Abs(ty - from.Y));
        for (var i = 1; i <= steps; i++)
        {
            var x = from.X + (tx - from.X) * i / steps;
            var y = from.Y + (ty - from.Y) * i / steps;
            if (_data!.World.IsInPortalFootprint(map, x, y)) return true;
        }
        return false;
    }

    private (int dx, int dy) RandomHeading()
    {
        while (true)
        {
            var dx = _rng.Next(-1, 2);
            var dy = _rng.Next(-1, 2);
            if (dx != 0 || dy != 0) return (dx, dy);
        }
    }

    private void ResetStuck(Position pos, int targetHp = -1)
    {
        _lastProgressPos = pos;
        _lastProgressAt = DateTime.UtcNow;
        _lastTargetHp = targetHp;
    }

    // Walk to a random nearby walkable cell to break out of a stuck position.
    private async Task NudgeAsync(Snapshot snap, CancellationToken ct)
    {
        var self = snap.SelfPos;
        var map = _data?.GetWalkMap(snap.Map);
        for (var i = 0; i < 10; i++)
        {
            var tx = self.X + _rng.Next(-6, 7);
            var ty = self.Y + _rng.Next(-6, 7);
            if (tx == self.X && ty == self.Y) continue;
            if (_data != null && _data.World.IsInPortalFootprint(snap.Map, tx, ty, margin: 1)) continue; // don't nudge into / next to a warp
            if (map == null || map.IsWalkable(tx, ty))
            {
                await _bot.WalkToAsync(Math.Max(1, tx), Math.Max(1, ty), ct);
                return;
            }
        }
    }

    // Verbose toggle: announce FSM state changes in map chat, once per transition.
    private async Task AnnounceModeAsync(CancellationToken ct)
    {
        if (!_config.Verbose || Mode == _lastAnnouncedMode || Mode == BotMode.Looting) return;
        _lastAnnouncedMode = Mode;
        var msg = Mode switch
        {
            BotMode.Hunting => string.IsNullOrEmpty(TargetName) ? "Hunting." : $"Hunting {TargetName}.",
            BotMode.Traveling => string.IsNullOrEmpty(_config.HomeMap) ? "Traveling." : $"Traveling to {_config.HomeMap}.",
            BotMode.Dead => "I died — respawning.",
            BotMode.Fleeing => "Fleeing — too dangerous here!",
            BotMode.Shopping => "Heading to town to shop.",
            BotMode.JobChange => "Off to change my job!",
            BotMode.Parked => "Holding position as ordered.",
            BotMode.Resting => "Resting to recover HP/SP.",
            BotMode.Following => "Following the party leader.",
            _ => "Idle — looking for targets.",
        };
        await _bot.SayAsync(msg, ct);
    }

    private async Task MaybeHealAsync(Snapshot snap, CancellationToken ct)
    {
        if (_data == null || _config.HealHpPercent <= 0 || snap.SelfMaxHp <= 0) return;
        if ((double)snap.SelfHp / snap.SelfMaxHp > _config.HealHpPercent) return;
        if (DateTime.UtcNow < _nextHeal) return;
        var itemId = _bot.WithState(FindHealItem);
        if (itemId <= 0) return;
        _nextHeal = DateTime.UtcNow.AddSeconds(_config.HealCooldownSeconds);
        await _bot.UseInventoryItemAsync(itemId, -1, ct);
    }

    // First configured healing item we actually hold that the DB confirms is Useable. Validating against
    // the DB is mandatory: the server disconnects the player if sent an unknown/non-usable item id.
    private int FindHealItem(WorldState w)
    {
        foreach (var id in _config.HealingItemIds)
        {
            if (_data == null || !_data.IsUsableItem(id)) continue;
            foreach (var it in w.Self.Inventory)
                if (it.ItemId == id && it.Count > 0) return id;
        }
        return 0;
    }

    // Sit to recover HP/SP when we're hurt and out of usable healing items. Stands back up once nearly full,
    // a monster comes within reach (sitting is cancelled on taking damage anyway), or a heal item appears.
    private async Task<bool> TickRestAsync(Snapshot snap, CancellationToken ct)
    {
        var hpPct = snap.SelfMaxHp > 0 ? (double)snap.SelfHp / snap.SelfMaxHp : 1.0;
        var spPct = snap.SelfMaxSp > 0 ? (double)snap.SelfSp / snap.SelfMaxSp : 1.0;
        var hasHeal = _bot.WithState(FindHealItem) > 0;

        if (_resting)
        {
            var recovered = hpPct >= 0.95 && spPct >= 0.95;
            if (!_config.RestWhenNoPotions || recovered || hasHeal || AnyAdjacentMonster(snap))
            {
                _resting = false;
                if (snap.SelfSitting) await _bot.SitAsync(false, ct);
                return false;
            }
            Mode = BotMode.Resting;
            if (!snap.SelfSitting && DateTime.UtcNow >= _nextSit) // server stood us up but we're still safe + low
            {
                _nextSit = DateTime.UtcNow.AddSeconds(1.5);
                await _bot.SitAsync(true, ct);
            }
            return true;
        }

        if (!_config.RestWhenNoPotions) return false;
        var wantRest = hpPct < _config.RestBelowPercent || spPct < _config.RestBelowPercent;
        if (!wantRest || hasHeal || !CanSit(snap) || AnyAdjacentMonster(snap)) return false;

        _resting = true;
        _targetId = 0;
        _targetClass = -1;
        Mode = BotMode.Resting;
        _nextSit = DateTime.UtcNow.AddSeconds(1.5);
        OnLog?.Invoke("Out of healing items and low — sitting to rest.");
        await _bot.SitAsync(true, ct);
        return true;
    }

    // A Novice (job 0) needs Basic Mastery level 2 to sit; everyone else always can.
    private bool CanSit(Snapshot snap)
    {
        if (snap.SelfJobId != 0) return true;
        return _bot.WithState(w =>
        {
            foreach (var k in w.Self.KnownSkills)
                if (k.Skill == CharacterSkill.BasicMastery && k.Level >= 2) return true;
            return false;
        });
    }

    private bool AnyAdjacentMonster(Snapshot snap, int range = 3)
    {
        foreach (var m in snap.Monsters)
        {
            if (m.MaxHp > 0 && m.Hp <= 0) continue;
            if (Dist(snap.SelfPos, m.Pos) <= range) return true;
        }
        return false;
    }

    private int NearestLoot(WorldState w, Position self)
    {
        var bestId = 0;
        var bestDist = int.MaxValue;
        foreach (var g in w.GroundItems.Values)
        {
            if (_config.LootBlacklist.Contains(g.ItemId)) continue;
            if (_skipDrops.Contains(g.DropId)) continue;
            if (CellNearHazard(g.X, g.Y)) continue; // don't walk into danger for loot
            var d = Math.Max(Math.Abs(g.X - self.X), Math.Abs(g.Y - self.Y));
            if (d > _config.LootRange || d >= bestDist) continue;
            bestDist = d;
            bestId = g.DropId;
        }
        return bestId;
    }

    // ---- hazard avoidance: stay clear of aggressive monsters the forecast says we'd lose to ----

    private bool IsAggressive(int classId)
    {
        var m = _data?.Monster(classId);
        if (m == null) return false;
        if (string.Equals(m.Special, "Boss", StringComparison.OrdinalIgnoreCase)) return true; // bosses/MVPs: always dangerous
        if (string.IsNullOrEmpty(m.Ai)) return false;
        return m.Ai.StartsWith("AiAggressive", StringComparison.Ordinal)
            || m.Ai.Equals("AiAngry", StringComparison.Ordinal)
            || m.Ai.Equals("AiStandardBoss", StringComparison.Ordinal);
    }

    /// <summary>Walk every monster in the DB, aggregate per-map danger contributions weighted by the
    /// level gap (mob_level - bot_level), aggression, boss/MVP flag, and spawn count. Mobs at or below
    /// the bot's level contribute 0 (no threat). Run on bot level change in steps of <see cref="DangerLevelStep"/>
    /// so it's not recomputed every tick.</summary>
    private void RecomputeMapDanger(int botLevel)
    {
        if (_data == null) return;
        var fresh = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _data.MonstersById.Values)
        {
            if (m.Spawns == null) continue;
            var levelGap = Math.Max(0, m.Level - botLevel);
            if (levelGap == 0) continue; // mob is at or below the bot's level — not a danger
            var aggMul = IsAggressive(m.Id) ? 3.0f : 1.0f;
            // Per-class severity. Pow(1.5) makes the gap super-linear so a 35-level gap (instant-death)
            // weights ~6× a 10-level gap.
            var perGap = (float)Math.Pow(levelGap, 1.5);
            // Self-experience penalty: bot died to this class → treat as MUCH more dangerous than its
            // raw level suggests (it actually killed us before).
            if (_avoidClasses.ContainsKey(m.Id)) perGap *= 4.0f;
            foreach (var sp in m.Spawns)
            {
                if (string.IsNullOrEmpty(sp.Map)) continue;
                var bossMul = sp.IsMvp ? 10.0f : (sp.IsBoss ? 4.0f : 1.0f);
                var contrib = perGap * aggMul * bossMul * Math.Max(1, sp.Count) / 100.0f;
                fresh[sp.Map] = (fresh.TryGetValue(sp.Map, out var prev) ? prev : 0) + contrib;
            }
        }
        _mapDanger = fresh;
        _mapDangerLastLevel = botLevel;
    }

    /// <summary>Returns map danger relative to this bot. 0 = safe (no mobs above our level), higher = more dangerous.</summary>
    private float GetMapDanger(string map) =>
        _mapDanger.TryGetValue(map, out var d) ? d : 0f;

    private int HazardRadius(int classId) => (_data?.Monster(classId)?.ScanDist ?? 10) + 2;

    // Recompute danger zones once per tick (one Forecast per aggressive monster) so the cell checks
    // are cheap lookups instead of re-running the battle simulator for every candidate cell.
    private void ComputeHazards(Snapshot snap)
    {
        _hazards.Clear();
        foreach (var m in snap.Monsters)
        {
            if (m.MaxHp > 0 && m.Hp <= 0) continue;
            if (_config.IgnoreClassIds.Contains(m.ClassId)) continue; // explicitly ignored (e.g. training dummy): neither hunt nor fear
            // Force-hunt override: operator explicitly told us "engage this class regardless of
            // forecast". The hazard system runs BEFORE PickTarget on every tick (including the very
            // first one after a map-change spawn) and a hazard-flagged mob short-circuits to
            // AvoidHazardsAsync — so a force-hunt entry that's only checked in PickTarget never gets
            // a chance to fire. Treating force-hunted mobs as "never a hazard" closes the loop.
            if (_config.ForceHuntClassIds.Contains(m.ClassId)) continue;
            if (!IsAggressive(m.ClassId)) continue;
            if (Forecast(snap, m).CanWin) continue; // aggressive but winnable = a target, not a hazard
            _hazards.Add((m.Pos, HazardRadius(m.ClassId), m.Name));
        }
    }

    private bool CellNearHazard(int x, int y)
    {
        foreach (var h in _hazards)
            if (Math.Max(Math.Abs(h.pos.X - x), Math.Abs(h.pos.Y - y)) <= h.radius) return true;
        return false;
    }

    private async Task<bool> AvoidHazardsAsync(Snapshot snap, CancellationToken ct)
    {
        var bestIdx = -1;
        var nearestD = int.MaxValue;
        for (var i = 0; i < _hazards.Count; i++)
        {
            var d = Dist(snap.SelfPos, _hazards[i].pos);
            if (d <= _hazards[i].radius && d < nearestD) { nearestD = d; bestIdx = i; }
        }
        if (bestIdx < 0)
        {
            // Hazard cleared (mob died, walked away, or we moved out of range). Surface the transition so a
            // tail of the bot's log shows the flee window ending instead of just going silent.
            if (Mode == BotMode.Fleeing)
                OnLog?.Invoke("Hazard no longer in range — resuming normal behavior.");
            return false;
        }
        var hz = _hazards[bestIdx];

        if (Mode != BotMode.Fleeing)
            OnLog?.Invoke($"Avoiding '{hz.name}' (aggressive, forecast says I'd lose) — fleeing.");
        Mode = BotMode.Fleeing;
        _targetId = 0;
        _targetClass = -1;

        var self = snap.SelfPos;
        var dx = Math.Sign(self.X - hz.pos.X);
        var dy = Math.Sign(self.Y - hz.pos.Y);
        if (dx == 0 && dy == 0) { dx = 1; dy = 1; }
        var map = _data?.GetWalkMap(snap.Map);
        for (var step = 14; step >= 6; step -= 4)
        {
            var tx = self.X + dx * step;
            var ty = self.Y + dy * step;
            if (map == null) { await _bot.WalkToAsync(Math.Max(1, tx), Math.Max(1, ty), ct); return true; }
            if (map.TryFindWalkableNear(tx, ty, 5, out var fx, out var fy) && !CellNearHazard(fx, fy))
            {
                await _bot.WalkToAsync(fx, fy, ct);
                return true;
            }
        }
        await _bot.WalkToAsync(Math.Max(1, self.X + dx * 6), Math.Max(1, self.Y + dy * 6), ct);
        return true;
    }

    private static MonsterInfo? FindMonster(Snapshot snap, int id)
    {
        foreach (var m in snap.Monsters)
            if (m.Id == id) return m;
        return null;
    }

    private static int Dist(Position a, Position b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private sealed record MonsterInfo(int Id, int ClassId, string Name, Position Pos, int Hp, int MaxHp);

    private sealed class Snapshot
    {
        public int SelfId;
        public Position SelfPos;
        public int SelfHp;
        public int SelfMaxHp;
        public int SelfAtkMin;
        public int SelfAtkMax;
        public int SelfDef;
        public int SelfLevel;
        public int SelfJobId;
        public int SelfJobLevel;
        public int SelfSkillPoints;
        public int SelfVit;
        public int SelfDex;
        public int SelfAgi;
        public int SelfHit;
        public int SelfFlee;
        public int SelfLuk;
        public int SelfAddCrit;     // server units (out of 1000); reflects LUK + gear + skills
        public int SelfMagicAtkMin;
        public int SelfMagicAtkMax;
        public double SelfAttackInterval;
        public int SelfAttackRange; // server-synced (CharacterStat.Range); 0 when not synced — caller falls back to skill heuristic
        public bool SelfDead;
        public int SelfSp;
        public int SelfMaxSp;
        public bool SelfSitting;
        public string Map = "";
        public readonly List<MonsterInfo> Monsters = new();
        public readonly List<(CharacterSkill Skill, int Level)> KnownSkills = new();
        public readonly HashSet<CharacterStatusEffect> SelfStatuses = new();
        // entityId → active status effects (still-current at snapshot time). Used by the skill DSL so a
        // rule like `use Curse 1 ally if target.status.poison == 0` can check what's on the ALLY/ENEMY.
        public readonly Dictionary<int, HashSet<CharacterStatusEffect>> EntityStatuses = new();

        public static Snapshot From(WorldState w)
        {
            var hasSelf = w.Entities.TryGetValue(w.Self.EntityId, out var self);
            var s = new Snapshot
            {
                SelfId = w.Self.EntityId,
                SelfPos = w.SelfPosition,
                SelfHp = hasSelf ? self!.Hp : w.Self.Hp,
                SelfMaxHp = hasSelf ? self!.MaxHp : w.Self.MaxHp,
                SelfAtkMin = w.Self.Attack,
                SelfAtkMax = Math.Max(w.Self.Attack, w.Self.AttackMax),
                SelfDef = w.Self.Def,
                SelfLevel = w.Self.Level,
                SelfJobId = w.Self.JobId,
                SelfJobLevel = w.Self.JobLevel,
                SelfSkillPoints = w.Self.SkillPoints,
                SelfVit = w.Self.Vit,
                SelfDex = w.Self.Dex,
                SelfAgi = w.Self.Agi,
                SelfHit = w.Self.Hit,
                SelfFlee = w.Self.Flee,
                SelfLuk = w.Self.Luk,
                SelfAddCrit = w.Self.AddCrit,
                SelfMagicAtkMin = w.Self.MagicAtkMin,
                SelfMagicAtkMax = w.Self.MagicAtkMax,
                SelfAttackInterval = w.Self.AttackSpeed,
                SelfAttackRange = w.Self.AttackRange,
                SelfDead = hasSelf && self!.State == CharacterState.Dead,
                SelfSp = w.Self.Sp,
                SelfMaxSp = w.Self.MaxSp,
                SelfSitting = hasSelf && self!.State == CharacterState.Sitting,
                Map = w.Self.Map,
            };
            foreach (var e in w.Entities.Values)
                if (e.IsMonster && e.Id != s.SelfId)
                    s.Monsters.Add(new MonsterInfo(e.Id, e.ClassId, e.Name, e.EstimatedCell(), e.Hp, e.MaxHp));
            foreach (var k in w.Self.KnownSkills)
                s.KnownSkills.Add((k.Skill, k.Level));
            // Self status effects, filtered to still-active ones so a stale Apply doesn't haunt the DSL.
            var nowUtc = DateTime.UtcNow;
            if (w.Entities.TryGetValue(w.Self.EntityId, out var selfView))
            {
                foreach (var kv in selfView.Statuses)
                    if (kv.Value > nowUtc) s.SelfStatuses.Add(kv.Key);
            }
            // Per-entity status effects, for `target.status.<name>` lookups in the DSL.
            foreach (var e in w.Entities.Values)
            {
                if (e.Statuses.Count == 0) continue;
                HashSet<CharacterStatusEffect>? active = null;
                foreach (var kv in e.Statuses)
                {
                    if (kv.Value <= nowUtc) continue;
                    (active ??= new HashSet<CharacterStatusEffect>()).Add(kv.Key);
                }
                if (active != null) s.EntityStatuses[e.Id] = active;
            }
            return s;
        }
    }
}
