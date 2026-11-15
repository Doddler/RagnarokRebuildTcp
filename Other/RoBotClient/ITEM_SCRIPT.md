# Item-Usage Rule Script

Mirrors the skill rule script (`SKILL_SCRIPT.md`) but for inventory consumables. Tell the bot WHEN to use which item — potion, Fly Wing, Butterfly Wing, status cure, whatever's in its bag — without touching code.

When `ItemScript` is non-empty, it takes priority over the legacy `HealHpPercent` + `AutoEscapeWhenStuck` paths. Empty script = legacy behavior.

## Grammar

One rule per line. Lines starting with `#` or containing `//` are comments.

```
use <itemId> [if <cond>[ and <cond>]*] [every <seconds>]
```

- `<itemId>` — numeric. Use `find_traders` / `get_item` MCP tools to look up names → ids.
- `if <cond>` — zero or more conditions joined with `and` (`&&` also accepted). All must match.
- `every <seconds>` — minimum gap between firings of THIS rule. Defaults to ~1s anti-spam.

## Condition LHS

| LHS | Meaning |
|---|---|
| `hppct` | Self HP as percent (0..100). |
| `sppct` | Self SP as percent (0..100). |
| `stuck` | Seconds since the bot last made progress (moved or damaged target). |
| `enemies` | Count of huntable monsters in view. |
| `dead` | 1 if dead, 0 otherwise. |

## Operators

`<`, `<=`, `>`, `>=`, `==`, `!=`.

## Examples

```
# Heal pattern, cheapest first.
use 501 if hppct < 70                 # Red Potion
use 502 if hppct < 50                 # Orange Potion
use 503 if hppct < 30                 # Yellow Potion
use 504 if hppct < 15                 # White Potion (panic)

# SP top-up for casters; only when below 30%, capped at 2/s.
use 506 if sppct < 30 every 2

# Stuck-escape: Fly Wing first, Butterfly Wing as last resort.
use 601 if stuck >= 8 every 5
use 602 if stuck >= 20 every 30
```

## Setting via MCP

```
configure_bot bot=bot1 itemScript="use 501 if hppct<50\nuse 601 if stuck>=8"
# or dedicated:
set_item_script bot=bot1 script="..."
get_item_script bot=bot1
```

## Persistence

The script is saved per-character (alongside the rest of the bot's config). It survives stop+respawn and dashboard restarts.

## Notes

- The item must exist in inventory **and** be marked Useable in the item DB. Sending an unknown id disconnects the bot — the rule engine validates first and silently skips otherwise.
- Rules are evaluated top-to-bottom each tick; the first matching, item-in-bag, off-cooldown rule fires and the tick returns. No batching: at most one item per tick.
- The classic `HealHpPercent`/`HealingItemIds`/`AutoEscapeWhenStuck` config keeps working when `ItemScript` is empty — the new DSL only takes over when you set it.
