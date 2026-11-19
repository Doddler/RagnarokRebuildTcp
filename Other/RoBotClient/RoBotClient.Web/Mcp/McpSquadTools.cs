using System.ComponentModel;
using ModelContextProtocol.Server;
using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.Manager;

namespace RoBotClient.Web.Mcp;

/// <summary>MCP tools for the squad system. Squads are sub-groups inside (or independent of) a party
/// that share a single follow-target; one party can host multiple squads. Squad leadership is decoupled
/// from party leadership — the party leader is just the server-side invite holder, while the squad
/// leader is what every other squad member's FSM follows. Election order: Swordsman > Thief > Merchant
/// > Archer > Mage > Acolyte > Novice; tiebreakers base-level → max-HP → VIT.</summary>
[McpServerToolType]
public static class McpSquadTools
{
    [McpServerTool(Name = "get_squad"),
     Description("Read this bot's current squad assignment (squadId, isSquadLeader, squadLeaderName).")]
    public static object GetSquad(BotManager bots, string botId)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return new { error = $"No bot '{botId}'." };
        return new
        {
            botId,
            squadId = cfg.SquadId,
            isSquadLeader = cfg.IsSquadLeader,
            squadLeaderName = cfg.SquadLeaderName,
        };
    }

    [McpServerTool(Name = "list_squads"),
     Description("Group every running bot by its SquadId. Returns each squad's members with rank info (jobId, level, maxHp, vit) and which member is the leader. Unsquadded bots appear under squadId='' for visibility.")]
    public static object ListSquads(BotManager bots)
    {
        var groups = new Dictionary<string, List<object>>();
        foreach (var id in bots.AllBotIds())
        {
            var cfg = bots.GetBehaviorConfig(id);
            var cand = bots.GetSquadCandidate(id);
            if (cfg == null || cand == null) continue;
            var entry = new
            {
                botId = id,
                cand.Value.CharacterName,
                jobId = cand.Value.JobId,
                level = cand.Value.Level,
                maxHp = cand.Value.MaxHp,
                vit = cand.Value.Vit,
                inGame = cand.Value.InGame,
                isLeader = cfg.IsSquadLeader,
            };
            var key = cfg.SquadId ?? "";
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<object>();
            list.Add(entry);
        }
        return groups.Select(kv => new { squadId = kv.Key, members = kv.Value }).ToList();
    }

    [McpServerTool(Name = "assign_squad"),
     Description("Assign one bot to a squad with an explicit leader name. The bot becomes a follower of <leaderCharacterName> within squad <squadId>. Pass leaderCharacterName equal to the bot's own character name (or set isSquadLeader=true) to make THIS bot the squad leader. Empty squadId clears the assignment (back to solo / legacy party-leader fallback).")]
    public static string AssignSquad(BotManager bots, string botId, string squadId,
        string? leaderCharacterName = null, bool? isSquadLeader = null)
    {
        var cfg = bots.GetBehaviorConfig(botId);
        if (cfg == null) return $"No bot '{botId}'.";
        cfg.SquadId = squadId ?? "";
        if (isSquadLeader.HasValue) cfg.IsSquadLeader = isSquadLeader.Value;
        if (leaderCharacterName != null) cfg.SquadLeaderName = leaderCharacterName;
        // Sanity: clearing squadId also clears the leader flags so a re-squadded bot doesn't carry stale
        // state.
        if (string.IsNullOrEmpty(cfg.SquadId))
        {
            cfg.IsSquadLeader = false;
            cfg.SquadLeaderName = "";
        }
        bots.SaveConfig(botId);
        return $"Bot {botId} assigned: squadId='{cfg.SquadId}', isLeader={cfg.IsSquadLeader}, leader='{cfg.SquadLeaderName}'.";
    }

    [McpServerTool(Name = "auto_form_squad"),
     Description("Take a comma-separated list of bot ids and assign them to one squad, electing the highest-ranked member as squad leader. Job rank: Swordsman > Thief > Merchant > Archer > Mage > Acolyte > Novice; tiebreaks base-level → max-HP → VIT. Returns the elected leader's bot id + name and the assignment summary. squadId defaults to 'squad-<leader-botId>'.")]
    public static object AutoFormSquad(BotManager bots, string botIds, string? squadId = null)
    {
        var ids = (botIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ids.Length == 0) return new { error = "botIds is empty." };

        var candidates = new List<SquadCandidate>();
        foreach (var id in ids)
        {
            var c = bots.GetSquadCandidate(id);
            if (c == null) return new { error = $"No bot '{id}'." };
            candidates.Add(c.Value);
        }

        var leader = SquadRanking.PickLeader(candidates);
        if (leader == null) return new { error = "No candidates after filtering." };

        var sqid = string.IsNullOrWhiteSpace(squadId) ? $"squad-{leader.Value.BotId}" : squadId;
        var leaderName = leader.Value.CharacterName;
        var assignments = new List<object>();
        foreach (var c in candidates)
        {
            var cfg = bots.GetBehaviorConfig(c.BotId)!;
            cfg.SquadId = sqid;
            cfg.SquadLeaderName = leaderName;
            cfg.IsSquadLeader = c.BotId == leader.Value.BotId;
            bots.SaveConfig(c.BotId);
            assignments.Add(new { c.BotId, c.CharacterName, isLeader = cfg.IsSquadLeader });
        }
        return new
        {
            squadId = sqid,
            leaderBotId = leader.Value.BotId,
            leaderCharacterName = leaderName,
            assignments,
        };
    }

    [McpServerTool(Name = "set_auto_squad"),
     Description("Toggle the dynamic squad auto-former. When enabled (default), every ~8s the server scans each bot's visible-players list and groups bots that can SEE each other into squads, electing leaders by rank (Swordsman > Thief > Merchant > Archer > Mage > Acolyte > Novice). Manual squads (SquadId not starting with 'auto-') are immune. Set runNow=true to trigger an immediate pass and get the resulting stats.")]
    public static object SetAutoSquad(SquadAutoFormer former, bool? enabled = null, bool runNow = false)
    {
        if (enabled.HasValue) former.Enabled = enabled.Value;
        var stats = runNow ? former.ForceRun() : (former.LastSquadsFormed, former.LastBotsAssigned);
        return new
        {
            enabled = former.Enabled,
            intervalSeconds = (int)former.Interval.TotalSeconds,
            lastRunUtc = former.LastRunUtc,
            lastSquadsFormed = stats.Item1,
            lastBotsAssigned = stats.Item2,
        };
    }

    [McpServerTool(Name = "clear_squad"),
     Description("Disband a squad: every bot with squadId == <squadId> has its squad assignment cleared (back to legacy party-leader fallback). Pass squadId='' to clear ALL squads. Note: the auto-former WILL re-cluster the bots on its next pass unless you also set_auto_squad enabled=false.")]
    public static string ClearSquad(BotManager bots, string squadId)
    {
        var n = 0;
        foreach (var id in bots.AllBotIds())
        {
            var cfg = bots.GetBehaviorConfig(id);
            if (cfg == null) continue;
            if (!string.IsNullOrEmpty(squadId) && cfg.SquadId != squadId) continue;
            if (string.IsNullOrEmpty(cfg.SquadId) && !string.IsNullOrEmpty(squadId)) continue;
            cfg.SquadId = "";
            cfg.IsSquadLeader = false;
            cfg.SquadLeaderName = "";
            bots.SaveConfig(id);
            n++;
        }
        return $"Cleared squad assignment from {n} bot(s).";
    }
}
