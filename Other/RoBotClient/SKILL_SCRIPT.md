# RoBotClient skill script

A small per-bot scripting language that decides which skill (if any) to cast each combat tick.

## How it runs

When a bot is committed to a target (Mode = Hunting), the script is evaluated BEFORE the basic attack each tick (~400ms cadence). Rules run top-to-bottom; the **first** rule whose conditions all match, whose skill is **known** to the bot, and whose per-skill throttle has elapsed fires that tick. If a rule fires, the basic attack is skipped for that tick. An empty script disables auto-cast.

## Syntax

One rule per line. `#` starts a comment.

```
use <SkillName> <level> [self|enemy|ground] [every <seconds>] [if <cond> [and <cond>]...]
```

- `<SkillName>` — the `CharacterSkill` enum name (e.g. `Bash`, `FireBolt`, `Heal`, `FirstAid`) or its numeric id. Case-insensitive. Use the MCP tool `list_skills` to see what the bot currently knows.
- `<level>` — integer skill level. **0** = use the bot's max known level for that skill.
- target (optional, default `enemy`):
  - `enemy` — single-target. Cast on the bot's current target. The same path is also valid for Ally-target skills with a friendly target id; the server validates ally-vs-enemy from the skill's own target type.
  - `self` — self-buff / self-heal. The wire encoding for self-target skills differs (skill id as a short) — the bot handles that automatically.
  - `ground` — area / placed skill. The bot aims at the current target's cell.
  - `ally` — cast on the most-injured visible party member (the bot must be in a party). The rule's `target.*` conditions refer to that ally. Ally rules are evaluated every tick — even out of combat — so a healer keeps allies topped up between fights. Note: HP info for idle party members may be stale until they take damage or send an HpSp broadcast; for now this works best when allies are actively in combat.
- `every <seconds>` (optional) — per-skill throttle. Default is **1.5** seconds. There is also a global 0.5s throttle across all skills to avoid spam.
- `if` — optional condition block. Conditions are joined by `and`. Each condition is `<lhs> <op> <number>` separated by spaces.

### Conditions

| lhs | meaning |
|---|---|
| `target.hp` | absolute HP of the current target |
| `target.maxhp` | max HP of the target |
| `target.hppct` | HP percent of the target (0–100) |
| `target.dist` | Chebyshev distance from bot to target (cells) |
| `self.hp` | bot's HP |
| `self.maxhp` | bot's max HP |
| `self.hppct` | bot's HP percent (0–100) |
| `self.sp` | bot's SP |
| `self.maxsp` | bot's max SP |
| `self.sppct` | bot's SP percent (0–100) |
| `self.level` | bot's base level |
| `enemies` | count of alive monsters in view |

Operators: `<` `<=` `>` `>=` `==` `!=`. Numbers may have a decimal point (use `.` not `,`).

## Examples

```
# Open with Bash while the target is healthy, finish with basic attack.
use Bash 1 if target.hppct > 30

# Spam Fire Bolt at max known level, but only when we have SP and the target isn't melee-adjacent.
use FireBolt 0 if self.sppct > 20 and target.dist > 1

# Throw a single First Aid (self) when low. Throttle to once every 5s.
use FirstAid 0 self every 5 if self.hppct < 50

# Heaven's Drive (ground AoE) when surrounded by 3+ enemies.
use HeavensDrive 0 ground if enemies >= 3

# Heal the most-injured party member who's dropped below 60% HP, at most every 2s.
use Heal 0 ally every 2 if target.hppct < 60
```

## Setting / inspecting the script

UI: the per-bot editor on the **Controls** page has a "Skill script" textarea. Click **Apply** to validate and save; parse diagnostics (rule count + per-line errors) are shown beneath the textarea.

MCP:
- `set_skill_script(botId, script)` — replaces the script. Returns `{ rules, errors }`.
- `get_skill_script(botId)` — reads the current script.
- `list_skills(botId)` — lists the bot's known + granted skills with levels.
- `use_skill(botId, skill, level, target='enemy', targetId=0, x=0, y=0)` — fires a skill manually right now (useful for testing a rule before committing it to the script).

The script is per-bot and lives on the running config; it survives Apply but not bot respawn (it's not yet persisted to a config file).

## Limits

- One skill per combat tick (a successful cast skips the basic attack for that tick).
- Conditions are pure comparisons; there is no `or`, no parenthesization, no arithmetic. Express alternatives as multiple rules in priority order instead.
- The script only runs when the bot has a committed target. Pre-fight self-buffs while idle aren't supported yet.
- Server-side validations still apply: an out-of-cooldown / unknown / wrong-target-type cast just gets rejected. Parse errors come back from `set_skill_script`; runtime errors (when the server rejects a cast at fire time) land in the bot's recent-errors buffer surfaced by `get_bot`.
