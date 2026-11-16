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
        if (config.ButterflyWingRestockCount > 0)
            SetRestockTarget(config.ButterflyWingItemId, config.ButterflyWingRestockCount);
        _bot.OnAllyHit += OnAllyHit;
        _bot.OnChatHeard += OnChatHeard;
        _bot.OnBaseExpUpdated += update => { if (update.OwnKill) lock (_duelLock) _duelKill = (update, DateTime.UtcNow); };
    }

    // One duel = Engaging a target until our kill of it, summarised in the log on the tick after the kill.
    private sealed record DuelStart(DateTime At, int Hp, int Sp, string Name);
    private DuelStart? _duel;
    private readonly object _duelLock = new();
    private (ExpUpdate Update, DateTime At)? _duelKill;

    private void LogDuelResult(Snapshot snap)
    {
        ExpUpdate update;
        lock (_duelLock)
        {
            if (_duelKill is not { } kill) return;
            update = kill.Update;
            _duelKill = null;
        }
        var duel = _duel;
        _duel = null;
        if (duel == null) return;
        var job = $"+{update.JobGained:N0} job exp";
        var hpLost = update.HpLost ?? Math.Max(0, duel.Hp - snap.SelfHp);
        OnLog?.Invoke($"Duel won: {(update.Monster.Length > 0 ? update.Monster : duel.Name)} in {(DateTime.UtcNow - duel.At).TotalSeconds:F1}s — " +
                      $"+{update.Gained:N0} base exp, {job}, HP -{hpLost}, SP -{Math.Max(0, duel.Sp - snap.SelfSp)}.");
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
        CountActivityTime(snap);
        LogDuelResult(snap);

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
            _travelWingMap = null; // a fresh visit gets a fresh Fly Wing budget
            _seenHazards.Clear();
            _walkGoal = null;
            OnLog?.Invoke($"Changed maps → {snap.Map}.");
        }

        // Refresh per-map danger when our level moves enough to change which mobs threaten us. Step-based
        // so we don't burn CPU rebuilding on every tick during exp-rich training.
        if (_mapDangerLastLevel < 0 || Math.Abs(snap.SelfLevel - _mapDangerLastLevel) >= DangerLevelStep)
            RecomputeMapDanger(snap);

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
                var modeBefore = Mode;
                Mode = BotMode.Dead;
                NoteMapDeath(snap.Map);
                // Dying on the way to the hunting map is the hunting map's cost too: the route there is part of the choice.
                if (!string.Equals(snap.Map, _config.HomeMap, StringComparison.OrdinalIgnoreCase) && _shopPhase == ShopPhase.None)
                    NoteMapDeath(_config.HomeMap, onTheWay: true);
                _nextMapCheck = DateTime.MinValue; // re-judge from wherever it respawns
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
                    _mapDangerLastLevel = -1; // re-score maps now that this class has killed us
                    var context = DateTime.UtcNow - _lastFleeAt < TimeSpan.FromSeconds(15)
                        ? $" Was fleeing: {_lastFleeAction}" : "";
                    if (fresh)
                        OnLog?.Invoke($"Died to '{killerName}' (class {killerClass}) — avoiding it for the next {AvoidClassDuration.TotalMinutes:F0} min.{context}");
                    else
                        OnLog?.Invoke($"Died to '{killerName}' (class {killerClass}) again — avoidance timer extended.{context}");
                }
                else
                    OnLog?.Invoke($"Died (couldn't attribute the kill; last attacker '{attackerName}', committed target '{TargetName}').");
                // Every death is a lost duel, including an ambush while travelling (no Engaging line before it).
                var duel = _duel;
                _duel = null;
                var killer = killerName.Length > 0 ? killerName : attackerName ?? "unknown";
                OnLog?.Invoke(duel != null
                    ? $"Duel lost: {duel.Name} engaged {(DateTime.UtcNow - duel.At).TotalSeconds:F1}s ago, killed by {killer} — HP -{duel.Hp}, SP -{Math.Max(0, duel.Sp - snap.SelfSp)}."
                    : $"Duel lost: ambushed by {killer} while {modeBefore.ToString().ToLowerInvariant()} — HP -{snap.SelfMaxHp}.");
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

        // Dropped inside a wall (bad teleport coordinate): the server can't path out of a blocked cell, so
        // every walk is silently ignored. A Fly Wing is the only way out.
        if (stuck && snap.SelfPos is not { X: 0, Y: 0 } // (0,0) = position not received yet after a map change
            && DateTime.UtcNow >= _nextEscapeItem && _data?.GetWalkMap(snap.Map) is { } here
            && !here.IsWalkable(snap.SelfPos.X, snap.SelfPos.Y)
            && _bot.WithState(w => w.Self.Inventory.Any(it => it.ItemId == _config.FlyWingItemId && it.Count > 0)))
        {
            _nextEscapeItem = DateTime.UtcNow.AddSeconds(3);
            OnLog?.Invoke($"Standing in a wall at ({snap.SelfPos.X},{snap.SelfPos.Y}) on {snap.Map} — using a Fly Wing to get out.");
            await _bot.UseInventoryItemAsync(_config.FlyWingItemId, -1, ct);
            return;
        }

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

        // Spend stat/skill points per the build (StatTargets / SkillOrder). One point per tick, never blocks.
        await TickBuildAsync(ct);
        await TickGearAsync(snap, ct);
        TickMapChoice(snap);

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
            if (await HoldFiringSpotAsync(snap, current, ct)) return; // fixed monster: get to a safe firing cell first
            if (await WalkAroundToTargetAsync(snap, current, ct)) return; // behind a wall: server won't path there
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
        _bot.NoteForecast(target.Id, forecast.SecondsToKillMonster, forecast.AttacksToKill, forecast.ExpectedDamageTaken);
        _nextAttack = DateTime.UtcNow.AddSeconds(3);
        _duel = new DuelStart(DateTime.UtcNow, snap.SelfHp, snap.SelfSp, target.Name);
        OnLog?.Invoke($"Engaging {target.Name} (hp {target.MaxHp}): my hit ~{forecast.MyDamagePerHit}@{forecast.MyHitChance:P0}, " +
                      $"its hit ~{forecast.MonsterDamagePerHit}@{forecast.MonsterHitChance:P0}, " +
                      $"kill ~{forecast.SecondsToKillMonster:F1}s vs my death ~{forecast.SecondsToKillMe:F1}s" +
                      (_bot.InParty ? "." : $", ~{ExpPerSecond(snap, target, forecast, Dist(snap.SelfPos, target.Pos)):F2} exp/s incl. walk + {RestSeconds(snap, forecast):F0}s rest (solo, best in view)."));
        await MaybeAnnounceTargetAsync(target, ct);
        // Skill-only bots still need the server to know who their target is (skills auto-target the
        // bot's current target id when no explicit target is passed in the rule). Just don't issue the
        // basic attack — the next tick's TickSkillsAsync will fire the right rule.
        // A fixed monster is shot from a safe cell; the next tick walks there first (the server's attack-walk would stop
        // at the first in-range cell, maybe inside another plant's reach).
        var walkFirst = SnipesFixed(snap, target) && FindFiringSpot(snap, target) is { } spot && (spot.X != snap.SelfPos.X || spot.Y != snap.SelfPos.Y);
        if (!_config.NoAutoAttack && !walkFirst)
            await _bot.AttackAsync(target.Id, ct);
    }

    private string _lastIdleReason = "";
    private string _idleSummary = "";

    /// <summary>One-line description of what the bot is doing right now, for the dashboard and map chat.</summary>
    public string Activity => Mode switch
    {
        BotMode.Hunting => string.IsNullOrEmpty(TargetName) ? "Hunting" : $"Hunting {TargetName}",
        BotMode.Traveling => string.IsNullOrEmpty(_config.HomeMap) ? "Traveling"
            : $"Traveling to {_data?.MapName(_config.HomeMap) ?? _config.HomeMap}" + (_shopPhase == ShopPhase.ReturnTravel ? " after shopping" : ""),
        BotMode.Dead => "Dead, respawning",
        BotMode.Fleeing => "Fleeing danger",
        BotMode.Shopping => (_shopPhase == ShopPhase.Travel
            ? $"Heading to {_shopNpcName} in {_data?.MapName(_shopMap) ?? _shopMap}"
            : $"At {_shopNpcName}") + (_shopPurpose.Length > 0 ? $" to {_shopPurpose}" : ""),
        BotMode.JobChange => $"Changing job to {JobName(_config.DesiredJobId)}",
        BotMode.Parked => "Holding position (ordered)",
        BotMode.Resting => "Sitting to recover HP/SP",
        BotMode.Following => "Following the party leader",
        BotMode.Looting => "Picking up loot",
        _ => string.IsNullOrEmpty(_idleSummary) ? "Looking for targets" : $"Looking for targets: {_idleSummary}",
    };

    /// <summary>Expected base exp per second of walking to, killing and then sitting off the HP lost to
    /// <paramref name="m"/>, solo. The server pays a solo killer the monster's full base exp (no level-gap
    /// penalty), so this is the database exp over walk + forecast kill time + rest time, which makes an
    /// HP-draining fight cost what it really costs: the sitting it forces later.</summary>
    private double ExpPerSecond(Snapshot snap, MonsterInfo m, BattleForecast f, int distance)
    {
        var exp = _data?.Monster(m.ClassId)?.Exp ?? 0;
        return exp / Math.Max(0.1, f.SecondsToKillMonster + distance * _bot.SelfSecondsPerCell + RestSeconds(snap, f));
    }

    /// <summary>Seconds of sitting to regenerate the HP the forecast says this fight costs. Mirrors the server's
    /// Player.HpRegenTick: (MaxHp/50 + Vit/5)*(200+Vit)/200 per tick, a tick every 3 s sitting and doubled.
    /// ponytail: ignores potions, AddHpRecoveryPercent and Increased HP Recovery, so it overstates the cost
    /// for bots with those; fold them in if such bots start avoiding fights they'd win cheaply.</summary>
    private static double RestSeconds(Snapshot snap, BattleForecast f)
    {
        var perTick = Math.Max(1, (snap.SelfMaxHp / 50 + snap.SelfVit / 5) * (200 + snap.SelfVit) / 200);
        return f.ExpectedDamageTaken / (perTick * 2 / 3.0);
    }

    private MonsterInfo? PickTarget(Snapshot snap, out BattleForecast chosen)
    {
        chosen = default;
        var candidates = new List<(double score, MonsterInfo m, BattleForecast f)>();
        // Solo, rank by expected exp per second (walk + fight) so the bot prefers efficient monsters over merely
        // close ones. In a party, healing and shared exp change the math, so keep the nearest-first rule there.
        var solo = !_bot.InParty;
        int notHuntable = 0, avoided = 0, blacklisted = 0, hazardous = 0, unwinnable = 0, swarmed = 0, portalRisk = 0, noSafeCell = 0, winnableRested = 0;
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
            var forecast = WithHealing(Forecast(snap, m), m.MaxHp, HealingOn(snap, m));
            if (SnipesFixed(snap, m) && FindFiringSpot(snap, m) == null) { noSafeCell++; continue; }
            // Failsafe override: operator-set ForceHuntClassIds bypasses the forecast veto. The bot
            // still consults the forecast (numbers go in the Engaging log), but the unwinnable gate
            // and the group-swarm gate both yield to the override. Useful when the simulator under-
            // estimates a fight (e.g. element gear it can't see, or until the kite FSM is on for a
            // ranged build) and the operator wants the bot to commit anyway.
            var forced = _config.ForceHuntClassIds.Contains(m.ClassId);
            if (!forced && !forecast.CanWin)
            {
                unwinnable++;
                if (snap.SelfHp < snap.SelfMaxHp)
                {
                    var hp = snap.SelfHp;
                    snap.SelfHp = snap.SelfMaxHp;
                    if (WithHealing(Forecast(snap, m), m.MaxHp, HealingOn(snap, m)).CanWin) winnableRested++;
                    snap.SelfHp = hp;
                }
                continue;
            }
            var group = EngagementGroup(snap, m);
            if (!forced && group.Count > 1 && !ForecastGroupOf(snap, group).CanWin) { swarmed++; continue; }
            double score = Dist(snap.SelfPos, m.Pos);
            if (solo)
                score = -ExpPerSecond(snap, m, forecast, (int)score); // negated: candidates sort ascending
            else
            {
                var mlvl = _data?.Monster(m.ClassId)?.Level ?? 0;
                if (mlvl > 0 && mlvl <= snap.SelfLevel - 15) score += 100; // grey mob: poor exp, deprioritize
            }
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
            _restToWin = false;
            _lastIdleReason = ""; // armed for the next idle window
            _idleSummary = "";
            return c.m;
        }

        // No target. Log WHY (deduped — only fire when the reason string actually changes), so the user can
        // diagnose "FSM keeps the bot Idle" without enabling Verbose chat.
        _restToWin = winnableRested > 0;
        var reason = $"PickTarget idle — {snap.Monsters.Count} mon in view (not-huntable={notHuntable}, avoided-class={avoided}, blacklisted={blacklisted}, in-hazard={hazardous}, near-portal={portalRisk}, unwinnable={unwinnable}, group-swarm={swarmed}, no-safe-cell={noSafeCell}, unreachable={unreachable}).";
        _idleSummary = snap.Monsters.Count == 0 ? "none in view"
            : hazardous > 0 ? $"{hazardous} near a dangerous monster"
            : unwinnable > 0 ? $"{unwinnable} too strong"
            : noSafeCell > 0 ? $"{noSafeCell} fixed, no safe cell to shoot from"
            : $"{snap.Monsters.Count} in view, none huntable";
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
        var hz = HazardZones();
        if (hz.Count == 0) return null;
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
        var hzList = HazardZones();
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
            var heal = HealPerUse(r.ItemId);
            if (heal <= 0) continue;
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
            // Only grants the out-of-range free kill; a mob that answers arrows with a spell doesn't get it.
            DefenderIsImmobile = IsMonsterImmobile(mDb) && !(mDb != null && _data!.PunishesRangedAttacks(mDb.Code)),
        };
        var f = BattleSimulator.Forecast(me, MonsterCombatant(m), mods, _config.WinMargin, _config.MaxRoundsToKill);
        // The simulator only models auto-attacks. If this monster has hit us far harder than that (a skill),
        // assume one more of those lands mid-fight.
        // ponytail: learned per name, not per skill; parse MonsterSkills/*.txt if first-contact deaths matter.
        var burst = _bot.MaxHitTakenFrom(m.Name);
        if (burst > f.MonsterDamagePerHit * 2)
            f = BattleSimulator.Forecast(me with { Hp = Math.Max(0, me.Hp - burst) }, MonsterCombatant(m), mods, _config.WinMargin, _config.MaxRoundsToKill);
        return f;
    }

    /// <summary>Effective attack range for the bot. Prefers the server-synced <c>CharacterStat.Range</c>
    /// (which reflects the equipped weapon — bow = ~9, melee weapon = 1). When not synced (older server
    /// build or before first stat-update), falls back to a skill heuristic: a bot with bow skills
    /// (DoubleStrafe / ArrowShower) or any magic Bolt skill is treated as ranged at 9 tiles, otherwise
    /// melee at 1.</summary>
    private int InferAttackRange(Snapshot snap)
    {
        if (snap.SelfAttackRange > 0) return snap.SelfAttackRange;
        // The server never sends Range (not in PlayerUpdateStats), so rebuild it like Player.UpdateStats does for a bow:
        // weapon range + Vulture's Eye level. Without this a bow user with no bow skill learned looked like melee (range 1).
        if (_data != null && _data.Gear.TryGetValue(snap.SelfWeaponItemId, out var weapon) && weapon.Type == "Bow" && weapon.Range > 0)
        {
            var vultureEye = 0;
            foreach (var (skill, level) in snap.KnownSkills) if (skill == CharacterSkill.VultureEye) vultureEye = Math.Max(vultureEye, level);
            return weapon.Range + vultureEye;
        }
        foreach (var (skill, _) in snap.KnownSkills)
        {
            // No bow skills here: without a bow equipped (handled above) they fail with "wrong weapon equipped",
            // and counting them made a bowless Archer think it outranged Mandragora while it walked up to knife it.
            switch (skill)
            {
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

    /// <summary>Average HP one use restores, from the server's item effect script (0 = doesn't heal HP).</summary>
    private int HealPerUse(int itemId) => _data?.HpRecovery(itemId) ?? 0;

    /// <summary>Estimate an "effective HP buffer" the bot gets from healing potions it actually carries
    /// AND has an item-script rule for. A Mage with 50 White Potions and `use 504 if hppct < 60` is much
    /// harder to kill than the raw HP number — this is how the forecast acknowledges that. Returns 0
    /// when there's no item script, no matching rule, or no inventory. Capped at 1.5×MaxHp so a hoarder
    /// doesn't get treated as effectively immortal.</summary>
    private double EstimatePotionHpBuffer(Snapshot snap)
    {
        if (string.IsNullOrEmpty(_config.ItemScript))
        {
            // No script: MaybeHealAsync eats whatever heals, so count the biggest heals held, at most 5 uses a fight.
            if (_config.HealHpPercent <= 0) return 0;
            var heals = _bot.WithState(w => w.Self.Inventory.Where(it => it.Count > 0 && HealPerUse(it.ItemId) > 0 && _data?.IsUsableItem(it.ItemId) == true)
                .Select(it => (Heal: HealPerUse(it.ItemId), it.Count)).ToList());
            double total = 0;
            var uses = 0;
            foreach (var (heal, count) in heals.OrderByDescending(h => h.Heal))
            {
                var take = Math.Min(count, 5 - uses);
                total += take * heal;
                if ((uses += take) >= 5) break;
            }
            return Math.Min(total, snap.SelfMaxHp * 1.5);
        }
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
            var healPerUse = HealPerUse(r.ItemId);
            if (healPerUse <= 0) continue;
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
        // Without the range to snipe it, the bot stands next to the target, inside any neighbouring fixed monster's reach.
        var standsAtTarget = !SnipesFixed(snap, target);
        foreach (var m in snap.Monsters)
        {
            if (m.Id == target.Id) continue;
            if (m.MaxHp > 0 && m.Hp <= 0) continue;
            var joins = (targetAssists && m.ClassId == target.ClassId && Dist(m.Pos, target.Pos) <= AssistRadius)
                        || (IsAggressive(m.ClassId) && Dist(snap.SelfPos, m.Pos) <= HazardRadius(m.ClassId))
                        || (standsAtTarget && _data?.Monster(m.ClassId) is { } fixedDb && IsMonsterImmobile(fixedDb)
                            && ServerDistance(m.Pos.X, m.Pos.Y, target.Pos.X, target.Pos.Y) <= fixedDb.Range + 1);
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
    /// <summary>The active wander A* destination, for the dashboard minimap.</summary>
    public Position? WanderTarget => DateTime.UtcNow <= _wanderTargetUntil ? _wanderTarget : null;
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

            // The map was chosen for a camp (a cluster of pinned spawns): walk there and stay inside it.
            var zone = string.Equals(snap.Map, _config.HomeMap, StringComparison.OrdinalIgnoreCase)
                       && _mapEstimates.TryGetValue(snap.Map, out var homeEstimate) ? homeEstimate.Zone : null;
            const int zoneMargin = 3;
            for (var attempt = 0; attempt < 6; attempt++)
            {
                var tx = self.X + _heading.dx * WanderStep;
                var ty = self.Y + _heading.dy * WanderStep;
                if (zone != null)
                    (tx, ty) = zone.Contains(self.X, self.Y, zoneMargin)
                        ? (Random.Shared.Next(zone.MinX - zoneMargin, zone.MaxX + zoneMargin + 1), Random.Shared.Next(zone.MinY - zoneMargin, zone.MaxY + zoneMargin + 1))
                        : ((zone.MinX + zone.MaxX) / 2, (zone.MinY + zone.MaxY) / 2);
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
                OnLog?.Invoke(zone == null ? $"Wander: heading to ({fx},{fy}) — {p.Count} cells via A*."
                    : zone.Contains(self.X, self.Y, zoneMargin) ? $"Wander: staying in the camp, heading to ({fx},{fy}) — {p.Count} cells via A*."
                    : $"Wander: heading to the camp at ({fx},{fy}) this map was chosen for ({_mapEstimates.GetValueOrDefault(snap.Map)?.Note}) — {p.Count} cells via A*.");
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
                OnLog?.Invoke($"Stuck: no progress for {(DateTime.UtcNow - _lastProgressAt).TotalSeconds:F0}s at ({self.X},{self.Y}) on {snap.Map} — nudging to ({tx},{ty}).");
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
        var msg = Activity + ".";
        await _bot.SayAsync(msg, ct);
    }

    private async Task MaybeHealAsync(Snapshot snap, CancellationToken ct)
    {
        if (_data == null || _config.HealHpPercent <= 0 || snap.SelfMaxHp <= 0) return;
        if ((double)snap.SelfHp / snap.SelfMaxHp > _config.HealHpPercent) return;
        // Out of a fight the bot rests for free; items are for when something is hitting us.
        if (_config.RestWhenNoPotions && _targetId == 0 && !AnyAdjacentMonster(snap) && _hazards.Count == 0) return;
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
        if (_data == null) return 0;
        foreach (var id in _config.HealingItemIds)
        {
            if (!_data.IsUsableItem(id)) continue;
            foreach (var it in w.Self.Inventory)
                if (it.ItemId == id && it.Count > 0) return id;
        }
        // Then anything else that heals HP (Novice Potions, Apples, Meat, herbs), cheapest zeny per HP first.
        var best = 0;
        var bestCost = double.MaxValue;
        foreach (var it in w.Self.Inventory)
        {
            var heal = HealPerUse(it.ItemId);
            if (it.Count <= 0 || heal <= 0 || !_data.IsUsableItem(it.ItemId)) continue;
            var cost = (double)_data.UsableItemPrice(it.ItemId) / heal;
            if (cost < bestCost) { bestCost = cost; best = it.ItemId; }
        }
        return best;
    }

    // Sit to recover HP/SP when we're hurt and out of usable healing items. Stands back up once nearly full,
    // a monster comes within reach (sitting is cancelled on taking damage anyway), or a heal item appears.
    private async Task<bool> TickRestAsync(Snapshot snap, CancellationToken ct)
    {
        var hpPct = snap.SelfMaxHp > 0 ? (double)snap.SelfHp / snap.SelfMaxHp : 1.0;
        // SP only matters to bots that kill with spells. A Thief out of SP for Envenom just keeps stabbing.
        var spPct = snap.SelfMaxSp > 0 && KillsWithSpells(snap) ? (double)snap.SelfSp / snap.SelfMaxSp : 1.0;

        if (_resting)
        {
            var recovered = hpPct >= 0.95 && spPct >= 0.95;
            if ((!_config.RestWhenNoPotions && !_restToWin) || recovered || AnyAdjacentMonster(snap) || _hazards.Count > 0)
            {
                _resting = false;
                _restToWin = false;
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

        // Resting to win: something in view is beatable at full HP but not as hurt as we are. Sitting is free, while
        // wandering at 77% HP turned every Spore on pay_fild02 into "too strong" (81 s searching per kill).
        var wantRest = _config.RestWhenNoPotions && (hpPct < _config.RestBelowPercent || spPct < _config.RestBelowPercent);
        // Never sit with a monster we can't beat in view (Bot3 sat down among Greatest Generals on moc_fild03).
        if (!(wantRest || _restToWin) || !CanSit(snap) || AnyAdjacentMonster(snap) || _hazards.Count > 0) return false;

        _resting = true;
        _targetId = 0;
        _targetClass = -1;
        Mode = BotMode.Resting;
        _nextSit = DateTime.UtcNow.AddSeconds(1.5);
        OnLog?.Invoke(wantRest ? "Low on HP/SP — sitting to rest." : "Too hurt to win the fights in view — sitting to recover first.");
        await _bot.SitAsync(true, ct);
        return true;
    }

    // A Novice (job 0) needs Basic Mastery level 2 to sit; everyone else always can.
    private bool _restToWin;

    private bool KillsWithSpells(Snapshot snap) => _config.NoAutoAttack || snap.SelfJobId is 3 or 8 or 14; // Mage, Wizard, Sage // the last target pick found fights we'd win at full HP but not now

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
        // Plants never move or attack, even boss-flagged ones (the prt_fild08 Target Dummy is Special=Boss).
        if (string.Equals(m.Ai, "AiPlant", StringComparison.Ordinal)) return false;
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
    private void RecomputeMapDanger(Snapshot snap)
    {
        if (_data == null) return;
        var botLevel = snap.SelfLevel;
        var fresh = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _data.MonstersById.Values)
        {
            if (m.Spawns == null) continue;
            var levelGap = Math.Max(0, m.Level - botLevel);
            // Level gap alone missed mjolnir_02 for a Lv31: Argos/Mantis/Flora are only 1-5 levels up (danger 24),
            // yet the forecast loses to all three and 85 of them spawn there. So every aggressive spawn we can't
            // beat counts on its own, whatever its level. Immobile ones only hurt next to them: a quarter.
            var aggressive = IsAggressive(m.Id);
            // A class that killed us counts as unwinnable, whatever the forecast says: otherwise a same-level killer
            // scored 0 (the diedTo multiplier below never ran) and routes kept walking back through its map.
            var unwinnable = aggressive && (_avoidClasses.ContainsKey(m.Id)
                || !Forecast(snap, new MonsterInfo(0, m.Id, m.Name, default, m.HP, m.HP)).CanWin);
            if (levelGap == 0 && !unwinnable) continue; // mob is at or below the bot's level and beatable — not a danger
            var unwinnableDanger = unwinnable ? UnwinnableSpawnDanger * (IsMonsterImmobile(m) ? 0.25f : 1f) : 0f;
            var aggMul = aggressive ? 3.0f : 1.0f;
            // Per-class severity. Pow(1.5) makes the gap super-linear so a 35-level gap (instant-death)
            // weights ~6× a 10-level gap.
            var perGap = (float)Math.Pow(levelGap, 1.5);
            // Self-experience penalty: bot died to this class → treat as MUCH more dangerous than its
            // raw level suggests (it actually killed us before).
            var diedTo = _avoidClasses.ContainsKey(m.Id) ? 4.0f : 1.0f;
            foreach (var sp in m.Spawns)
            {
                if (string.IsNullOrEmpty(sp.Map)) continue;
                var bossMul = sp.IsMvp ? 10.0f : (sp.IsBoss ? 4.0f : 1.0f);
                var contrib = (perGap * aggMul / 100.0f + unwinnableDanger) * diedTo * bossMul * Math.Max(1, sp.Count);
                fresh[sp.Map] = (fresh.TryGetValue(sp.Map, out var prev) ? prev : 0) + contrib;
            }
        }
        _mapDanger = fresh;
        _mapDangerLastLevel = botLevel;
    }

    /// <summary>Returns map danger relative to this bot. 0 = safe (no mobs above our level), higher = more dangerous.</summary>
    private float GetMapDanger(string map) =>
        _mapDanger.TryGetValue(map, out var d) ? d : 0f;

    // A mob that never moves can only hit us within its attack range; fleeing at its scan distance (12 for Flora, range 3)
    // made the bot flee, walk back toward its exit and flee again every 10 s.
    private int HazardRadius(int classId) => _data?.Monster(classId) is { } db && IsMonsterImmobile(db)
        ? Math.Max(1, db.Range) + 2
        : (_data?.Monster(classId)?.ScanDist ?? 10) + 2;

    private const float UnwinnableSpawnDanger = 3f; // per spawn of an aggressive mob the forecast loses to ≈ 15 walked cells

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
            _seenHazards[m.Id] = (m.Pos, HazardRadius(m.ClassId), DateTime.UtcNow);
        }
        foreach (var (id, h) in _seenHazards)
            if (DateTime.UtcNow - h.at > HazardMemory) _seenHazards.Remove(id);
    }

    // Hazards seen recently but now out of view. Only path costs use them: once a fled-from mob left view, the
    // next A* went straight back through it and the bot ping-ponged between fleeing and traveling.
    // ponytail: last-seen position only; a chasing mob may have moved. Predict its position if that bites.
    private readonly Dictionary<int, (Position pos, int radius, DateTime at)> _seenHazards = new();
    private static readonly TimeSpan HazardMemory = TimeSpan.FromSeconds(30);

    private List<(int x, int y, int radius)> HazardZones()
    {
        var list = new List<(int x, int y, int radius)>(_seenHazards.Count + _hazards.Count);
        foreach (var h in _hazards) list.Add((h.pos.X, h.pos.Y, h.radius));
        foreach (var h in _seenHazards.Values) list.Add((h.pos.X, h.pos.Y, h.radius));
        return list;
    }

    // The server's pathfinder gives up past 20 cells (PathFinder.MaxDistance) and skills/attacks need line of sight, so
    // a target behind a wall got "Bash: no line of sight" spam with the bot standing still until it timed out as
    // unreachable. Walk our own uncapped A* until we can see it and the server can path the rest.
    private const int ServerMaxPath = 18; // 20 minus slack for the server stopping one cell short
    private DateTime _nextChaseWalk;
    private int _chaseLoggedFor;

    private async Task<bool> WalkAroundToTargetAsync(Snapshot snap, MonsterInfo target, CancellationToken ct)
    {
        var map = _data?.GetWalkMap(snap.Map);
        if (map == null) return false;
        string why;
        if (!map.HasLineOfSight(snap.SelfPos.X, snap.SelfPos.Y, target.Pos.X, target.Pos.Y)) why = "no line of sight";
        else if (Dist(snap.SelfPos, target.Pos) <= InferAttackRange(snap)) return false;
        else if (map.FindPath(snap.SelfPos.X, snap.SelfPos.Y, target.Pos.X, target.Pos.Y, 4000) is { } p && p.Count - 1 > ServerMaxPath)
            why = $"walk is {p.Count - 1} cells, server only paths {ServerMaxPath}";
        else return false;

        if (_chaseLoggedFor != target.Id)
        {
            _chaseLoggedFor = target.Id;
            OnLog?.Invoke($"Can't hit '{target.Name}' from here ({why}) — walking around to it myself.");
        }
        if (DateTime.UtcNow < _nextChaseWalk) return true;
        _nextChaseWalk = DateTime.UtcNow.AddSeconds(0.5);
        await WalkPathTowardAsync(snap, target.Pos.X, target.Pos.Y, 12, ct);
        return true;
    }

    // Last target WalkPathTowardAsync aimed at (travel, shop, kafra, bard...), so fleeing can keep heading there.
    private (string map, int x, int y, DateTime at)? _walkGoal;

    /// <summary>First waypoint (≤ 8 cells) of an A* path to the current walk goal where every hazard zone is
    /// steeply priced (deeper = dearer, so from inside the path leaves by the shortest way out). Null when there's
    /// no fresh goal, no path, or the waypoint wouldn't take us farther from <paramref name="threat"/>.</summary>
    private (int x, int y)? DodgeTowardGoal(Snapshot snap, Position threat, out string whyNot)
    {
        whyNot = "";
        if (_walkGoal is not { } g || g.map != snap.Map || DateTime.UtcNow - g.at > TimeSpan.FromSeconds(5))
        { whyNot = "not walking anywhere"; return null; }
        var map = _data?.GetWalkMap(snap.Map);
        if (map == null) { whyNot = "no walk map"; return null; }
        var zones = HazardZones();
        var path = map.FindPath(snap.SelfPos.X, snap.SelfPos.Y, g.x, g.y, 20000, (x, y) =>
        {
            var cost = 0f;
            foreach (var h in zones)
            {
                var d = Math.Max(Math.Abs(h.x - x), Math.Abs(h.y - y));
                if (d <= h.radius) cost += 40f * (h.radius - d + 1);
            }
            return cost;
        });
        if (path is not { Count: > 1 }) { whyNot = $"no path to my goal ({g.x},{g.y})"; return null; }
        var step = FurthestStraightLineSafeStep(snap.Map, snap.SelfPos.X, snap.SelfPos.Y, path, Math.Min(8, path.Count - 1), (g.x, g.y));
        if (step < 1) { whyNot = "a portal is in the way"; return null; }
        var wp = new Position(path[step].x, path[step].y);
        if (Dist(wp, threat) > Dist(snap.SelfPos, threat)) return path[step];
        whyNot = $"the way to my goal ({g.x},{g.y}) goes toward it";
        return null;
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
            _lastFleeLog = "";
            return false;
        }
        var hz = _hazards[bestIdx];

        if (Mode != BotMode.Fleeing)
        {
            OnLog?.Invoke($"Avoiding '{hz.name}' (aggressive, forecast says I'd lose) — fleeing. It's {nearestD} cells away "
                + $"(danger radius {hz.radius}), I'm at ({snap.SelfPos.X},{snap.SelfPos.Y}), it's at ({hz.pos.X},{hz.pos.Y}).");
            NoteMapFlee(snap.Map);
        }
        Mode = BotMode.Fleeing;
        _targetId = 0;
        _targetClass = -1;

        // On the way somewhere: sidestep along an A* path to the goal that bends out of the danger zone, instead
        // of running straight back the way we came. Fly Wing / flee-away only when it's closing in or no such path.
        string noDodge;
        if (nearestD <= hz.radius / 2) noDodge = $"it's {nearestD} cells away, too close to sidestep";
        else if (DodgeTowardGoal(snap, hz.pos, out noDodge) is { } dodge)
        {
            LogFlee("dodge", $"Flee: sidestepping '{hz.name}' toward my goal ({_walkGoal!.Value.x},{_walkGoal.Value.y}) via ({dodge.x},{dodge.y}).");
            await _bot.WalkToAsync(dodge.x, dodge.y, ct);
            return true;
        }

        // Walking away rarely outruns an aggressive mob (Angeling on pay_fild04 killed Bot3 that way).
        // A Fly Wing (random spot on this map) gets out of its sight instantly; travel/hunt just replans.
        if (DateTime.UtcNow >= _nextEscapeItem && _data?.IsUsableItem(_config.FlyWingItemId) == true
            && _bot.WithState(w => w.Self.Inventory.Any(it => it.ItemId == _config.FlyWingItemId && it.Count > 0)))
        {
            OnLog?.Invoke($"Using Fly Wing to escape '{hz.name}' (no sidestep: {noDodge}).");
            _lastFleeAction = $"Fly Wing away from '{hz.name}'";
            _lastFleeAt = DateTime.UtcNow;
            _nextEscapeItem = DateTime.UtcNow.AddSeconds(3); // let the teleport land before judging again
            await _bot.UseInventoryItemAsync(_config.FlyWingItemId, -1, ct);
            return true;
        }

        var self = snap.SelfPos;
        var dx = Math.Sign(self.X - hz.pos.X);
        var dy = Math.Sign(self.Y - hz.pos.Y);
        if (dx == 0 && dy == 0) { dx = 1; dy = 1; }
        var map = _data?.GetWalkMap(snap.Map);
        var why = $"no sidestep: {noDodge}; no Fly Wing";
        for (var step = 14; step >= 6; step -= 4)
        {
            var tx = self.X + dx * step;
            var ty = self.Y + dy * step;
            if (map == null) { await _bot.WalkToAsync(Math.Max(1, tx), Math.Max(1, ty), ct); return true; }
            if (map.TryFindWalkableNear(tx, ty, 5, out var fx, out var fy) && !CellNearHazard(fx, fy))
            {
                LogFlee("away", $"Flee: running straight away from '{hz.name}' to ({fx},{fy}) ({why}).");
                await _bot.WalkToAsync(fx, fy, ct);
                return true;
            }
        }
        LogFlee("cornered", $"Flee: no safe cell away from '{hz.name}' — stepping blindly 6 cells off ({why}).");
        await _bot.WalkToAsync(Math.Max(1, self.X + dx * 6), Math.Max(1, self.Y + dy * 6), ct);
        return true;
    }

    // Flee decisions log once per change of action (not every tick); the last one is quoted in the death log.
    private string _lastFleeLog = "";
    private string _lastFleeAction = "";
    private DateTime _lastFleeAt;
    private void LogFlee(string kind, string msg)
    {
        _lastFleeAction = msg.StartsWith("Flee: ") ? msg[6..] : msg;
        _lastFleeAt = DateTime.UtcNow;
        if (kind == _lastFleeLog) return;
        _lastFleeLog = kind;
        OnLog?.Invoke(msg);
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
        public int SelfWeaponItemId; // item in the weapon (right hand) slot, 0 = none
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

        private static int WeaponItemId(WorldState w)
        {
            var bag = w.Self.EquippedBagIds[(int)EquipSlot.RightHand];
            if (bag == 0) return 0;
            foreach (var item in w.Self.Inventory) if (item.BagId == bag) return item.ItemId;
            return 0;
        }

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
                SelfWeaponItemId = WeaponItemId(w),
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
