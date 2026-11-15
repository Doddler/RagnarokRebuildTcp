using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RebuildSharedData.Packets;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.Net;
using RoBotClient.Bot.Protocol;
using RoBotClient.Bot.State;

namespace RoBotClient.Bot.Session;

/// <summary>Latest pending party invite the bot has received but not yet acted on.</summary>
public sealed class PendingPartyInvite
{
    public int PartyId;
    public string PartyName = "";
    public string SenderName = "";
    public DateTime AtUtc;
}

/// <summary>
/// One bot: owns a connection, drives login/character/enter, then keeps a live picture of the world
/// (self stats + nearby entities) by decoding the server's broadcasts. Actions (walk/attack) send
/// intent packets; the authoritative server does the pathfinding and combat.
/// </summary>
public sealed class BotSession : IAsyncDisposable
{
    private readonly BotConfig _config;
    private readonly GameDatabase? _data;
    private readonly object _stateLock = new();
    private GameConnection? _conn;
    private bool _needReady; // set when a ChangeMaps requires us to re-announce PlayerReady
    private string _lastAttackerName = "";    // last entity to attack us (for death attribution)
    // Recent attack pairing — target entity id → (attacker entity id, when). Updated from ReadAttack; read
    // by ReadHit to attribute damage to the most recent attacker even when the HitTarget packet itself
    // doesn't carry an attacker id. ~3s freshness window matches typical melee swing rate.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, (int Attacker, DateTime At)> _recentAttacks = new();
    /// <summary>Fires when an entity OTHER than this bot took damage (HitTarget packet). Lets the
    /// behavior layer react — e.g. a tank can switch target to defend a squadmate. Both ids may be -1 if
    /// unresolved.</summary>
    public event Action<int /* targetId */, int /* attackerId */>? OnAllyHit;

    /// <summary>Fires when ANY chat line lands in our view (Say / Shout / Party / Notice). Lets the
    /// behavior layer react to leader-issued chat commands like <c>!engage 1002</c>. Skips messages this
    /// bot itself sent. <paramref name="chatType"/> is the server's PlayerChatType byte (0..3).</summary>
    public event Action<string /* senderName */, string /* text */, byte /* chatType */>? OnChatHeard;
    private string _lastDeadMonsterName = "";  // last monster to die near us (for kill attribution)
    private DateTime _lastDeadMonsterAt;
    private readonly NpcDialogState _npc = new();
    private readonly List<(DateTime at, string msg)> _errors = new();
    private readonly object _errLock = new();
    private PendingPartyInvite? _pendingInvite;
    private bool _inParty;
    private bool _isLeader;
    private string _partyLeaderName = "";

    // Map-wide positions of "important" entities (every active player, MVPs, portals) broadcast roughly every
    // 4 steps via PacketType.UpdateMapImportantEntityTracking. Lets a follower locate its leader even when the
    // leader is off-screen (out of normal EntityView range).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, RebuildSharedData.Data.Position> _mapPositions = new();

    public WorldState World { get; } = new();
    public bool InGame { get; private set; }
    public event Action<string>? OnLog;

    /// <summary>Fires whenever this bot's local party state changes (Create/Accept/Leave/inbound broadcast).
    /// The BotManager hooks this to persist the new state to disk so a reconnect can restore it.</summary>
    public event Action? OnPartyStateChanged;

    /// <summary>Set the party state from a persisted snapshot, without sending anything to the server. Used
    /// when reconnecting to a character that's still in a party server-side, so MCP get_party doesn't lie.</summary>
    public void RestorePartyState(bool inParty, bool isLeader, string leaderName)
    {
        _inParty = inParty;
        _isLeader = isLeader;
        _partyLeaderName = leaderName ?? "";
        // No OnPartyStateChanged here — restore is loading saved state, not a change worth persisting back.
    }

    /// <summary>Level-stamped event log (kills, deaths, loot, level-ups, map changes) for the UI and MCP.</summary>
    public BotTelemetry Telemetry { get; } = new();

    /// <summary>Most recent server ErrorMessage strings (oldest first, timestamped), so the UI/MCP can surface
    /// rejections that the fire-and-forget action senders can't return synchronously.</summary>
    public IReadOnlyList<string> RecentErrors(int max = 10)
    {
        lock (_errLock)
        {
            var n = Math.Min(max, _errors.Count);
            var list = new List<string>(n);
            for (var i = _errors.Count - n; i < _errors.Count; i++)
                list.Add($"{_errors[i].at:HH:mm:ss} {_errors[i].msg}");
            return list;
        }
    }

    /// <summary>The newest server error at/after the given UTC time, or null — lets an action tool detect a
    /// rejection that arrived shortly after it sent its request.</summary>
    public string? LatestErrorAfter(DateTime sinceUtc)
    {
        lock (_errLock)
        {
            for (var i = _errors.Count - 1; i >= 0; i--)
                if (_errors[i].at >= sinceUtc) return _errors[i].msg;
            return null;
        }
    }

    /// <summary>Name of the most recent entity to attack this bot — set by ReadAttack whenever the bot is
    /// the target. Used by the behavior for proper death attribution (the committed target is often NOT the
    /// actual killer when assist mobs or aggressor pile-ons are involved).</summary>
    public string LastAttackerName => _lastAttackerName;

    /// <summary>The most recent party invite this bot has received but not yet acted on, or null.</summary>
    public PendingPartyInvite? GetPendingPartyInvite() => _pendingInvite;

    /// <summary>Best-effort flag for "this bot is currently in a party" (tracked optimistically from our own
    /// create/accept/leave actions; cleared on a LeaveParty / DisbandParty broadcast).</summary>
    public bool InParty => _inParty;

    /// <summary>True if this bot organized the party (called CreateParty); false if it joined via accept.</summary>
    public bool IsPartyLeader => _isLeader;

    /// <summary>Name of the party leader for follower bots (captured from the invite's sender). Empty for
    /// leaders or when the party was joined without a pending invite in scope.</summary>
    public string PartyLeaderName => _partyLeaderName;

    /// <summary>Last known map-wide position of an entity from the minimap broadcast, or null if we haven't
    /// seen the id on the current map. Used by follower mode to track the leader when they're outside our
    /// normal view range.</summary>
    public Position? GetMapPosition(int entityId) =>
        _mapPositions.TryGetValue(entityId, out var p) ? p : null;

    public BotSession(BotConfig config, GameDatabase? data = null)
    {
        _config = config;
        _data = data;
    }

    private void Log(string msg) => OnLog?.Invoke(msg);

    /// <summary>External entry to the session's OnLog channel — used by BotManager to log task-level
    /// lifecycle events (spawn start, spawn-task end) through the same teeing setup as session-internal
    /// logs, so they land in the per-bot log file regardless of when they fire.</summary>
    public void RaiseLog(string msg) => OnLog?.Invoke(msg);

    /// <summary>Run a read function against the world state under the state lock (consistent snapshot).</summary>
    public T WithState<T>(Func<WorldState, T> read)
    {
        lock (_stateLock) return read(World);
    }

    /// <summary>A consistent copy of the current NPC-conversation state for the behavior to drive.</summary>
    public NpcDialogState SnapshotNpc()
    {
        lock (_stateLock) return _npc.Clone();
    }

    // ---- connect / login / enter ----

    public async Task<bool> ConnectAndEnterAsync(CancellationToken ct = default)
    {
        var chars = await LoginAsync(createAccount: false, ct);
        if (_conn == null)
        {
            Log("Regular login failed — creating the account.");
            chars = await LoginAsync(createAccount: true, ct);
        }
        if (_conn == null) { Log("FAILED: could not log in."); return false; }

        var desired = _config.CharacterName;
        var existing = chars.FirstOrDefault(c => string.Equals(c.Name, desired, StringComparison.Ordinal));
        if (existing.Name != null)
        {
            Log($"Selecting existing character '{desired}'.");
            var w = new PacketWriter();
            w.WritePacketType(PacketType.EnterServer);
            w.Write(false);
            w.Write(desired);
            await _conn.SendPacketAsync(w, ct);
        }
        else
        {
            var slot = FirstFreeSlot(chars);
            Log($"Creating character '{desired}' in slot {slot}.");
            var w = new PacketWriter();
            w.WritePacketType(PacketType.EnterServer);
            w.Write(true);
            w.Write(desired);
            w.Write(Math.Clamp(_config.HairStyle, 0, 19)); // head = hair style
            w.Write(Math.Clamp(_config.HairColor, 0, 8));  // hair = hair color
            w.Write((byte)slot);
            w.Write(_config.Str);
            w.Write(_config.Agi);
            w.Write(_config.Vit);
            w.Write(_config.Int);
            w.Write(_config.Dex);
            w.Write(_config.Luk);
            w.Write(_config.IsMale);
            await _conn.SendPacketAsync(w, ct);
        }

        using var enterCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        enterCts.CancelAfter(TimeSpan.FromSeconds(10));
        var enterStart = DateTime.UtcNow;
        var packetsBeforeEntry = 0;
        try
        {
            while (true)
            {
                var r = await _conn.ReceivePacketAsync(enterCts.Token);
                if (r == null)
                {
                    Log($"FAILED: socket closed before entering the world (after {(DateTime.UtcNow - enterStart).TotalMilliseconds:F0}ms, {packetsBeforeEntry} pre-entry packets).");
                    return false;
                }
                var type = r.ReadPacketType();
                if (type == PacketType.EnterServer)
                {
                    var id = r.ReadInt32();
                    var map = r.ReadString();
                    r.ReadBytes(16); // guid
                    lock (_stateLock)
                    {
                        World.Self.EntityId = id;
                        World.Self.Map = map;
                        World.Self.Name = desired;
                    }
                    Telemetry.Record(TelemetryEventType.MapChange, World.Self.Level, map);
                    Log($"Entered world: entityId={id}, map='{map}' (handshake took {(DateTime.UtcNow - enterStart).TotalMilliseconds:F0}ms).");
                    break;
                }
                if (type == PacketType.ErrorMessage)
                {
                    Log($"FAILED: server returned ErrorMessage during entry: '{r.ReadString()}'.");
                    return false;
                }
                packetsBeforeEntry++;
                DispatchBody(r, type); // pre-entry packets (e.g. initial UpdatePlayerData)
            }
        }
        catch (OperationCanceledException) when (enterCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Distinguish entry-timeout from external stop_bot. The latter is "user wanted to stop"; the
            // former is the silent-failure mode that was producing repeating "Selecting existing character"
            // lines with no follow-up "Entered world".
            Log($"FAILED: entry handshake timed out after 10s — server didn't return EnterServer ({packetsBeforeEntry} pre-entry packets received).");
            return false;
        }

        await _conn.SendSimpleAsync(PacketType.PlayerReady, ct);
        InGame = true;
        return true;
    }

    /// <summary>Log in to the configured account and return the character names on it, then cleanly close
    /// the connection without entering the world. Used by <see cref="Manager.BotManager"/> discovery to
    /// enumerate accounts that already exist on the server without spawning a running bot. Empty list on
    /// any failure (bad creds, account doesn't exist, server down, etc.).</summary>
    public async Task<IReadOnlyList<string>> EnumerateCharactersAsync(CancellationToken ct = default)
    {
        var chars = await LoginAsync(createAccount: false, ct);
        if (_conn != null)
        {
            try { await _conn.DisposeAsync(); } catch { /* best-effort */ }
            _conn = null;
        }
        var names = new List<string>(chars.Count);
        foreach (var c in chars) names.Add(c.Name);
        return names;
    }

    private async Task<List<CharInfo>> LoginAsync(bool createAccount, CancellationToken ct)
    {
        var conn = new GameConnection();
        await conn.ConnectAsync(new Uri(_config.ServerUri), ct);
        await conn.SendRawAsync(
            LoginHandshake.BuildLogin(_config.ServerVersion, _config.Account, _config.Password, createAccount), ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        var r = await conn.ReceivePacketAsync(cts.Token);
        if (r == null) { await conn.DisposeAsync(); return new(); }

        var type = r.ReadPacketType();
        if (type == PacketType.ConnectionDenied)
        {
            Log($"ConnectionDenied: '{r.ReadString()}'");
            await conn.DisposeAsync();
            return new();
        }
        if (type != PacketType.ConnectionApproved)
        {
            Log($"Unexpected first packet: {type}");
            await conn.DisposeAsync();
            return new();
        }

        if (r.ReadBoolean())             // hasToken
            r.ReadBytes(r.ReadInt32());

        var count = r.ReadInt32();
        var chars = new List<CharInfo>();
        for (var i = 0; i < count; i++)
        {
            var name = r.ReadString();
            var slot = r.ReadInt32();
            var map = r.ReadString();
            var summaryLen = r.ReadInt32();
            for (var j = 0; j < summaryLen / 4; j++)
                r.ReadInt32();
            chars.Add(new CharInfo(name, slot, map));
        }

        _conn = conn;
        return chars;
    }

    private static int FirstFreeSlot(List<CharInfo> chars)
    {
        for (var s = 0; s < 3; s++)
            if (chars.All(c => c.Slot != s))
                return s;
        return 0;
    }

    // ---- run loop ----

    public async Task RunAsync(CancellationToken ct)
    {
        if (_conn == null) return;
        var ping = PingLoopAsync(ct);
        var sessionStart = DateTime.UtcNow;
        var serverDrop = false;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var r = await _conn.ReceivePacketAsync(ct);
                if (r == null)
                {
                    serverDrop = true;
                    var seconds = (DateTime.UtcNow - sessionStart).TotalSeconds;
                    Log($"Disconnected by server (session was up for {seconds:F0}s).");
                    break;
                }
                Dispatch(r);
                if (_needReady)
                {
                    _needReady = false;
                    await _conn.SendSimpleAsync(PacketType.PlayerReady, ct);
                    Log($"Map changed to '{World.Self.Map}' — re-sent PlayerReady.");
                }
            }
        }
        catch (OperationCanceledException) { /* clean stop_bot — no disconnect to count */ }
        InGame = false;
        if (serverDrop)
        {
            // Telemetry: count server-initiated drops separately from clean stops so an agent can detect a
            // bot that's disconnect-looping (multiple Disconnect events in a short window).
            Telemetry.Record(TelemetryEventType.Disconnect, World.Self.Level,
                detail: World.Self.Map, value: (int)(DateTime.UtcNow - sessionStart).TotalSeconds);
        }
        try { await ping; } catch { }
    }

    private async Task PingLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(5000, ct);
                if (_conn != null) await _conn.SendSimpleAsync(PacketType.Ping, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    // ---- actions ----

    public Task WalkToAsync(int x, int y, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.StartWalk);
        w.Write((short)x);
        w.Write((short)y);
        return _conn.SendPacketAsync(w, ct);
    }

    public Task AttackAsync(int entityId, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.Attack);
        w.Write(entityId);
        return _conn.SendPacketAsync(w, ct);
    }

    public Task StopAsync(CancellationToken ct = default) =>
        _conn?.SendSimpleAsync(PacketType.StopAction, ct) ?? Task.CompletedTask;

    /// <summary>Respawn after death. inPlace=false returns to the save point (the normal case).</summary>
    public Task RespawnAsync(bool inPlace = false, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter(8);
        w.WritePacketType(PacketType.Respawn);
        w.Write((byte)(inPlace ? 1 : 0)); // server reads this byte — omitting it made it read past the packet
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Public chat on the current map — used by the verbose toggle to announce FSM changes.</summary>
    public Task SayAsync(string text, CancellationToken ct = default)
    {
        if (_conn == null || string.IsNullOrEmpty(text)) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.Say);
        w.Write(text);
        w.Write((byte)PlayerChatType.Say);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Request to pick up a ground item by its drop id; the server walks us there if needed.</summary>
    public Task PickUpAsync(int dropId, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.PickUpItem);
        w.Write(dropId);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Use/consume an inventory item (e.g. a potion). target=-1 for self.
    /// IMPORTANT: only call with a validated Useable item id — the server disconnects the player on an unknown id.</summary>
    public Task UseInventoryItemAsync(int itemId, int target = -1, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        Telemetry.Record(TelemetryEventType.UsedItem, World.Self.Level, _data?.ItemName(itemId) ?? $"#{itemId}");
        var w = new PacketWriter();
        w.WritePacketType(PacketType.UseInventoryItem);
        w.Write(itemId);
        w.Write(target);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Equip an inventory item by its bag id. The server validates job/level/position and just
    /// returns an error on a bad request.</summary>
    public Task EquipItemAsync(int bagId, CancellationToken ct = default) => SendEquipAsync(bagId, true, ct);

    /// <summary>Unequip the item in the given bag id.</summary>
    public Task UnequipItemAsync(int bagId, CancellationToken ct = default) => SendEquipAsync(bagId, false, ct);

    /// <summary>Slot a card (srcBagId) into a piece of slotted gear (gearBagId). Server validates the slot
    /// count, that the card's EquipPosition matches the gear's, and that there's a free slot — on failure
    /// an ErrorMessage comes back.</summary>
    public Task SocketCardAsync(int gearBagId, int cardBagId, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.SocketEquipment);
        w.Write(gearBagId);
        w.Write(cardBagId);
        return _conn.SendPacketAsync(w, ct);
    }

    private Task SendEquipAsync(int bagId, bool equip, CancellationToken ct)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.EquipUnequipGear);
        w.Write(bagId);
        w.Write(equip);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Click an NPC entity to start a conversation. Clears any stale dialog state first.</summary>
    public Task NpcClickAsync(int entityId, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            _npc.Phase = NpcPhase.None;
            _npc.Options.Clear();
            _npc.ShopItems.Clear();
        }
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.NpcClick);
        w.Write(entityId);
        return _conn.SendPacketAsync(w, ct);
    }

    public Task NpcAdvanceAsync(CancellationToken ct = default) =>
        _conn?.SendSimpleAsync(PacketType.NpcAdvance, ct) ?? Task.CompletedTask;

    public Task NpcSelectOptionAsync(int index, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.NpcSelectOption);
        w.Write(index);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Submit a shop order. For BUY each line is (itemId, qty); for SELL each is (bagId, qty).
    /// An empty list closes the shop.</summary>
    public Task ShopBuySellAsync(IReadOnlyList<(int id, int count)> lines, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.ShopBuySell);
        w.Write(lines.Count);
        foreach (var (id, count) in lines)
        {
            w.Write(id);
            w.Write(count);
        }
        return _conn.SendPacketAsync(w, ct);
    }

    public Task ShopCloseAsync(CancellationToken ct = default) =>
        ShopBuySellAsync(Array.Empty<(int, int)>(), ct);

    /// <summary>Drop <paramref name="count"/> units of the inventory stack identified by
    /// <paramref name="bagId"/> at the bot's feet. Wire shape mirrors <c>PacketDropItem</c> on the
    /// server: Int32 bagId, Int16 count. Equipped items are rejected server-side, so this is safe to
    /// call without a pre-check; the bot's equip state may lag the server.</summary>
    public Task DropItemAsync(int bagId, int count, CancellationToken ct = default)
    {
        if (_conn == null || count <= 0) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.DropItem);
        w.Write(bagId);
        w.Write((short)Math.Min(count, short.MaxValue));
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Spend stat points: 6 deltas in STR,AGI,VIT,INT,DEX,LUK order. The server validates
    /// affordability and the 99 cap, so a rejected request is a harmless no-op.</summary>
    public Task ApplyStatPointsAsync(int[] deltas, CancellationToken ct = default)
    {
        if (_conn == null || deltas.Length < 6) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.ApplyStatPoints);
        for (var i = 0; i < 6; i++) w.Write(deltas[i]);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Spend one skill point to raise a skill by a level. The server validates points,
    /// prerequisites and max level (a rejected request just returns an ErrorMessage).</summary>
    public Task ApplySkillPointAsync(CharacterSkill skill, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.ApplySkillPoint);
        w.Write((byte)skill);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Spend available skill points into <paramref name="skill"/> until it reaches
    /// <paramref name="minLevel"/>, or until no points remain. Returns the number of points sent. Bounded by
    /// a hard upper limit so a stale read of SkillPoints can't drive an infinite loop.</summary>
    public async Task<int> EnsureSkillAsync(CharacterSkill skill, int minLevel, CancellationToken ct = default)
    {
        var sent = 0;
        for (var i = 0; i < 12; i++) // hard cap
        {
            var (level, points) = WithState(w =>
            {
                var lv = 0;
                foreach (var k in w.Self.KnownSkills) if (k.Skill == skill && k.Level > lv) lv = k.Level;
                foreach (var k in w.Self.GrantedSkills) if (k.Skill == skill && k.Level > lv) lv = k.Level;
                return (lv, w.Self.SkillPoints);
            });
            if (level >= minLevel) break;
            if (points <= 0) break;
            await ApplySkillPointAsync(skill, ct);
            sent++;
            try { await Task.Delay(250, ct); } catch { break; } // let the server apply + broadcast the update
        }
        return sent;
    }

    /// <summary>Sit or stand. Sitting speeds HP/SP regen but is cancelled server-side on taking damage.
    /// A Novice (job 0) needs Basic Mastery level 2 to sit, otherwise the server rejects with an error.</summary>
    public Task SitAsync(bool sit, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.SitStand);
        w.Write(sit);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Cast a single-target skill at an entity. The wire "type" byte is always Enemy — the server
    /// routes by it and then validates ally-vs-enemy from the skill's own target type, so this also covers
    /// Ally-target skills (heal/buff a party member) by passing the ally's entity id.</summary>
    public Task UseSkillOnTargetAsync(CharacterSkill skill, int level, int targetId, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        Telemetry.Record(TelemetryEventType.SkillCast, World.Self.Level, $"{skill}@enemy", level);
        var w = new PacketWriter();
        w.WritePacketType(PacketType.Skill);
        w.Write((byte)SkillTarget.Enemy);
        w.Write(targetId);
        w.Write((byte)skill);
        w.Write((byte)level);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Cast a self-targeted skill (buff, heal-self). Note: the self path encodes the skill id as a
    /// SHORT, unlike the byte used by the target/ground paths.</summary>
    public Task UseSkillSelfAsync(CharacterSkill skill, int level, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        Telemetry.Record(TelemetryEventType.SkillCast, World.Self.Level, $"{skill}@self", level);
        var w = new PacketWriter();
        w.WritePacketType(PacketType.Skill);
        w.Write((byte)SkillTarget.Self);
        w.Write((short)skill);
        w.Write((byte)level);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Cast a ground-targeted skill at a cell (AoE / placed skills). The server requires line of
    /// sight from the caster to the target cell.</summary>
    public Task UseSkillGroundAsync(CharacterSkill skill, int level, int x, int y, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        Telemetry.Record(TelemetryEventType.SkillCast, World.Self.Level, $"{skill}@ground({x},{y})", level);
        var w = new PacketWriter();
        w.WritePacketType(PacketType.Skill);
        w.Write((byte)SkillTarget.Ground);
        w.Write(new Position(x, y));
        w.Write((byte)skill);
        w.Write((byte)level);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Create a new party with the given name. Server requirement: Basic Mastery level 6+.
    /// Optionally invite an entity right after creation (0 = no follow-up invite).</summary>
    public Task CreatePartyAsync(string partyName, int inviteEntityId = 0, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        _inParty = true; // optimistic; ErrorMessage / ServerResult will surface if the server refuses
        _isLeader = true;
        _partyLeaderName = ""; // self is the leader; followers compare PartyLeaderName == ""
        OnPartyStateChanged?.Invoke();
        var w = new PacketWriter();
        w.WritePacketType(PacketType.CreateParty);
        w.Write(partyName);
        w.Write(inviteEntityId);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Invite a nearby player by entity id (must be the party leader).</summary>
    public Task InvitePartyMemberAsync(int entityId, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.InvitePartyMember);
        w.Write((byte)0); // 0 = invite by entity id
        w.Write(entityId);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Invite a player by character name (must be the party leader).</summary>
    public Task InvitePartyMemberByNameAsync(string name, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        var w = new PacketWriter();
        w.WritePacketType(PacketType.InvitePartyMember);
        w.Write((byte)1); // 1 = invite by name
        w.Write(name);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Accept a pending party invite. The partyId comes from the InvitePartyMember broadcast
    /// (see <see cref="GetPendingPartyInvite"/>).</summary>
    public Task AcceptPartyInviteAsync(int partyId, CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        // Capture the leader's name from the pending invite BEFORE we clear it (the sender = the inviter,
        // which on this server is the party leader). Followers use this to find the leader in-view.
        var leaderName = _pendingInvite?.SenderName ?? _partyLeaderName;
        _inParty = true; // optimistic
        _isLeader = false;
        _partyLeaderName = leaderName ?? "";
        _pendingInvite = null;
        OnPartyStateChanged?.Invoke();
        var w = new PacketWriter();
        w.WritePacketType(PacketType.AcceptPartyInvite);
        w.Write(partyId);
        return _conn.SendPacketAsync(w, ct);
    }

    /// <summary>Leave the current party.</summary>
    public Task LeavePartyAsync(CancellationToken ct = default)
    {
        if (_conn == null) return Task.CompletedTask;
        _inParty = false;
        _isLeader = false;
        _partyLeaderName = "";
        OnPartyStateChanged?.Invoke();
        var w = new PacketWriter();
        w.WritePacketType(PacketType.UpdateParty);
        w.Write((byte)PartyClientAction.LeaveParty);
        return _conn.SendPacketAsync(w, ct);
    }

    // ---- packet dispatch ----

    private void Dispatch(PacketReader r) => DispatchBody(r, r.ReadPacketType());

    private void DispatchBody(PacketReader r, PacketType type)
    {
        try
        {
            lock (_stateLock)
            {
                switch (type)
                {
                    case PacketType.UpdatePlayerData: ReadUpdatePlayerData(r); break;
                    case PacketType.CreateEntity2: ReadCreateEntity2(r); break;
                    case PacketType.RemoveEntity: World.Entities.TryRemove(r.ReadInt32(), out _); break;
                    case PacketType.RemoveAllEntities: World.Entities.Clear(); break;
                    case PacketType.DropItem: ReadDropItem(r); break;
                    case PacketType.PickUpItem: ReadPickUpItem(r); break;
                    case PacketType.AddOrRemoveInventoryItem: ReadInventoryDelta(r); break;
                    case PacketType.NpcInteraction: ReadNpcInteraction(r); break;
                    case PacketType.OpenShop: ReadOpenShop(r); break;
                    case PacketType.UpdateZeny: World.Self.Data[PlayerStat.Zeny] = r.ReadInt32(); break;
                    case PacketType.EquipUnequipGear: ReadEquipUpdate(r); break;
                    case PacketType.ChangeMaps: ReadChangeMaps(r); break;
                    case PacketType.StartWalk: ReadStartWalk(r); break;
                    case PacketType.Move: UpdatePosition(r.ReadInt32(), r.ReadPosition(), CharacterState.Idle); break;
                    case PacketType.StopImmediate: UpdatePosition(r.ReadInt32(), r.ReadPosition(), CharacterState.Idle); break;
                    case PacketType.StopAction: SetState(r.ReadInt32(), CharacterState.Idle); break;
                    case PacketType.SitStand: ReadSitStand(r); break;
                    case PacketType.ErrorMessage: ReadError(r); break;
                    case PacketType.ServerResult: ReadServerResult(r); break;
                    case PacketType.SkillError: ReadSkillError(r); break;
                    case PacketType.InvitePartyMember: ReadPartyInvite(r); break;
                    case PacketType.UpdateParty: ReadUpdateParty(r); break;
                    case PacketType.UpdateMapImportantEntityTracking: ReadMapImportantEntities(r); break;
                    case PacketType.Attack: ReadAttack(r); break;
                    case PacketType.TakeDamage: ApplyDamage(r.ReadInt32(), r.ReadInt32()); break;
                    case PacketType.HitTarget: ReadHit(r); break;
                    case PacketType.Death: SetDead(r.ReadInt32()); break;
                    case PacketType.LevelUp: ReadLevelUp(r); break;
                    case PacketType.HpRecovery: ReadHpRecovery(r); break;
                    case PacketType.ChangeSpValue: ReadChangeSp(r); break;
                    case PacketType.GainExp: ReadGainExp(r); break;
                    case PacketType.ApplyStatusEffect: ReadApplyStatusEffect(r); break;
                    case PacketType.RemoveStatusEffect: ReadRemoveStatusEffect(r); break;
                    case PacketType.Say: ReadSay(r); break;
                    default: break; // not modelled yet — safely ignored
                }
            }
        }
        catch (Exception ex)
        {
            Log($"(warn) failed to parse {type}: {ex.Message}");
        }
    }

    private void ReadUpdatePlayerData(PacketReader r)
    {
        var self = World.Self;
        foreach (var stat in PlayerClientStatusDef.PlayerUpdateData)
            self.Data[stat] = r.ReadInt32();
        foreach (var stat in PlayerClientStatusDef.PlayerUpdateStats)
            self.Stats[stat] = r.ReadInt32();

        self.AttackSpeed = r.ReadFloat();
        self.Weight = r.ReadInt32();
        self.CartWeight = r.ReadInt32();
        self.MaxWeight = self.Stats.GetValueOrDefault(CharacterStat.WeightCapacity);

        if (r.ReadBoolean()) // hasSkills
        {
            self.KnownSkills.Clear();
            var n = r.ReadInt16();
            for (var i = 0; i < n; i++)
                self.KnownSkills.Add(new SkillEntry((CharacterSkill)r.ReadInt16(), r.ReadByte()));

            self.GrantedSkills.Clear();
            var g = r.ReadInt16();
            for (var i = 0; i < g; i++)
                self.GrantedSkills.Add(new SkillEntry((CharacterSkill)r.ReadInt16(), r.ReadByte()));
        }

        if (r.ReadBoolean()) // hasInventory
        {
            self.Inventory.Clear();
            ReadInventory(r, self.Inventory);
            if (r.ReadByte() == 1)
                ReadInventory(r, null); // cart, discarded
            for (var i = 0; i < 10; i++)
                self.EquippedBagIds[i] = r.ReadInt32();
            self.AmmoId = r.ReadInt32();
        }
    }

    private static void ReadInventory(PacketReader r, List<InventoryItemView>? into)
    {
        if (r.ReadByte() == 0) // hasBagData
            return;

        var regularCount = r.ReadInt32();
        for (var i = 0; i < regularCount; i++)
        {
            var id = r.ReadInt32();
            var count = r.ReadInt16();
            into?.Add(new InventoryItemView { BagId = id, ItemId = id, Count = count });
        }

        var uniqueCount = r.ReadInt32();
        for (var i = 0; i < uniqueCount; i++)
        {
            var bagId = r.ReadInt32();
            var id = r.ReadInt32();
            var count = r.ReadInt16();
            var flags = r.ReadByte();
            var refine = r.ReadByte();
            var guid = new Guid(r.ReadBytes(16));
            var cards = new int[4];
            for (var c = 0; c < 4; c++)
                cards[c] = r.ReadInt32();
            into?.Add(new InventoryItemView
            {
                BagId = bagId, ItemId = id, Count = count, IsUnique = true,
                Refine = refine, UniqueId = guid, Cards = cards
            });
        }
    }

    private void ReadChangeMaps(PacketReader r)
    {
        World.Self.Map = r.ReadString();
        World.Entities.Clear();
        World.GroundItems.Clear();
        _mapPositions.Clear(); // minimap broadcast is per-map; old entries are stale across map changes
        Telemetry.Record(TelemetryEventType.MapChange, World.Self.Level, World.Self.Map);
        _needReady = true; // the receive loop will re-send PlayerReady to re-activate us
    }

    private void ReadCreateEntity2(PacketReader r)
    {
        var et = (CreateEntityEventType)r.ReadByte();
        if (et == CreateEntityEventType.Toss) r.ReadPosition();
        var sp = r.MemoryPackDeserializeWithLength<EntitySpawnParameters>();

        var ev = new EntityView
        {
            Id = sp.ServerId,
            Type = sp.Type,
            ClassId = sp.ClassId,
            Name = sp.Name ?? "",
            Position = sp.Position,
            State = sp.State,
            Facing = sp.Facing,
            Level = sp.Level,
            Hp = sp.Hp,
            MaxHp = sp.MaxHp,
        };
        World.Entities[ev.Id] = ev;

        if (sp.IsMainCharacter || sp.ServerId == World.Self.EntityId)
        {
            World.Self.EntityId = sp.ServerId;
            World.Self.JobId = sp.ClassId;
            if (!string.IsNullOrEmpty(sp.Name)) World.Self.Name = sp.Name;
        }
    }

    private void ReadDropItem(PacketReader r)
    {
        var dropId = r.ReadInt32();
        var x = r.ReadFloat();
        var y = r.ReadFloat();
        var itemId = r.ReadInt32();
        var count = r.ReadInt16();
        r.ReadBoolean(); // isNewDrop — drives the client's drop animation only
        World.GroundItems[dropId] = new GroundItemView
        {
            DropId = dropId, ItemId = itemId, Count = count, X = (int)x, Y = (int)y,
        };
    }

    private void ReadPickUpItem(PacketReader r)
    {
        var pickerId = r.ReadInt32();
        var dropId = r.ReadInt32();
        if (World.GroundItems.TryRemove(dropId, out var item) && pickerId == World.Self.EntityId)
        {
            var name = _data?.ItemName(item.ItemId) ?? $"#{item.ItemId}";
            Telemetry.Record(TelemetryEventType.Loot, World.Self.Level, name, item.Count);
        }
    }

    private void ReadInventoryDelta(PacketReader r)
    {
        var inv = World.Self.Inventory;
        if (r.ReadBoolean()) // isAdd — the body carries the authoritative new stack count
        {
            var type = (ItemType)r.ReadByte();
            var bagId = r.ReadInt32();
            r.ReadInt16();              // change (delta) — for the client's popup only
            World.Self.Weight = r.ReadInt32();
            if (type == ItemType.RegularItem)
            {
                var itemId = r.ReadInt32();
                var count = r.ReadInt16();
                var existing = inv.FirstOrDefault(i => i.BagId == bagId);
                if (existing != null) { existing.ItemId = itemId; existing.Count = count; }
                else inv.Add(new InventoryItemView { BagId = bagId, ItemId = itemId, Count = count });
            }
            else if (type == ItemType.UniqueItem)
            {
                var itemId = r.ReadInt32();
                var count = r.ReadInt16();
                r.ReadByte();           // flags
                var refine = r.ReadByte();
                var guid = new Guid(r.ReadBytes(16));
                var cards = new int[4];
                for (var c = 0; c < 4; c++) cards[c] = r.ReadInt32();
                var existing = inv.FirstOrDefault(i => i.BagId == bagId);
                if (existing != null)
                {
                    existing.ItemId = itemId; existing.Count = count; existing.IsUnique = true;
                    existing.Refine = refine; existing.UniqueId = guid; existing.Cards = cards;
                }
                else inv.Add(new InventoryItemView
                {
                    BagId = bagId, ItemId = itemId, Count = count, IsUnique = true,
                    Refine = refine, UniqueId = guid, Cards = cards,
                });
            }
        }
        else // remove
        {
            var bagId = r.ReadInt32();
            var change = r.ReadInt16();
            World.Self.Weight = r.ReadInt32();
            r.ReadBoolean();            // notifyUser
            var existing = inv.FirstOrDefault(i => i.BagId == bagId);
            if (existing != null)
            {
                existing.Count -= change;
                if (existing.Count <= 0) inv.Remove(existing);
            }
        }
    }

    // NpcInteractionType sub-types: 0 FocusNpc, 1 Dialog, 2 Option, 3 EndInteraction, 4 ShowSprite, 5 OpenRefineWindow.
    private void ReadNpcInteraction(PacketReader r)
    {
        var sub = r.ReadByte();
        _npc.Seq++;
        switch (sub)
        {
            case 1: // Dialog: name, text, isBig — awaits NpcAdvance
                r.ReadString();
                _npc.DialogText = r.ReadString();
                r.ReadBoolean();
                _npc.Phase = NpcPhase.Dialog;
                break;
            case 2: // Option: count + strings — awaits NpcSelectOption(index)
                _npc.Options.Clear();
                var n = r.ReadInt32();
                for (var i = 0; i < n; i++) _npc.Options.Add(r.ReadString());
                _npc.Phase = NpcPhase.Option;
                break;
            case 3: // EndInteraction
                _npc.Phase = NpcPhase.Ended;
                break;
            case 5: // OpenRefineWindow — awaits NpcRefineSubmit (we just advance to leave it)
                _npc.Phase = NpcPhase.Refine;
                break;
            case 0: // FocusNpc: entityId, isFocus — decorative
                r.ReadInt32();
                r.ReadBoolean();
                break;
            case 4: // ShowSprite: name, pos — decorative
                r.ReadString();
                r.ReadByte();
                break;
            default:
                break;
        }
    }

    // Server->client equip notification: bagId, slot, isEquip. Keep our equipped-slot view in sync.
    private void ReadEquipUpdate(PacketReader r)
    {
        var bagId = r.ReadInt32();
        var slot = r.ReadByte();
        var isEquip = r.ReadBoolean();
        if (slot < World.Self.EquippedBagIds.Length)
            World.Self.EquippedBagIds[slot] = isEquip ? bagId : 0;
    }

    private void ReadOpenShop(PacketReader r)
    {
        var shopType = r.ReadByte();
        _npc.Seq++;
        _npc.ShopItems.Clear();
        if (shopType == 1) // buy-from-NPC: discountLevel, count, (itemId, price)*
        {
            r.ReadByte();
            var count = r.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var id = r.ReadInt32();
                var price = r.ReadInt32();
                _npc.ShopItems.Add((id, price));
            }
            _npc.Phase = NpcPhase.ShopBuy;
        }
        else // sell-to-NPC: overchargeLevel (we sell from our own inventory)
        {
            r.ReadInt32();
            _npc.Phase = NpcPhase.ShopSell;
        }
    }

    private void ReadStartWalk(PacketReader r)
    {
        var id = r.ReadInt32();
        var startCell = r.ReadPosition(); // WalkPath[MoveStep] = the entity's current cell

        // Parse the rest (same layout as the client's LoadMoveData2) to reconstruct the path, so we can
        // estimate where a moving entity actually is between sparse updates for accurate nearest-target (#2).
        Position[]? path = null;
        var cellTime = 0f;
        try
        {
            r.ReadFloat(); r.ReadFloat();   // real world position (floats) — unused
            cellTime = r.ReadFloat();       // seconds per cell
            r.ReadFloat();                  // time to reach the next step — unused
            var totalSteps = r.ReadByte();
            if (totalSteps > 0)
            {
                path = new Position[totalSteps];
                path[0] = startCell;
                var i = 1;
                while (i < totalSteps)
                {
                    var b = r.ReadByte();
                    path[i] = path[i - 1].AddDirectionToPosition((Direction)(b >> 4));
                    i++;
                    if (i < totalSteps)
                    {
                        path[i] = path[i - 1].AddDirectionToPosition((Direction)(b & 0xF));
                        i++;
                    }
                }
            }
        }
        catch { path = null; cellTime = 0f; } // parse hiccup → fall back to just the start cell (no interp)

        if (World.Entities.TryGetValue(id, out var e))
        {
            e.Position = startCell;
            e.State = CharacterState.Moving;
            e.WalkPath = path;
            e.MoveCellTime = cellTime;
            e.WalkStartUtc = DateTime.UtcNow;
        }
    }

    private void ReadAttack(PacketReader r)
    {
        var src = r.ReadInt32();
        var target = r.ReadInt32();   // target
        r.ReadByte();                 // dir
        r.ReadByte();                 // skill
        r.ReadByte();                 // hitCount
        r.ReadByte();                 // result
        var pos = r.ReadPosition();   // attacker's authoritative cell
        // remaining fields (per-hit display dmg, offhand dmg, timings, showMotion) drive visuals only.
        // Do NOT subtract Hp here: Attack carries per-hit DisplayDamage, while the authoritative Hp change
        // arrives in the HitTarget packet (server applies Damage*HitCount+offhand once). Counting both
        // double-decremented the target's tracked Hp, making it look "dead" after 1-2 hits so the bot
        // abandoned a still-alive monster. The real client only updates Hp from HitTarget (OnMessageHit).
        if (target == World.Self.EntityId && World.Entities.TryGetValue(src, out var attacker))
            _lastAttackerName = attacker.Name;
        if (World.Entities.TryGetValue(src, out var atk))
        {
            atk.Position = pos;
            atk.CurrentTargetId = target; // lets a follower mirror its leader's target
        }
        // Record pair-up for HitTarget attribution. Drop the oldest entries when the dict grows past a
        // reasonable bound (the World.Entities turnover already evicts stale entities, but a noisy map can
        // still bloat this map).
        if (target > 0 && src > 0)
        {
            _recentAttacks[target] = (src, DateTime.UtcNow);
            if (_recentAttacks.Count > 256)
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-10);
                foreach (var kv in _recentAttacks)
                    if (kv.Value.At < cutoff) _recentAttacks.TryRemove(kv.Key, out _);
            }
        }
    }

    private void ReadHit(PacketReader r)
    {
        var id = r.ReadInt32();
        var dmg = r.ReadInt32();       // total damage this hit (Damage*HitCount + offhand)
        var pos = r.ReadPosition();
        if (World.Entities.TryGetValue(id, out var e)) e.Position = pos;
        ApplyDamage(id, dmg);

        // Telemetry: damage attribution. Per-hit packets only — we don't have to dedupe.
        if (dmg <= 0) return;
        if (id == World.Self.EntityId)
        {
            // We took a hit. Attacker name is whatever ReadAttack last recorded as targeting us.
            Telemetry.Record(TelemetryEventType.DamageReceived, World.Self.Level,
                string.IsNullOrEmpty(_lastAttackerName) ? "(unknown)" : _lastAttackerName, dmg);
        }
        else if (World.Entities.TryGetValue(id, out var allyView) && allyView.IsPlayer)
        {
            // Another player took a hit. Look up the attacker from the recent-attacks map (populated by
            // ReadAttack). Fire OnAllyHit so the behavior can decide whether to defend — for tanks, the
            // event drives "switch target if a squadmate is being mauled".
            var attackerId = -1;
            if (_recentAttacks.TryGetValue(id, out var rec)
                && (DateTime.UtcNow - rec.At).TotalSeconds < 3.0)
                attackerId = rec.Attacker;
            OnAllyHit?.Invoke(id, attackerId);
        }
        if (World.Entities.TryGetValue(World.Self.EntityId, out var selfE) && selfE.CurrentTargetId == id)
        {
            // Best-effort "we dealt damage": the bot's own CurrentTargetId matches the HitTarget id, so
            // the just-received hit on it is most likely from us.
            Telemetry.Record(TelemetryEventType.DamageDealt, World.Self.Level, e?.Name ?? $"#{id}", dmg);
        }
    }

    private void ReadLevelUp(PacketReader r)
    {
        var id = r.ReadInt32();
        var level = r.ReadByte();
        if (World.Entities.TryGetValue(id, out var e)) e.Level = level;
        if (id == World.Self.EntityId)
        {
            World.Self.Data[PlayerStat.Level] = level;
            Telemetry.Record(TelemetryEventType.LevelUp, level, "", level);
        }
    }

    private void ReadHpRecovery(PacketReader r)
    {
        var id = r.ReadInt32();
        r.ReadInt32();              // amount healed
        var hp = r.ReadInt32();
        var maxHp = r.ReadInt32();
        if (World.Entities.TryGetValue(id, out var e)) { e.Hp = hp; e.MaxHp = maxHp; }
        if (id == World.Self.EntityId) { World.Self.Stats[CharacterStat.Hp] = hp; World.Self.Stats[CharacterStat.MaxHp] = maxHp; }
    }

    private void ReadChangeSp(PacketReader r)
    {
        var sp = r.ReadInt32();
        var maxSp = r.ReadInt32();
        World.Self.Stats[CharacterStat.Sp] = sp;
        World.Self.Stats[CharacterStat.MaxSp] = maxSp;
    }

    private void ReadGainExp(PacketReader r)
    {
        var baseTotal = r.ReadInt32();
        var baseGained = r.ReadInt32();
        r.ReadInt32(); // job total
        r.ReadInt32(); // job gained
        World.Self.BaseExp = baseTotal;
        if (baseGained > 0)
        {
            World.Self.Kills++; // for this bot, exp only comes from kills
            var killed = (DateTime.UtcNow - _lastDeadMonsterAt).TotalSeconds < 3 ? _lastDeadMonsterName : "";
            Telemetry.Record(TelemetryEventType.Kill, World.Self.Level, killed); // no Value → counts as 1 kill
        }
    }

    private void UpdatePosition(int id, Position pos, CharacterState state)
    {
        if (World.Entities.TryGetValue(id, out var e))
        {
            e.Position = pos;
            e.State = state;
        }
    }

    private void SetState(int id, CharacterState state)
    {
        if (World.Entities.TryGetValue(id, out var e)) e.State = state;
    }

    private void ReadSitStand(PacketReader r)
    {
        var id = r.ReadInt32();
        var sitting = r.ReadBoolean();
        SetState(id, sitting ? CharacterState.Sitting : CharacterState.Idle);
    }

    // Say (server → client, multicast). Wire shape: int senderEntityId, string text, string senderName,
    // byte chatType (PlayerChatType: 0 Say, 1 Shout, 2 Party, 3 Notice). Fires OnChatHeard so the behavior
    // layer can drive leader chat-command coordination (!engage, !flee, etc.). Skips echoes of our own
    // outbound Say so a leader doesn't react to its own announcements.
    private void ReadSay(PacketReader r)
    {
        var senderId = r.ReadInt32();
        var text = r.ReadString();
        var senderName = r.ReadString();
        var chatType = r.ReadByte();
        // Drop our own echo. The server multicasts what we sent back to us.
        if (senderId == World.Self.EntityId) return;
        if (!string.IsNullOrEmpty(senderName) && string.Equals(senderName, World.Self.Name, StringComparison.Ordinal))
            return;
        try { OnChatHeard?.Invoke(senderName ?? "", text ?? "", chatType); }
        catch { /* swallow handler exceptions — packet thread must never die */ }
    }

    // ApplyStatusEffect (server → client, multicast to visible players). Wire shape: int targetEntityId,
    // byte statusType, float durationSeconds. We stamp the expected expiry on the entity's Statuses dict
    // so the FSM / DSL can answer "do I have Blessing?" / "am I poisoned right now?".
    private void ReadApplyStatusEffect(PacketReader r)
    {
        var id = r.ReadInt32();
        var statusByte = r.ReadByte();
        var duration = r.ReadFloat();
        var status = (CharacterStatusEffect)statusByte;
        var expires = duration > 0 ? DateTime.UtcNow.AddSeconds(duration) : DateTime.UtcNow.AddMinutes(60);
        if (World.Entities.TryGetValue(id, out var e))
        {
            e.Statuses[status] = expires;
            if (id == World.Self.EntityId)
                Log($"Status applied: {status} ({duration:F1}s).");
        }
    }

    // RemoveStatusEffect (server → client). Wire shape: int targetEntityId, byte statusType, bool isRefresh.
    // The isRefresh flag is the server's "this is the precursor to a re-Apply, don't tell the UI it ended"
    // hint; we still clear because the matching Apply will reinstall the entry one tick later.
    private void ReadRemoveStatusEffect(PacketReader r)
    {
        var id = r.ReadInt32();
        var statusByte = r.ReadByte();
        var isRefresh = r.ReadBoolean();
        var status = (CharacterStatusEffect)statusByte;
        if (World.Entities.TryGetValue(id, out var e) && e.Statuses.Remove(status))
        {
            if (id == World.Self.EntityId && !isRefresh)
                Log($"Status ended: {status}.");
        }
    }

    private void ReadError(PacketReader r)
    {
        var msg = r.ReadString();
        lock (_errLock)
        {
            _errors.Add((DateTime.UtcNow, msg));
            if (_errors.Count > 40) _errors.RemoveRange(0, _errors.Count - 40);
        }
        Log($"server error: {msg}");
    }

    // Server's CommandBuilder.SkillFailed sends PacketType.SkillError with a single byte SkillValidationResult.
    // Without this case a missing-required-item / out-of-range / not-enough-SP cast is silently dropped — the
    // MCP layer sees IsError=False and the SkillScript engine has no idea its rule failed.
    private void ReadSkillError(PacketReader r)
    {
        var res = (SkillValidationResult)r.ReadByte();
        if (res == SkillValidationResult.Success) return; // not actually a failure
        var msg = res switch
        {
            SkillValidationResult.NoLineOfSight        => "Skill failed: no line of sight to target.",
            SkillValidationResult.IncorrectWeapon      => "Skill failed: wrong weapon equipped.",
            SkillValidationResult.IncorrectAmmunition  => "Skill failed: missing or wrong ammunition.",
            SkillValidationResult.InsufficientSp       => "Skill failed: not enough SP.",
            SkillValidationResult.InsufficientItemCount=> "Skill failed: not enough of a required item.",
            SkillValidationResult.InsufficientZeny     => "Skill failed: not enough zeny.",
            SkillValidationResult.MissingRequiredItem  => "Skill failed: missing required item.",
            SkillValidationResult.SkillNotKnown        => "Skill failed: skill not known at that level.",
            SkillValidationResult.TooFarAway           => "Skill failed: target is too far away.",
            SkillValidationResult.TooClose             => "Skill failed: target is too close.",
            SkillValidationResult.MustBeStandingInWater=> "Skill failed: must be standing in water.",
            SkillValidationResult.CannotTeleportHere   => "Skill failed: cannot teleport here.",
            SkillValidationResult.TrapTooClose         => "Skill failed: another trap is too close.",
            SkillValidationResult.CartRequired         => "Skill failed: a cart is required.",
            SkillValidationResult.InvalidTarget        => "Skill failed: invalid target for this skill.",
            SkillValidationResult.CannotTargetSelf     => "Skill failed: cannot target self with this skill.",
            SkillValidationResult.CannotTargetBossMonster => "Skill failed: cannot target boss monsters.",
            SkillValidationResult.OverlappingAreaOfEffect => "Skill failed: another AoE overlaps this one.",
            SkillValidationResult.UnusableWhileHidden  => "Skill failed: cannot use while hidden.",
            SkillValidationResult.MustBeUsedWhileHidden=> "Skill failed: must be hidden to use this.",
            SkillValidationResult.ItemAlreadyStolen    => "Skill failed: item already stolen from that monster.",
            SkillValidationResult.CannotCreateMore     => "Skill failed: cannot create more of these.",
            _ => $"Skill failed: {res}.",
        };
        lock (_errLock)
        {
            _errors.Add((DateTime.UtcNow, msg));
            if (_errors.Count > 40) _errors.RemoveRange(0, _errors.Count - 40);
        }
        Log($"server skill error: {msg}");
    }

    // Server uses PacketType.ServerResult (not ErrorMessage) to report failure of actions like party-invite,
    // so the bot has to decode it explicitly — otherwise WithErrorReadback in the MCP layer sees nothing and
    // the failure looks like a success. Wire shape matches CommandBuilder.SendActionResult: byte kind /
    // int32 id / string text. We record failure variants into the same _errors buffer ErrorMessage uses.
    private void ReadServerResult(PacketReader r)
    {
        var kind = (ServerResult)r.ReadByte();
        r.ReadInt32();         // id — unused
        r.ReadString();        // text — usually empty (server defaults it)
        if (kind == ServerResult.PartyInviteSent) return;
        var msg = kind switch
        {
            ServerResult.InviteFailedSenderNoBasicSkill    => "Party invite failed: you need Basic Mastery level 6.",
            ServerResult.InviteFailedRecipientNoBasicSkill => "Party invite failed: target needs Basic Mastery level 4.",
            ServerResult.InviteFailedAlreadyInParty        => "Party invite failed: target is already in a party.",
            _ => $"ServerResult: {kind}",
        };
        lock (_errLock)
        {
            _errors.Add((DateTime.UtcNow, msg));
            if (_errors.Count > 40) _errors.RemoveRange(0, _errors.Count - 40);
        }
        Log($"server result: {msg}");
    }

    private void ReadPartyInvite(PacketReader r)
    {
        var partyId = r.ReadInt32();
        var partyName = r.ReadString();
        var sender = r.ReadString();
        _pendingInvite = new PendingPartyInvite
        {
            PartyId = partyId,
            PartyName = partyName,
            SenderName = sender,
            AtUtc = DateTime.UtcNow,
        };
        Log($"Party invite from {sender}: '{partyName}' (partyId {partyId}).");
    }

    // Map-wide position broadcast for "important" entities — every active player on this map (plus MVPs and
    // portals). Used so a follower can locate its leader even when they're outside normal view range. Wire
    // layout: short count / (int id, Position pos, byte CharacterDisplayType [, string name if Effect])*.
    // An (X,Y) with either coord < 0 means "removed" (Position.Invalid).
    private void ReadMapImportantEntities(PacketReader r)
    {
        var count = r.ReadInt16();
        for (var i = 0; i < count; i++)
        {
            var id = r.ReadInt32();
            var pos = r.ReadPosition();
            var type = r.ReadByte();
            if (type == 8) r.ReadString(); // CharacterDisplayType.Effect — drain the trailing effect name
            if (id == World.Self.EntityId) continue;
            if (pos.X < 0 || pos.Y < 0) _mapPositions.TryRemove(id, out _);
            else _mapPositions[id] = pos;
        }
    }

    // UpdateParty is server→client too. We only read the leading PartyUpdateType byte for membership tracking;
    // each type's payload differs and we don't need the full breakdown here (per-packet readers are discarded
    // after Dispatch, so leaving trailing bytes unread is safe).
    private void ReadUpdateParty(PacketReader r)
    {
        var type = (PartyUpdateType)r.ReadByte();
        if (type == PartyUpdateType.LeaveParty || type == PartyUpdateType.DisbandParty)
        {
            _inParty = false;
            _isLeader = false;
            _partyLeaderName = "";
            _pendingInvite = null;
            OnPartyStateChanged?.Invoke();
        }
    }

    private void SetDead(int id)
    {
        if (!World.Entities.TryGetValue(id, out var e)) return;
        e.Hp = 0;
        e.State = CharacterState.Dead;
        if (id == World.Self.EntityId)
            Telemetry.Record(TelemetryEventType.Death, World.Self.Level, _lastAttackerName);
        else if (e.IsMonster)
        {
            _lastDeadMonsterName = e.Name;
            _lastDeadMonsterAt = DateTime.UtcNow;
        }
    }

    private void ApplyDamage(int id, int dmg)
    {
        if (dmg <= 0 || !World.Entities.TryGetValue(id, out var e)) return;
        e.Hp = Math.Max(0, e.Hp - dmg);
        if (e.Hp == 0 && e.IsMonster) // killing blow — remember it so the following GainExp can name the kill
        {
            _lastDeadMonsterName = e.Name;
            _lastDeadMonsterAt = DateTime.UtcNow;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conn != null) await _conn.DisposeAsync();
    }

    private readonly record struct CharInfo(string Name, int Slot, string Map);
}
