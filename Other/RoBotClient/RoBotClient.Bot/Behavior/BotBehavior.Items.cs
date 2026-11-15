using System.Globalization;
using RebuildSharedData.Enum;

namespace RoBotClient.Bot.Behavior;

// Item-usage scripting (parallel to BotBehavior.Skills). Lets the MCP agent describe WHEN the bot should
// consume which inventory item via a tiny rule language — replacing the hard-coded HealHpPercent +
// AutoEscapeWhenStuck plumbing with something the agent can rewrite per situation.
//
// Grammar (one rule per line, # / // comments stripped):
//   use <itemId> [if <cond>[ and <cond>]*] [every <seconds>]
// LHS supported:
//   hppct      // self HP as % (0..100)
//   sppct      // self SP as %
//   stuck      // seconds since last "progress" (server is rejecting WalkTo, or no movement)
//   enemies    // count of huntable monsters within sight
//   dead       // 1 if dead, 0 otherwise
// Ops: < <= > >= == !=
// Examples:
//   use 501 if hppct < 50              # red potion under 50% HP
//   use 506 if sppct < 30 every 2      # green potion for SP, 2s cooldown
//   use 601 if stuck >= 8              # Fly Wing when stuck 8s+
//   use 602 if stuck >= 20             # Butterfly Wing as last resort
//
// First matching rule (top-to-bottom) with the item in inventory and off-cooldown wins. Unparseable
// lines are skipped — diagnostics are exposed via LastItemScriptDiagnostics.
public sealed partial class BotBehavior
{
    public readonly record struct ItemCond(string Lhs, string Op, double Value);

    public sealed record ItemRule(int ItemId, double EveryCooldown, IReadOnlyList<ItemCond> Conditions);

    private string? _itemScriptSource;
    private List<ItemRule> _itemRules = new();
    private string _itemScriptDiagnostics = "";
    private readonly Dictionary<int, DateTime> _itemRuleCooldown = new();

    public string LastItemScriptDiagnostics => _itemScriptDiagnostics;

    private void ReloadItemScriptIfChanged()
    {
        if (ReferenceEquals(_itemScriptSource, _config.ItemScript)) return;
        _itemScriptSource = _config.ItemScript;
        _itemRules = ParseItemScript(_config.ItemScript ?? "", out _itemScriptDiagnostics);
        _itemRuleCooldown.Clear();
    }

    private static List<ItemRule> ParseItemScript(string src, out string diagnostics)
    {
        var rules = new List<ItemRule>();
        var diag = new List<string>();
        var lineNo = 0;
        foreach (var raw in (src ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            lineNo++;
            var line = raw.Trim();
            // Strip line comments — both `#` (shell-style) and `//` (C-style) supported for muscle-memory.
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash].Trim();
            var slash = line.IndexOf("//", StringComparison.Ordinal);
            if (slash >= 0) line = line[..slash].Trim();
            if (line.Length == 0) continue;

            // Expected: use <itemId> [if ...] [every N]
            if (!line.StartsWith("use ", StringComparison.OrdinalIgnoreCase))
            {
                diag.Add($"L{lineNo}: expected 'use <itemId> ...', got: {raw}");
                continue;
            }
            var rest = line[4..].Trim();
            var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || !int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId))
            {
                diag.Add($"L{lineNo}: missing or invalid itemId in: {raw}");
                continue;
            }

            // Pull `every N` off the tail if present (any position works, but typical at end).
            var every = 0.0;
            var idx = Array.FindIndex(tokens, t => t.Equals("every", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && idx + 1 < tokens.Length
                && double.TryParse(tokens[idx + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ev))
                every = ev;

            // Conditions: everything after `if` up to (but excluding) `every`.
            var conds = new List<ItemCond>();
            var ifIdx = Array.FindIndex(tokens, t => t.Equals("if", StringComparison.OrdinalIgnoreCase));
            if (ifIdx >= 0)
            {
                var condEnd = idx >= 0 ? idx : tokens.Length;
                if (condEnd > ifIdx + 1)
                {
                    var condStr = string.Join(' ', tokens[(ifIdx + 1)..condEnd]);
                    foreach (var part in condStr.Split(new[] { " and ", " AND ", " && " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (TryParseCond(part.Trim(), out var c)) conds.Add(c);
                        else diag.Add($"L{lineNo}: cannot parse condition: {part}");
                    }
                }
            }

            rules.Add(new ItemRule(itemId, every, conds));
        }
        diagnostics = string.Join('\n', diag);
        return rules;
    }

    private static bool TryParseCond(string s, out ItemCond cond)
    {
        cond = default;
        // Two-char ops first (>=, <=, ==, !=), then single-char.
        foreach (var op in new[] { ">=", "<=", "==", "!=", ">", "<" })
        {
            var i = s.IndexOf(op, StringComparison.Ordinal);
            if (i <= 0) continue;
            var lhs = s[..i].Trim().ToLowerInvariant();
            var rhs = s[(i + op.Length)..].Trim();
            if (!double.TryParse(rhs, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return false;
            cond = new ItemCond(lhs, op, v);
            return true;
        }
        return false;
    }

    private double ItemLhsValue(Snapshot snap, string lhs)
    {
        // self.status.<name> works in the item DSL too — "use 506 if self.status.poison == 1" for
        // Green Potion when poisoned, "use Blessing scroll if self.status.blessing == 0", etc.
        if (lhs.StartsWith("self.status.", StringComparison.OrdinalIgnoreCase))
        {
            var statusName = lhs["self.status.".Length..];
            return Enum.TryParse<CharacterStatusEffect>(statusName, true, out var s) && snap.SelfStatuses.Contains(s) ? 1 : 0;
        }
        return lhs switch
        {
            "hppct" => snap.SelfMaxHp > 0 ? 100.0 * snap.SelfHp / snap.SelfMaxHp : 100,
            "sppct" => snap.SelfMaxSp > 0 ? 100.0 * snap.SelfSp / snap.SelfMaxSp : 100,
            "stuck" => (DateTime.UtcNow - _lastProgressAt).TotalSeconds,
            "enemies" => snap.Monsters.Count(m => IsHuntable(m)),
            "dead" => snap.SelfDead ? 1 : 0,
            // Common debuff/buff convenience flags (same names as the skill DSL).
            "poisoned"     => snap.SelfStatuses.Contains(CharacterStatusEffect.Poison) ? 1 : 0,
            "cursed"       => snap.SelfStatuses.Contains(CharacterStatusEffect.Curse) ? 1 : 0,
            "silenced"     => snap.SelfStatuses.Contains(CharacterStatusEffect.Silence) ? 1 : 0,
            "blinded"      => snap.SelfStatuses.Contains(CharacterStatusEffect.Blind) ? 1 : 0,
            "stunned"      => (snap.SelfStatuses.Contains(CharacterStatusEffect.Stun)
                                || snap.SelfStatuses.Contains(CharacterStatusEffect.Sleep)
                                || snap.SelfStatuses.Contains(CharacterStatusEffect.Frozen)
                                || snap.SelfStatuses.Contains(CharacterStatusEffect.Stone)) ? 1 : 0,
            "has_blessing" => snap.SelfStatuses.Contains(CharacterStatusEffect.Blessing) ? 1 : 0,
            "has_agiup"    => snap.SelfStatuses.Contains(CharacterStatusEffect.IncreaseAgi) ? 1 : 0,
            _ => 0,
        };
    }

    private static bool EvalOp(double left, string op, double right) => op switch
    {
        "<"  => left <  right,
        "<=" => left <= right,
        ">"  => left >  right,
        ">=" => left >= right,
        "==" => Math.Abs(left - right) < 0.0001,
        "!=" => Math.Abs(left - right) >= 0.0001,
        _    => false,
    };

    /// <summary>Try one item-script rule. Returns true (and consumes the cooldown) when an item was sent.
    /// When <see cref="BotBehaviorConfig.ItemScript"/> is empty this never fires and the legacy
    /// MaybeHealAsync path is responsible for potions.</summary>
    private async Task<bool> TickItemRulesAsync(Snapshot snap, CancellationToken ct)
    {
        ReloadItemScriptIfChanged();
        if (_itemRules.Count == 0) return false;

        foreach (var rule in _itemRules)
        {
            if (_itemRuleCooldown.TryGetValue(rule.ItemId, out var until) && DateTime.UtcNow < until) continue;

            var allMatch = true;
            foreach (var c in rule.Conditions)
                if (!EvalOp(ItemLhsValue(snap, c.Lhs), c.Op, c.Value)) { allMatch = false; break; }
            if (!allMatch) continue;

            // Item must be in inventory + useable (sending an unknown id disconnects the player).
            var ok = _bot.WithState(w =>
            {
                if (_data != null && !_data.IsUsableItem(rule.ItemId)) return false;
                foreach (var it in w.Self.Inventory)
                    if (it.ItemId == rule.ItemId && it.Count > 0) return true;
                return false;
            });
            if (!ok) continue;

            if (rule.EveryCooldown > 0)
                _itemRuleCooldown[rule.ItemId] = DateTime.UtcNow.AddSeconds(rule.EveryCooldown);
            else
                _itemRuleCooldown[rule.ItemId] = DateTime.UtcNow.AddSeconds(1.0); // soft anti-spam

            OnLog?.Invoke($"Item rule fired: use {rule.ItemId} ({_data?.ItemName(rule.ItemId) ?? "?"}).");
            await _bot.UseInventoryItemAsync(rule.ItemId, -1, ct);
            return true;
        }
        return false;
    }
}
