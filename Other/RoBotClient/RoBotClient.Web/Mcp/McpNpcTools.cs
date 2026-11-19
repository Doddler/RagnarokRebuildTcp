using System.ComponentModel;
using ModelContextProtocol.Server;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.Manager;
using RoBotClient.Bot.State;

namespace RoBotClient.Web.Mcp;

/// <summary>NPC dialog control surface for MCP agents. Lets the agent enumerate visible NPCs, click one,
/// read whichever dialog / option / shop / refine state the server pushes back, pick an option by index,
/// and advance/close. Driving Kafra, Blacksmith, job-changers etc. through this surface means the
/// agent doesn't need any hard-coded NPC knowledge — it can read the menu text and pick.</summary>
[McpServerToolType]
public static class McpNpcTools
{
    /// <summary>Server packets land asynchronously. After sending an action, wait up to <paramref
    /// name="waitMs"/> ms for the NpcDialogState.Seq to bump past <paramref name="sinceSeq"/> — that's the
    /// signal the server's response has arrived and the state we read is the new one, not stale.</summary>
    private static async Task<NpcDialogState> WaitForStateChangeAsync(BotManager bots, string botId, int sinceSeq, int waitMs)
    {
        var session = bots.GetSession(botId);
        if (session == null) return new NpcDialogState();
        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Clamp(waitMs, 0, 10_000));
        while (DateTime.UtcNow < deadline)
        {
            var st = session.SnapshotNpc();
            if (st.Seq != sinceSeq) return st;
            await Task.Delay(50);
        }
        return session.SnapshotNpc();
    }

    /// <summary>Project an NpcDialogState into something the MCP layer can serialize cleanly — with
    /// options pre-indexed so the agent can echo back the integer it wants.</summary>
    private static object ViewState(NpcDialogState st, GameDatabase db)
    {
        var options = new List<object>(st.Options.Count);
        for (var i = 0; i < st.Options.Count; i++)
            options.Add(new { index = i, label = st.Options[i] });

        var shopItems = new List<object>(st.ShopItems.Count);
        for (var i = 0; i < st.ShopItems.Count; i++)
        {
            var (itemId, price) = st.ShopItems[i];
            shopItems.Add(new { index = i, itemId, name = db.ItemName(itemId), price });
        }

        return new
        {
            phase = st.Phase.ToString(),
            dialogText = st.DialogText,
            options,
            shopItems,
            seq = st.Seq,
        };
    }

    [McpServerTool(Name = "list_visible_npcs"),
     Description("List NPCs the bot can currently see, with each one's entity id, name, cell, and distance from the bot. Pass any of these entity ids to npc_click. Useful when the agent wants to drive a Kafra / Blacksmith / job NPC without hard-coding ids — find the closest NPC matching a name pattern and use its id.")]
    public static object ListVisibleNpcs(BotManager bots, string botId)
    {
        var session = bots.GetSession(botId);
        if (session == null) return new { error = $"No bot '{botId}'." };
        return session.WithState(w =>
        {
            var sx = w.SelfPosition.X;
            var sy = w.SelfPosition.Y;
            var list = new List<(int dist, object view)>();
            foreach (var e in w.Entities.Values)
            {
                if (!e.IsNpc) continue;
                var dist = Math.Max(Math.Abs(e.Position.X - sx), Math.Abs(e.Position.Y - sy));
                list.Add((dist, new { entityId = e.Id, name = e.Name, x = e.Position.X, y = e.Position.Y, dist }));
            }
            list.Sort((a, b) => a.dist.CompareTo(b.dist));
            var npcs = new List<object>(list.Count);
            foreach (var (_, v) in list) npcs.Add(v);
            return new { count = npcs.Count, npcs };
        });
    }

    [McpServerTool(Name = "npc_click"),
     Description("Click an NPC to start a dialog. Either supply entityId directly (from list_visible_npcs), OR a namePattern (case-insensitive substring match — the closest visible NPC whose name contains it is picked). Waits up to waitMs for the server's first response so the returned state reflects the actual dialog/option/shop/refine the NPC opened with. Returns { entityId, name, phase, dialogText, options, shopItems, seq }.")]
    public static async Task<object> NpcClick(BotManager bots, GameDatabase db, string botId,
        int? entityId = null, string? namePattern = null, int waitMs = 2000)
    {
        var session = bots.GetSession(botId);
        if (session == null) return new { error = $"No bot '{botId}'." };

        var (resolvedId, resolvedName) = session.WithState(w =>
        {
            if (entityId.HasValue && entityId.Value > 0
                && w.Entities.TryGetValue(entityId.Value, out var e) && e.IsNpc)
                return (e.Id, e.Name);

            if (!string.IsNullOrWhiteSpace(namePattern))
            {
                EntityView? best = null;
                var bestDist = int.MaxValue;
                var sx = w.SelfPosition.X;
                var sy = w.SelfPosition.Y;
                foreach (var ev in w.Entities.Values)
                {
                    if (!ev.IsNpc) continue;
                    if (ev.Name == null) continue;
                    if (ev.Name.IndexOf(namePattern, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var d = Math.Max(Math.Abs(ev.Position.X - sx), Math.Abs(ev.Position.Y - sy));
                    if (d < bestDist) { bestDist = d; best = ev; }
                }
                if (best != null) return (best.Id, best.Name);
            }
            return (0, "");
        });

        if (resolvedId == 0)
            return new { error = "No matching NPC in view. Use list_visible_npcs to see what's available." };

        var prevSeq = session.SnapshotNpc().Seq;
        await session.NpcClickAsync(resolvedId);
        var st = await WaitForStateChangeAsync(bots, botId, prevSeq, waitMs);
        return new { entityId = resolvedId, name = resolvedName, state = ViewState(st, db) };
    }

    [McpServerTool(Name = "npc_get_state"),
     Description("Read the bot's current NPC dialog state without doing anything. Phase=None means no active interaction; Dialog means waiting for npc_advance; Option means waiting for npc_select_option with an index from the returned options[] list; ShopBuy/ShopSell means an OpenShop is showing items; Refine means the refine window is open and waiting for an NpcRefineSubmit. seq lets the agent detect when the server pushes new state.")]
    public static object NpcGetState(BotManager bots, GameDatabase db, string botId)
    {
        var session = bots.GetSession(botId);
        if (session == null) return new { error = $"No bot '{botId}'." };
        return ViewState(session.SnapshotNpc(), db);
    }

    [McpServerTool(Name = "npc_select_option"),
     Description("Pick option <index> from the current NPC menu (Option phase). Indices come from npc_get_state.options[].index. Waits up to waitMs for the next state to arrive. Returns the new state — could be Dialog (next page), Option (sub-menu), ShopBuy/Sell, Refine, or Ended.")]
    public static async Task<object> NpcSelectOption(BotManager bots, GameDatabase db, string botId, int index, int waitMs = 2000)
    {
        var session = bots.GetSession(botId);
        if (session == null) return new { error = $"No bot '{botId}'." };
        var prev = session.SnapshotNpc();
        if (prev.Phase != NpcPhase.Option)
            return new { error = $"Current NPC phase is {prev.Phase}, not Option. Run npc_get_state or npc_advance first." };
        if (index < 0 || index >= prev.Options.Count)
            return new { error = $"Option index {index} out of range. Available indices: 0..{prev.Options.Count - 1}." };

        await session.NpcSelectOptionAsync(index);
        var st = await WaitForStateChangeAsync(bots, botId, prev.Seq, waitMs);
        return new { picked = new { index, label = prev.Options[index] }, state = ViewState(st, db) };
    }

    [McpServerTool(Name = "npc_advance"),
     Description("Advance past a Dialog text panel (the 'next' button). Phase must be Dialog. Waits for the server's next packet so the returned state is the panel AFTER the one you just clicked through.")]
    public static async Task<object> NpcAdvance(BotManager bots, GameDatabase db, string botId, int waitMs = 2000)
    {
        var session = bots.GetSession(botId);
        if (session == null) return new { error = $"No bot '{botId}'." };
        var prev = session.SnapshotNpc();
        await session.NpcAdvanceAsync();
        var st = await WaitForStateChangeAsync(bots, botId, prev.Seq, waitMs);
        return new { state = ViewState(st, db) };
    }

    [McpServerTool(Name = "npc_close"),
     Description("Close the active NPC dialog by sending an NpcAdvance to walk past whatever's showing. Useful when the agent realizes it picked the wrong NPC or the dialog has stalled. After this, npc_get_state should show phase Ended or None.")]
    public static async Task<object> NpcClose(BotManager bots, GameDatabase db, string botId, int waitMs = 500)
    {
        var session = bots.GetSession(botId);
        if (session == null) return new { error = $"No bot '{botId}'." };
        var prev = session.SnapshotNpc();
        // The server accepts an NpcAdvance from any state to terminate; some NPC paths need a couple of
        // advances to fully unwind, but one packet is enough for the dialog handler to release the lock.
        await session.NpcAdvanceAsync();
        var st = await WaitForStateChangeAsync(bots, botId, prev.Seq, waitMs);
        return new { state = ViewState(st, db) };
    }
}
