# RoBotClient MCP tools

All tools live on the dashboard's MCP endpoint: `http://localhost:5080/mcp` (Streamable HTTP transport). They route through the live `BotManager` in the same process, so MCP and the Blazor UI see the same bots.

Conventions:
- `botId` everywhere is the in-memory bot id (`"bot1"`, `"bot2"`, …) returned by `spawn_bot` / shown by `list_bots`. **Not** the character name.
- Item / monster lookups accept an int id, a `CODE` (e.g. `"PORING"`, `"ARROW"`), or an exact name.
- For server-validated mutations (allocate/equip/use_skill/party actions/buy/sell/…) the response includes any server `ErrorMessage` that arrived in the ~300ms after the request. A `SkillError` or `ServerResult` failure (silent at the protocol level) also lands in the bot's `recentErrors` ring buffer — visible via `get_bot`.

---

## Bot inspection

| Tool | What it returns |
|---|---|
| `list_bots()` | summary of every running bot (level, HP/SP, map, mode, target, kills, zeny, weight, etc.). |
| `get_bot(botId, logLines=50)` | snapshot + base/derived stats + inventory (with `bagId` + `equipped` flags) + known skills + the bot's behavior config + **recentErrors** + the tail of the bot's log. `logLines` is capped at 500 (0 omits the log). |
| `get_telemetry(botId, minLevel=0)` | per-bot rollup: kills/deaths/looted/used/levelups, kills+deaths by monster, looted+sold+bought+used items, seconds per map. `minLevel` drops events recorded below that bot level. |
| `get_party(botId)` | `{ inParty, isLeader, leaderName, pendingInvite }`. `leaderName` is empty when this bot **is** the leader. |

## Bot lifecycle

| Tool | What it does |
|---|---|
| `spawn_bot(characterName?, homeMap?, huntCodes?, ignoreCodes?, desiredJob=0, isMale=true, hairStyle=0, hairColor=0)` | Spawn a new bot, **or reconnect to a previously-used character**. If `characterName` matches a record in the account store (see `list_accounts`), the stored account+password are used and `isMale`/`hairStyle`/`hairColor` are ignored. Returns `{ id, message, reconnected }`. |
| `stop_bot(botId)` | Stop and remove a running bot. |
| `list_accounts()` | Every account the client has successfully logged into, with its known characters. Populated each time a bot calls `ConnectAndEnterAsync` and by `discover_accounts`. **New.** |
| `discover_accounts(maxAccount=20)` | Probe `bot_01..bot_NN` with the default password to register characters that already exist on the server. Each probe logs in, lists characters, and disconnects — **no bots are left running**. Returns `{ totalAccounts, accountsResponding }`. **New.** |

## Behavior config

| Tool | What it does |
|---|---|
| `configure_bot(botId, ...many...)` | Live-update a bot's behavior config. Omitted fields are unchanged. Monster lists are comma-separated `CODES`; `healingItemIds`/`lootBlacklist` are comma-separated item ids. Fractional units: `healHpPercent` and `restBelowPercent` are 0..1; `winMargin` is 0..2. Includes `restWhenNoPotions`, `desiredJob`, `verbose`, `autoEscapeWhenStuck`, etc. |

## Stat / skill points

| Tool | What it does |
|---|---|
| `allocate_stats(botId, str, agi, vit, intel, dex, luk)` | Spend stat points. Values are **deltas** to add to each stat. Server validates affordability + the 99 cap. |
| `allocate_skill(botId, skill)` | Spend one skill point on `skill` (name or numeric id). Server validates points/prereqs/max. |
| `list_skills(botId)` | `{ known: [...], granted: [...] }` — the bot's currently usable skills with levels. Use this when writing a skill script so rules only reference what the bot can actually cast. **New.** |
| `ensure_basic_mastery(botId, level=6)` | Spend the bot's unspent skill points into Basic Mastery up to `level`. 6 is the party-inviter threshold; 4 is the invitee threshold. Returns `{ spent, error }`. **New.** |

## Skill casting

| Tool | What it does |
|---|---|
| `use_skill(botId, skill, level=0, target="enemy", targetId=0, x=0, y=0)` | Manually fire a skill. `target` = `"enemy"` (single-target — uses the bot's current target if `targetId=0`; also valid for Ally-target skills with a friendly entity id), `"self"` (buff/self-heal — uses the SHORT-encoded self path automatically), or `"ground"` (uses `x,y`). `level=0` = the bot's max known level. **New.** |
| `set_skill_script(botId, script)` | Replace the per-bot skill rule script (see [SKILL_SCRIPT.md](./SKILL_SCRIPT.md)). Returns `{ rules, errors }` so parse failures are visible. **New.** |
| `get_skill_script(botId)` | Read the bot's current skill script. **New.** |

## Inventory — equip / buy / sell

| Tool | What it does |
|---|---|
| `equip_item(botId, bagId)` | Equip an inventory item by bag id. Server routes by item class — works for weapons, armor, ammunition (arrows go into the ammo slot via the same packet), accessories, etc. |
| `unequip_item(botId, bagId)` | Unequip a slot's item. |
| `equip_item_by_name(botId, item)` | Equip the first matching inventory stack by item id, code, or exact name. Convenience for `"ARROW"`, `"COMPOSITE_BOW"`, etc., when you don't want to look up the bag id first. **New.** |
| `use_item(botId, item, target=-1)` | Use a consumable by id, code, or exact name — `"FLY_WING"` (random same-map teleport), `"BUTTERFLY_WING"` (return to save point), `"RED_POTION"`, etc. Pre-checks that the item is both in inventory AND flagged `Useable` in the DB (sending an unknown id otherwise disconnects the player). `target = -1` (default) = self. **New.** |
| `equip_card(botId, card, gear)` | Socket a card into a slotted piece of inventory gear (both args by id/code/name). Server validates the slot count, the card's EquipPosition matching the gear's, and that there's a free slot; failure surfaces in `recentErrors`. **New.** |
| `buy_item(botId, item, quantity=1)` | Drive a one-shot shop trip to buy a specific item. The bot finds the nearest reachable trader that stocks it, travels there, opens the shop dialog, and buys what it can afford. Pre-checks fail fast if no NPC stocks the item. **New.** |
| `sell_item(botId, item, quantity=0)` | One-shot shop trip to sell a specific item (`quantity=0` = sell every stack). Equipped items are filtered out automatically — won't strip your bow off mid-sale. **New.** |
| `force_sell(botId)` | Default-policy junk-sell trip (uses `ShopPolicy`'s rarity rules). Ignores the auto-shop weight/stack thresholds and shop cooldown. |

## Manual movement

| Tool | What it does |
|---|---|
| `go_to(botId, map, x, y)` | Travel to (`map`, `x`, `y`) and **hold** there until released. Overrides hunting/shopping. |
| `resume_bot(botId)` | Release a `go_to` hold; resume normal autonomous behavior. |

## Parties

When in a party, follower bots automatically defer to the leader (see "Party FSM" below). The MCP layer is for setting them up.

| Tool | What it does |
|---|---|
| `create_party(botId, partyName, inviteEntityId=0)` | Organize a new party (server requires Basic Mastery ≥ 6 — use `ensure_basic_mastery` first if needed). Optional `inviteEntityId` invites someone immediately on success (race-free). The bot is marked as leader. **New.** |
| `invite_to_party(botId, target)` | Invite by entity id (numeric) or character name. Must be the party leader. **New.** |
| `accept_party_invite(botId, partyId=0)` | Accept a pending invite. `partyId=0` uses the most recent pending invite (see `get_party.pendingInvite`). Returns `{ error: "..." }` when nothing is pending — the dashboard surfaces that as a failure. **New.** |
| `leave_party(botId)` | Leave the current party. **New.** |

## Game database

| Tool | What it returns |
|---|---|
| `get_monster(monster)` | Full monster record by id or code — stats, AI string, ScanDist (aggro radius), drops, spawns. |
| `find_monsters(minLevel=0, maxLevel=999, name?, aggressiveOnly=false, limit=40)` | Search monsters by level range / name substring. |
| `get_item(item)` | Item record by id, code, or exact name — weight, prices, slots, item/use type, equip position. |
| `find_traders(itemId)` | NPC traders that sell a given item id, with map + coordinates. Useful for verifying a `buy_item` will work. |

## Simulator

| Tool | What it does |
|---|---|
| `simulate_fight(botId, monster)` | 1v1 melee forecast using the bot's live stats vs the monster DB. Returns CanWin + damage-per-hit / hit chances / seconds-to-kill each way. |
| `simulate_group_fight(botId, monsters, count=1)` | Forecast a whole pack at once. `monsters` is a comma-separated list of ids/codes; `count` repeats the list. Models kill-fastest-first sequencing while everyone alive hits us. Returns `{ pack, packSize, CanWin, TotalSecondsToKill, TotalDamageTaken, MyHp }`. **New.** |

## Build files

| Tool | What it does |
|---|---|
| `read_build(name)` | Read a saved markdown build plan. Returns `{ found, content }`. **New shape** (was a bare string). |
| `write_build(name, markdown)` | Save/overwrite a build plan. `name` is usually the character name. |
| `list_builds()` | Names of all saved build plans. |

---

## Cross-cutting: error visibility

Three classes of server-side rejection used to be silent. The bot now decodes all three into its `_errors` ring buffer, surfaced through:

- The action tool's response when an error lands within ~300 ms of the call (the `WithErrorReadback` window).
- `get_bot.recentErrors`: the last 10 timestamped messages.

| Packet | When | Decoded as |
|---|---|---|
| `PacketType.ErrorMessage` | Item-use rejected, can't be done here, name invalid, character creation errors, party-not-yet-created races, etc. | Verbatim server text. |
| `PacketType.ServerResult` | Party invite rejections: `InviteFailedSenderNoBasicSkill`, `InviteFailedRecipientNoBasicSkill`, `InviteFailedAlreadyInParty`. | Human-readable variant per case. |
| `PacketType.SkillError` | Every `SkillValidationResult` value: no LOS, wrong weapon, **missing/wrong ammunition** (the archer-with-no-arrows case), insufficient SP/zeny/items, target too close/far, trap too close, must be hidden, … | Specific text per enum value. |

If something looks like it should have worked but didn't, check `get_bot(bot, logLines=0).recentErrors` first.

---

## Party FSM (the auto-pilot side)

This isn't an MCP tool — it's behavior. When a bot is in a party but **isn't** the leader, the FSM switches to follower mode:

1. **Lookup the leader by name** (captured from the invite at `accept_party_invite` time) in the bot's local EntityView. If found, cache the entity id.
2. **Target mirroring** — if the leader is currently attacking a monster (tracked from `PacketType.Attack` broadcasts the bot observes), the follower attacks the same monster. Skill rules still apply (so a Priest follower will Heal the leader between mob ticks, etc.).
3. **Stay close** — when the leader has no current target, the follower walks toward them until within ~4 cells.
4. **Off-screen fallback** — if the leader isn't in EntityView, the follower looks the cached id up in the **minimap broadcast** (`PacketType.UpdateMapImportantEntityTracking` — every active player on the same map, refreshed roughly every 4 steps server-side) and walks toward that position. No combat mirroring possible at this range — we just close the gap until the leader is in view again.
5. **Truly lost** (no cached id and no minimap entry, e.g. different map or never seen) — the follower stays idle rather than running off on its own; the next tick may resolve it.

Hazard avoidance, healing, sit-to-rest, and ally-target skill rules (heals on the leader) still run *before* follower mode each tick, so the bot won't passively die while following.

The leader role is sticky for the session: set true by `create_party`, false by `accept_party_invite` / `leave_party` / inbound `LeaveParty` / `DisbandParty`. `get_party` exposes the current state.

---

## Stuck-escape escalation

When `autoEscapeWhenStuck` is enabled on a bot (via `configure_bot`), the FSM watches its stuck timer and reaches for the wings when nothing else works:

- After **`FlyWingStuckSeconds`** (default 8 s) of no progress, if the bag has a Fly Wing (default item id 601), `use_item` it — same-map random teleport, gets us off whatever tile is wedging movement.
- After **`ButterflyWingStuckSeconds`** (default 20 s) of continued no progress, if the bag has a Butterfly Wing (default 602) but no Fly Wing left, use that instead — back to the save point, the bot's normal travel-home logic then routes us back to the hunting map.

Both attempts share a 5-second cooldown so the bot doesn't blow the whole stack on a single bad tile. The item ids and timers are configurable on `BotBehaviorConfig` (`FlyWingItemId`, `ButterflyWingItemId`, `FlyWingStuckSeconds`, `ButterflyWingStuckSeconds`) in case a server uses non-standard ids. Manual escape is always available via `use_item(bot, "FLY_WING")` / `use_item(bot, "BUTTERFLY_WING")`.

---

## Persistence

Three files live next to the project under `Other/RoBotClient/`:

- `accounts.json` — `(account, password, characters)` tuples, populated each time a bot successfully enters the world and by `discover_accounts`. Lets `spawn_bot(characterName=...)` reconnect with stored creds.
- `bot-configs.json` — per-character `BotBehaviorConfig`. `configure_bot` and dashboard Apply save here; `spawn_bot` rehydrates over the form defaults so settings survive a stop+respawn or dashboard restart.
- `bot-party-state.json` — per-character `InParty` / `IsLeader` / `LeaderName`. Saved on Create/Accept/Leave/inbound LeaveParty/DisbandParty, restored on reconnect so MCP `get_party` reflects reality (the server keeps the bot in its party across reconnects but the bot's local flags would otherwise reset).
- `bot-telemetry/<character>.json` — full per-character event log (kills, deaths, loot, items used, items sold/bought, skill casts, map changes, level-ups). Capped at 5000 events per bot. Loaded on each successful connect; flushed every 30 s (only for bots with new events) and once more when the run loop ends.

All three are plaintext JSON next to the app — same trust model as the hardcoded `botbot01` defaults. Add them to `.gitignore` if you don't want them committed.

---

## Diagnostic log entries

For visibility into "why is this bot Idle" the FSM emits a few diagnostic OnLog entries (visible via `get_bot.log`):

- **PickTarget reason** — when target selection returns nothing, a one-line summary fires (deduped): `"PickTarget idle — N mon in view (not-huntable=…, avoided-class=…, blacklisted=…, in-hazard=…, unwinnable=…, group-swarm=…, unreachable=…)."` Re-emits only when the breakdown actually changes.
- **Follower leader state** — `"Follower: leader 'X' in view."` / `"Follower: leader 'X' off-screen — tracking via minimap at (x,y)."` / `"Follower: lost track of leader 'X'."` / after 30 s lost: `"Follower: leader 'X' lost for 30s — falling back to normal hunting."`
- **Map change** — `"Changed maps → <map>."` Resets stuck timers, blacklists, the cached follower leader id, and the idle-reason dedup.
- **Stuck escape** — `"Stuck for Ns — using Fly Wing/Butterfly Wing to escape."` (when `autoEscapeWhenStuck` is on).
- **Death attribution** — `"Died to 'X' (class N) — will avoid it from now on."` Uses the last attacker (not the committed target) so assist-mob pile-ons blacklist the right monster.
- **Skill casts** — `"Skill: <Name> L<lvl> (<target>) on '<entityName>'."` Fired by every script-driven cast (combat + ally support) and now always logged (no longer Verbose-gated). The cast is also written to telemetry as a `SkillCast` event with `Detail="<Name>@<scope>"` (scope = `enemy`/`self`/`ground(x,y)`) and `Value=level`, so `get_telemetry.skillCastsByName` shows per-skill usage counts.

Verbose chat (in-game `Say`) is a separate, simpler announcer keyed on Mode transitions; enable via `configure_bot(verbose=true)`.
