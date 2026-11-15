using System.Collections.Concurrent;
using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.Session;

namespace RoBotClient.Bot.Manager;

/// <summary>
/// An immutable summary of one bot for list/detail UI binding (safe to hand across threads).
/// </summary>
public sealed record BotSnapshot(
    string Id, string CharacterName, string Account,
    string Map, string MapDisplayName, int X, int Y,
    int Level, int JobLevel, int Hp, int MaxHp, int Sp, int MaxSp,
    string Mode, int TargetId, string TargetName,
    int Kills, int BaseExp, int Zeny, int Weight, int MaxWeight,
    int MonstersInView, int EntitiesInView, bool InGame,
    double IdleSeconds, string SquadId, int SquadSlot, bool IsSquadLeader)
{
    public double HpPercent => MaxHp > 0 ? (double)Hp / MaxHp : 0;
    public double SpPercent => MaxSp > 0 ? (double)Sp / MaxSp : 0;
}

/// <summary>
/// Owns and supervises all running bots. Each bot is a BotSession + BotBehavior driven on its own task.
/// The web layer reads live state through GetSnapshot(s) / GetSession / GetBehavior(Config) and mutates
/// via SpawnBot / StopBot. All members are safe to call from UI threads.
/// </summary>
public sealed class BotManager
{
    private readonly GameDatabase _data;
    private readonly AccountStore? _accounts;
    private readonly BotConfigStore? _configs;
    private readonly PartyStateStore? _partyStates;
    private readonly BotTelemetryStore? _telemetry;
    private readonly BotLogStore? _logStore;
    private readonly Timer? _telemetryFlushTimer;
    private readonly ConcurrentDictionary<string, BotRunner> _bots = new();
    // Recent-disconnect cooldown: track when each (account, characterName) last had its runner exit so a
    // hot-loop respawn (MCP agent retrying) doesn't slam the server's still-warm session. The server can
    // take ~3 seconds to clean up the previous connection and release the account; spawning during that
    // window almost always silently fails the entry handshake (server holds it open then kicks both).
    private readonly ConcurrentDictionary<string, DateTime> _recentDisconnects = new();
    private static readonly TimeSpan SessionCooldown = TimeSpan.FromSeconds(4);
    private int _counter;

    /// <summary>One entry per recently-failed bot spawn — surface via <see cref="GetRecentSpawnFailures"/>
    /// so the MCP agent can see WHY <c>spawn_bot</c> returned success but the bot never appeared in
    /// list_bots. Capped at <see cref="MaxRecentFailures"/>; oldest dropped first.</summary>
    public sealed record SpawnFailure(string Id, string Account, string CharacterName, string Reason, DateTime AtUtc);
    private readonly Queue<SpawnFailure> _recentFailures = new();
    private readonly object _failureLock = new();
    private const int MaxRecentFailures = 50;

    private void RecordSpawnFailure(string id, string account, string characterName, string reason)
    {
        lock (_failureLock)
        {
            _recentFailures.Enqueue(new SpawnFailure(id, account, characterName, reason, DateTime.UtcNow));
            while (_recentFailures.Count > MaxRecentFailures) _recentFailures.Dequeue();
        }
    }

    /// <summary>Return spawn failures from the last <paramref name="window"/> (default 5 min) in newest-
    /// first order. Empty list when nothing failed recently.</summary>
    public IReadOnlyList<SpawnFailure> GetRecentSpawnFailures(TimeSpan? window = null)
    {
        var cutoff = DateTime.UtcNow - (window ?? TimeSpan.FromMinutes(5));
        lock (_failureLock)
            return _recentFailures.Where(f => f.AtUtc >= cutoff).Reverse().ToList();
    }

    public BotManager(GameDatabase data, AccountStore? accounts = null, BotConfigStore? configs = null,
        PartyStateStore? partyStates = null, BotTelemetryStore? telemetry = null, BotLogStore? logStore = null)
    {
        _data = data;
        _accounts = accounts;
        _configs = configs;
        _partyStates = partyStates;
        _telemetry = telemetry;
        _logStore = logStore;
        if (_telemetry != null)
            _telemetryFlushTimer = new Timer(_ => FlushDirtyTelemetry(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public GameDatabase Data => _data;
    public AccountStore? Accounts => _accounts;
    public BotConfigStore? Configs => _configs;
    public PartyStateStore? PartyStates => _partyStates;
    public BotTelemetryStore? TelemetryStore => _telemetry;
    public BotLogStore? LogStore => _logStore;

    private void FlushDirtyTelemetry()
    {
        if (_telemetry == null) return;
        foreach (var kvp in _bots)
        {
            try
            {
                if (!kvp.Value.Session.Telemetry.TakeDirty()) continue;
                _telemetry.Save(kvp.Value.Config.CharacterName, kvp.Value.Session.Telemetry.Snapshot());
            }
            catch { }
        }
    }

    /// <summary>Persist the bot's current behavior config to disk keyed by its character name. Idempotent;
    /// safe to call from configure_bot / dashboard Apply / spawn paths.</summary>
    public void SaveConfig(string botId)
    {
        if (_configs == null) return;
        if (!_bots.TryGetValue(botId, out var runner)) return;
        _configs.Set(runner.Config.CharacterName, runner.BehaviorConfig);
    }

    public IReadOnlyList<string> Ids => _bots.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Create a bot (auto-generating account/character name if not supplied) and start it. If a bot
    /// for the requested account is ALREADY running (a reconnect attempt for a still-live session), returns
    /// that existing bot id without spawning a duplicate.</summary>
    public string SpawnBot(BotBehaviorConfig behavior, string? characterName = null, string? account = null, string? password = null,
        bool isMale = true, int hairStyle = 0, int hairColor = 0)
    {
        // Dedup: if the caller asked for a specific account and that account is already in flight, hand back
        // the live id instead of starting bot2 against the same login (which the server would kick anyway).
        if (!string.IsNullOrEmpty(account))
        {
            var existing = FindRunningByAccount(account);
            if (existing != null) return existing;

            // Cooldown: a runner that just disconnected leaves a warm session on the server side for a few
            // seconds. Hammering spawn_bot during that window produces the silent "Selecting existing
            // character" loop (server holds the handshake then drops both connections). Yield the existing
            // id back to the caller if one is still cooling.
            if (_recentDisconnects.TryGetValue(account, out var disconnectedAt))
            {
                var since = DateTime.UtcNow - disconnectedAt;
                if (since < SessionCooldown)
                {
                    var wait = (SessionCooldown - since).TotalMilliseconds;
                    throw new InvalidOperationException(
                        $"Account '{account}' disconnected {since.TotalMilliseconds:F0}ms ago — wait {wait:F0}ms before respawning so the server can release the previous session.");
                }
                // Past cooldown — clear the marker so we don't keep checking against a stale timestamp.
                _recentDisconnects.TryRemove(account, out _);
            }
        }

        var n = Interlocked.Increment(ref _counter);
        var id = $"bot{n}";
        var config = new BotConfig
        {
            Account = account ?? $"bot_{n:00}",
            Password = password ?? "botbot01",
            CharacterBaseName = characterName ?? $"Bot{n}",
            IsMale = isMale,
            HairStyle = hairStyle,
            HairColor = hairColor,
        };

        // Rehydrate saved behavior config (configure_bot / dashboard Apply changes) over the spawn-form
        // defaults — so previous settings survive stop+respawn, dashboard restarts, etc.
        if (_configs != null)
        {
            var saved = _configs.Get(config.CharacterName);
            if (saved != null) behavior.CopyFrom(saved);
        }

        var session = new BotSession(config, _data);
        var brain = new BotBehavior(session, behavior, _data);
        var cts = new CancellationTokenSource();
        var runner = new BotRunner(id, config, session, brain, behavior, cts);

        session.OnLog += runner.Log;
        brain.OnLog += runner.Log;

        // Tee every OnLog line to a per-character file under bot-logs/ so a tail (or me, when iterating
        // autonomously) can see what the bot was doing without polling MCP get_bot.log. The in-memory ring
        // buffer is still authoritative for the UI; this is purely append-only persistence.
        if (_logStore != null)
        {
            // Local copy by another name — `characterName` is already the SpawnBot parameter, so the lambda
            // would shadow it and the compiler errors. Capture the resolved CharacterName instead.
            var logKey = config.CharacterName;
            Action<string> tee = msg => _logStore.Append(logKey, msg);
            session.OnLog += tee;
            brain.OnLog += tee;
        }

        // Persist party-state changes (Create / Accept / Leave / inbound LeaveParty / DisbandParty) so a
        // reconnect can restore the bot's local InParty / IsLeader / LeaderName flags — otherwise MCP
        // get_party would lie even though the server still has the bot in the party.
        if (_partyStates != null)
        {
            session.OnPartyStateChanged += () =>
                _partyStates.Save(config.CharacterName, new BotPartyState
                {
                    InParty = session.InParty,
                    IsLeader = session.IsPartyLeader,
                    LeaderName = session.PartyLeaderName,
                });
        }

        // Insert into _bots BEFORE starting the task so the auto-cleanup below can't race past the Add.
        _bots[id] = runner;

        // Persist the (possibly-merged) config now so a brand-new character also gets a baseline saved file
        // — without this, the very first reconnect would still lose anything the user set in the spawn form.
        _configs?.Set(config.CharacterName, behavior);

        runner.Task = Task.Run(async () =>
        {
            // Start trace — without this, a bot that fails the very first packet leaves no fingerprint
            // on disk (the session's own Log() wouldn't have fired yet). Tee'd through session.OnLog so
            // _logStore captures it as if it were a session line.
            session.RaiseLog($"Spawn task starting: id={id}, account='{config.Account}', character='{config.CharacterName}'.");
            var failureReason = (string?)null;
            try
            {
                if (await session.ConnectAndEnterAsync(cts.Token))
                {
                    _accounts?.Register(config.Account, config.Password, config.CharacterName);
                    // Rehydrate locally-cached party state (so MCP get_party reflects reality immediately).
                    var savedParty = _partyStates?.Get(config.CharacterName);
                    if (savedParty != null && savedParty.InParty)
                        session.RestorePartyState(savedParty.InParty, savedParty.IsLeader, savedParty.LeaderName);
                    // Rehydrate the historical event log so the controlling agent has context (kill counts,
                    // recent deaths, items used, skill casts, map times) without having to re-observe them.
                    var savedEvents = _telemetry?.Load(config.CharacterName);
                    if (savedEvents != null) session.Telemetry.Load(savedEvents);
                    await Task.WhenAll(session.RunAsync(cts.Token), brain.RunAsync(cts.Token));
                }
                else
                {
                    // ConnectAndEnterAsync logs its own FAILED reason via session.Log internally, but we
                    // also surface a one-line summary on the task so the file shows a clear end-of-task
                    // marker AND the spawn-failure ring buffer gets populated.
                    failureReason = "ConnectAndEnterAsync returned false — see preceding FAILED line for the exact reason.";
                    runner.Log($"Spawn task ending: {failureReason}");
                }
            }
            catch (OperationCanceledException)
            {
                failureReason = "cancelled (stop_bot or dashboard shutdown)";
            }
            catch (Exception ex)
            {
                failureReason = $"fatal: {ex.GetType().Name}: {ex.Message}";
                runner.Log(failureReason);
            }
            finally { await session.DisposeAsync(); }
            // Final telemetry flush — last chance to persist anything that accumulated since the periodic
            // timer's previous fire (and which would otherwise be lost when we drop the runner reference).
            try
            {
                if (_telemetry != null && session.Telemetry.TakeDirty())
                    _telemetry.Save(config.CharacterName, session.Telemetry.Snapshot());
            }
            catch { }
            // Stale-entry sweep: when the run loop ends (failed login, server kick, disconnect, stop_bot),
            // drop the runner from the dictionary so the UI / list_bots doesn't show a dead duplicate.
            _bots.TryRemove(id, out _);
            // Stamp the disconnect time so an immediate respawn against the same account hits the cooldown
            // gate instead of racing the server's session-cleanup.
            if (!string.IsNullOrEmpty(config.Account))
                _recentDisconnects[config.Account] = DateTime.UtcNow;
            // If the spawn failed BEFORE entering the world (failureReason is set and we never logged
            // "Entered world"), record it so the MCP agent can see the failure even after the bot has
            // been swept from _bots. Cancellation (user-initiated stop_bot) is logged but NOT recorded
            // as a failure — the agent expects that to drop the bot.
            if (failureReason != null && !session.InGame
                && !failureReason.StartsWith("cancelled", StringComparison.Ordinal))
            {
                RecordSpawnFailure(id, config.Account, config.CharacterName, failureReason);
            }
        });

        return id;
    }

    private string? FindRunningByAccount(string account)
    {
        foreach (var kvp in _bots)
            if (string.Equals(kvp.Value.Config.Account, account, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        return null;
    }

    /// <summary>Reconnect to a previously-used character. <paramref name="characterName"/> can be either the
    /// "BotZero" base form or the "[BOT] BotZero" display form; the account store is consulted to find which
    /// account it lives on. Returns the spawned bot id, or null if no matching record exists.</summary>
    public string? SpawnExistingBot(BotBehaviorConfig behavior, string characterName,
        bool isMale = true, int hairStyle = 0, int hairColor = 0)
    {
        var rec = _accounts?.FindByCharacter(characterName);
        if (rec == null) return null;
        var baseName = characterName.StartsWith("[BOT] ", StringComparison.OrdinalIgnoreCase)
            ? characterName.Substring("[BOT] ".Length)
            : characterName;
        // Appearance fields are sent only during character creation; ignored for an existing character.
        return SpawnBot(behavior, baseName, rec.Account, rec.Password, isMale, hairStyle, hairColor);
    }

    /// <summary>Probe a range of conventional bot account names ("bot_01".."bot_NN" with the default
    /// password) by logging into each in turn and listing characters. Anything that comes back gets written
    /// to the AccountStore so the UI dropdown / MCP <c>list_accounts</c> can see it without the user having
    /// to spawn a fresh bot first. Each probe disconnects immediately after enumeration — no bot is left
    /// running. Returns the number of accounts that responded with at least one character.</summary>
    public async Task<int> DiscoverAccountsAsync(int maxAccount = 20, string password = "botbot01", CancellationToken ct = default)
    {
        if (_accounts == null) return 0;
        var found = 0;
        var bound = Math.Clamp(maxAccount, 1, 99);
        for (var n = 1; n <= bound; n++)
        {
            if (ct.IsCancellationRequested) break;
            var account = $"bot_{n:00}";
            var cfg = new BotConfig { Account = account, Password = password };
            var session = new BotSession(cfg, _data);
            try
            {
                var chars = await session.EnumerateCharactersAsync(ct);
                if (chars.Count > 0)
                {
                    foreach (var name in chars) _accounts.Register(account, password, name);
                    found++;
                }
            }
            catch { /* swallow per-account errors so one bad probe doesn't abort the scan */ }
            finally { try { await session.DisposeAsync(); } catch { } }
        }
        return found;
    }

    public bool StopBot(string id)
    {
        if (!_bots.TryRemove(id, out var runner)) return false;
        runner.Cts.Cancel();
        return true;
    }

    public void StopAll()
    {
        foreach (var id in _bots.Keys.ToList())
            StopBot(id);
    }

    public BotSession? GetSession(string id) => _bots.TryGetValue(id, out var r) ? r.Session : null;
    public BotBehavior? GetBehavior(string id) => _bots.TryGetValue(id, out var r) ? r.Brain : null;
    public BotBehaviorConfig? GetBehaviorConfig(string id) => _bots.TryGetValue(id, out var r) ? r.BehaviorConfig : null;
    public string[] GetLog(string id) => _bots.TryGetValue(id, out var r) ? r.LogLines() : Array.Empty<string>();

    public BotSnapshot? GetSnapshot(string id) => _bots.TryGetValue(id, out var r) ? Snapshot(r) : null;

    /// <summary>Live ranking inputs for the squad-leader election (JobId + level + max HP + VIT pulled
    /// from the running session). Used by McpBotTools.AutoFormSquad and friends.</summary>
    public SquadCandidate? GetSquadCandidate(string id)
    {
        if (!_bots.TryGetValue(id, out var r)) return null;
        return r.Session.WithState(w =>
        {
            var s = w.Self;
            return new SquadCandidate(
                BotId: r.Id,
                CharacterName: s.Name,
                JobId: s.JobId,
                Level: s.Level,
                MaxHp: s.MaxHp,
                Vit: s.Vit,
                InGame: r.Session.InGame);
        });
    }

    /// <summary>All bots' ids — the MCP layer enumerates them when forming squads from "everyone in
    /// party X" / "everyone on map Y".</summary>
    public IReadOnlyCollection<string> AllBotIds() => _bots.Keys.ToList();

    public List<BotSnapshot> GetSnapshots() =>
        _bots.Values.Select(Snapshot).OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase).ToList();

    private BotSnapshot Snapshot(BotRunner r)
    {
        return r.Session.WithState(w =>
        {
            var s = w.Self;
            var selfEntity = w.Entities.TryGetValue(s.EntityId, out var e) ? e : null;
            var hp = selfEntity?.Hp ?? s.Hp;
            var maxHp = selfEntity?.MaxHp ?? s.MaxHp;
            var pos = w.SelfPosition;

            var monsters = 0;
            var entities = 0;
            foreach (var en in w.Entities.Values)
            {
                entities++;
                if (en.IsMonster && en.Id != s.EntityId) monsters++;
            }

            return new BotSnapshot(
                r.Id, s.Name, r.Config.Account,
                s.Map, _data.MapName(s.Map), pos.X, pos.Y,
                s.Level, s.JobLevel, hp, maxHp, s.Sp, s.MaxSp,
                r.Brain.Mode.ToString(), r.Brain.TargetId, r.Brain.TargetName,
                s.Kills, s.BaseExp, s.Zeny, s.Weight, s.MaxWeight,
                monsters, entities, r.Session.InGame,
                Math.Round(r.Brain.IdleSeconds, 1),
                r.BehaviorConfig.SquadId, r.BehaviorConfig.SquadSlot, r.BehaviorConfig.IsSquadLeader);
        });
    }
}

internal sealed class BotRunner
{
    public string Id { get; }
    public BotConfig Config { get; }
    public BotSession Session { get; }
    public BotBehavior Brain { get; }
    public BotBehaviorConfig BehaviorConfig { get; }
    public CancellationTokenSource Cts { get; }
    public Task? Task { get; set; }

    private readonly object _logLock = new();
    private readonly Queue<string> _log = new();

    public BotRunner(string id, BotConfig config, BotSession session, BotBehavior brain, BotBehaviorConfig behaviorConfig, CancellationTokenSource cts)
    {
        Id = id;
        Config = config;
        Session = session;
        Brain = brain;
        BehaviorConfig = behaviorConfig;
        Cts = cts;
    }

    public void Log(string message)
    {
        lock (_logLock)
        {
            _log.Enqueue($"{DateTime.Now:HH:mm:ss} {message}");
            while (_log.Count > 200) _log.Dequeue();
        }
    }

    public string[] LogLines()
    {
        lock (_logLock) return _log.ToArray();
    }
}
