# RoBotClient — Known Issues / Findings

Running log of bugs and inefficiencies found during live testing, with root-cause analysis and the
proposed fix, so they can be addressed later. Newest first. Status: **OPEN** until fixed.

---

## #8 — Group / assist-aware avoidance: simulate the fight against the whole pack, not 1v1

**Status:** OPEN — feature/safety, requested 2026-05-27.

**Request:** some monsters "assist" — they turn aggressive when they see one of their own kind being attacked —
so a lone target can pull a deadly swarm depending on numbers. Before engaging, the simulator should forecast
the fight against **all** the monsters that would join, not just 1v1, when several of that kind are on screen.

**Mechanic (verified — `Monster.Ai.cs:267`):** `InAllyInCombat()` → `CanAssistAlly(10, …)` — a monster scans for
**same-type** allies that are in combat within **10 tiles** and joins them. Assisting AI types are `AiAssist`,
`AiAggressiveAssist`, `AiLooterAssist` (from the AI mapping); aggressive types also pile on via their own aggro
range. Because each new assister re-scans, the pull can **chain** across a same-`ClassId` cluster.

**Current gap:** `BattleSimulator.Forecast` and `PickTarget` evaluate **1v1 only** (`BotBehavior.cs:240`), and the
Phase-D hazard system only avoids monsters that are *already* aggressive-on-sight — not a passive pack that
becomes hostile once provoked. So the bot will happily attack one assist monster sitting in a group and get mobbed.

**Proposed change (M–L — builds on #5's accurate simulator):**
1. Flag **assist** monsters from the AI string (`IsAssist` = `Ai` contains `"Assist"`).
2. In `PickTarget`, build the candidate's **engagement group**: the target + (if it's an assist type) the
   same-`ClassId` monsters in view within the (chained) ~10-tile assist radius, plus any already-aggressive
   monsters within their aggro range — conservatively, all same-kind assisters currently on screen.
3. Add `BattleSimulator.ForecastGroup(me, monsters[])` — model the bot taking the **summed** incoming DPS from
   the whole group while clearing them sequentially (time-to-kill-all vs. time-to-die). Engage only if the
   *group* fight is winnable; otherwise skip and steer wander around the pack.
4. MCP: a `simulate_group_fight` tool (or an `includeNearbyAssist` flag on `simulate_fight`) so an agent sees the
   pack forecast, not just the single-target one.

## #7 — Ally support: use buff & healing skills on party members (depends on #6 + #5)

**Status:** OPEN — feature, requested 2026-05-27.

**Request:** improve the bot AI so it can cast **buff and healing skills on allies** (party members), not just
fight.

**Protocol:** same `PacketType.Skill` as #5, but with **`SkillTarget.Ally`** (verified — the `SkillTarget` enum
is `Passive, Enemy, Ally, Any, Ground, Self, Trap`). Cast an ally-target skill on a party member's entity id.
`skillinfo.json` gives each skill's `Target`, so heal/buff skills (Ally-targeted, supportive) are identifiable.

**Proposed change (M–L — builds on #6 parties + #5 skills):**
1. **Ally awareness:** from the party-member list (#6), match nearby ally **entities** in view (by id/name) and
   track their HP%.
2. **Support behavior:** a "support" role/mode — follow the party, heal the lowest-HP ally below a threshold,
   and keep buffs up; or fold ally conditions into the #5 skill-script DSL, e.g.
   `if ally.hp% < 50: use Heal on lowest-hp ally`, `if ally.missingBuff(Blessing): use Blessing on ally`.
3. **Skill classification:** from `skillinfo`, flag the bot's learned skills that are heals/buffs (Target=Ally),
   with SP cost + cast time so the support logic and simulator can reason about them.
4. **MCP/UI:** a support toggle + ally rules (extends the skill-script tools/editor from #5).

## #6 — Parties: form / invite / join / leave (user + MCP)

**Status:** OPEN — feature, requested 2026-05-27.

**Request:** let the user and an agent (MCP) make bots **form**, **invite/join**, and **leave** parties.

**Protocol (verified — `NetworkManager`):**
- **Form:** `CreateParty` — `string partyName, int entityId` (own entity id, or -1).
- **Invite:** `InvitePartyMember` — `byte mode` then either `int id` (mode 0) or `string name` (mode 1).
- **Accept/join:** `AcceptPartyInvite` — `int partyId`.
- **Leave:** `UpdateParty` — `byte PartyClientAction.LeaveParty`. (Manage/kick: `UpdateParty` — `byte action, int id`.)
- **State in (S→C):** `UpdateParty` / `NotifyPlayerPartyChange` carry membership; the bot must parse the incoming
  invite notification to learn the `partyId` it needs to accept.

**Proposed change (M):**
1. `BotSession`: parse the inbound party packets (invite → store pending invite's partyId; `UpdateParty`/
   `NotifyPlayerPartyChange` → maintain a party-member list). Add actions: `CreatePartyAsync(name)`,
   `InvitePartyAsync(idOrName)`, `AcceptPartyInviteAsync(partyId)`, `LeavePartyAsync()`.
2. Expose party name + members (ids/names) on the bot state/snapshot.
3. **MCP tools:** `create_party`, `invite_to_party` (by id or name), `accept_party_invite` (or an `auto_accept`
   toggle), `leave_party`; include party info in `get_bot`.
4. **UI:** party controls on the bot detail page (create / invite by name / accept pending / leave) + member list.
5. Prerequisite for #7, and enables exp-share (the prt_fild08 Bard notes exp is party-shared).

## #5 — Skill usage: manual trigger + a per-bot "skill script" ruleset (+ accurate, skill-aware simulator)

**Status:** OPEN — feature, requested 2026-05-27.

**Request:** let the user and an agent (MCP) drive skills two ways:
- **Manual trigger** — cast a named skill now (on the current target / self / ground), from the UI and MCP.
- **Rule-based auto-cast** — a user/MCP-writable ruleset, e.g. `if target.hp < 100: use Bash 5`, evaluated each
  combat tick. **Saved per bot.** Implemented as a small scripting language ("skill script") for now, editable
  from both the UI and MCP.
Also: make the **battle simulator accurate / skill-aware** (it currently approximates melee only — see the
`BattleSimulator` element/size/crit TODOs) and surface that through MCP so an agent can forecast skill builds.

**Protocol (already mapped):** skill use is `PacketType.Skill` with a `SkillTarget`:
`SendSingleTargetSkillAction` = `byte Skill, byte SkillTarget.Enemy, int targetId, byte skill, byte lvl`;
`SendSelfTargetSkillAction` = `byte Skill, byte SkillTarget.Self, short skill, byte lvl` *(note: self uses a
`short` skill id)*; `SendGroundTargetSkillAction` = `byte Skill, byte SkillTarget.Ground, Vector2Int target,
byte skill, byte lvl` (`NetworkManager.cs`). The bot already learns skills via `ApplySkillPoint`; it has **no
use-skill action yet**.

**Proposed change (M–L):**
1. `BotSession`: add `UseSkillOnTargetAsync(skill, level, targetId)`, `UseSkillSelfAsync(skill, level)`,
   `UseSkillGroundAsync(skill, level, x, y)` (match the byte-vs-short skill-id quirk above).
2. **Skill-script engine:** a small DSL evaluated each combat tick — conditions on `target.hp`/`target.hpPct`/
   `self.hp%`/`self.sp%`/`self.sp`/distance → an action (`use <Skill> <level>` self/target). First matching rule
   wins; skip if the skill isn't known / not enough SP / on cooldown. **Persist per bot** (alongside the build
   file or a sibling store).
3. **Manual trigger:** MCP `use_skill(botId, skill, level, target?)` + a UI button (defaulting target = current).
4. **Rules editing:** MCP `set_skill_script(botId, text)` / `get_skill_script(botId)` + a UI textarea.
5. **Accurate simulator:** extend `BattleSimulator` to model the bot's skills (and the element/size/crit TODOs)
   so `simulate_fight` reflects skill damage; optionally a `simulate_skill` variant.
6. **TODO (explicit, per request):** write a `.md` documenting the skill-script language — grammar, available
   condition variables, skill-name/level syntax, evaluation order, cooldown/SP handling, and examples.

## #4 — Rest / sit to recover HP & SP when no recovery items are available

**Status:** OPEN — feature, requested 2026-05-27.

**Request:** let the user and MCP enable "rest when out of recovery items" — the bot **sits** (HP/SP regen is
faster while sitting) when it's low and holds no usable healing item, then stands and resumes once recovered.

**Protocol (verified):** sit/stand is `PacketType.SitStand` + `bool isSitting` (`NetworkManager.ChangePlayerSitStand`).
**Novice caveat (verified — `PacketSitStand.cs`):** a Novice (`JobId == 0`) must have **Basic Mastery ≥ 2** to
sit, else the server replies `"You need level 2 of basic mastery to sit."` Other jobs sit freely.

**Proposed change (S–M):**
1. `BotSession`: add `SitAsync(bool sitting)` (`SitStand` + bool) and track our sitting state from the inbound
   `SitStand` broadcast.
2. `BotBehaviorConfig`: a `RestWhenNoPotions` flag + a rest-until HP%/SP% threshold.
3. Behavior: when HP%/SP% is low **and** `FindHealItem()` returns nothing **and** sitting is allowed (non-Novice,
   or Novice with Basic Mastery ≥ 2), sit; stand and resume once HP%/SP% recovers past the threshold, or
   immediately if a hazard/target needs action.
4. As a Novice with resting enabled, optionally auto-learn Basic Mastery to lvl 2 first (reuses the
   `ApplySkillPoint` path) so sitting is possible.
5. Expose the toggle + thresholds in the spawn/per-bot UI and MCP `configure_bot`.

---

## #3 — Character appearance (gender / hairstyle / hair color) not configurable at creation

**Status:** OPEN — enhancement, requested 2026-05-27.

**Request:** let the user (spawn UI) and an agent (MCP `spawn_bot`) choose a new bot's **gender**, **hairstyle**,
and **hair color** when its character is created.

**Current behavior:** `BotSession.ConnectAndEnterAsync` hardcodes the appearance in the create packet —
`w.Write(0)` for head (hairstyle) and `w.Write(0)` for hair (color); only `IsMale` comes from config. Every
created bot ends up with hairstyle 0 / color 0.

**Protocol (verified — `PacketEnterServer.cs`):** the EnterServer *create* packet is
`bool isNewCharacter, string name, int head, int hair, byte slot, byte[6] stats, bool isMale`. The server maps
`head → PlayerStat.Head` (hairstyle), `hair → PlayerStat.HairId` (hair color), `isMale → PlayerStat.Gender`
(0 = male, 1 = female); slot must be 0–2.

**Proposed change (S):**
1. `BotConfig`: add `int HairStyle` and `int HairColor` (keep existing `IsMale`), default 0/0.
2. `BotSession` create branch: write `_config.HairStyle` / `_config.HairColor` instead of the hardcoded `0`s.
3. `BotManager.SpawnBot`: accept optional `isMale` / `hairStyle` / `hairColor` and set them on the new `BotConfig`.
4. Spawn UI (`Controls.razor`): add a gender select + hairstyle + hair-color inputs to the spawn form.
5. MCP `spawn_bot`: add `gender` (or `isMale`) / `hairStyle` / `hairColor` params passed through to `SpawnBot`.
   Valid id ranges follow the client's sprite/palette tables — clamp/validate or let the server reject.

> Applies only at first creation; it does not re-style an already-created character. (The prt_fild08 Bard's
> "Change appearance" dialog can randomize gender/hair on an existing char, but the bot doesn't drive that yet.)

---

## #2 — Bot doesn't reliably target the *closest* monster (looks random / inefficient)

**Status:** OPEN
**Found:** live test, 2026-05-27. Observed the bot walking to a farther monster while a closer one was available.

**Symptom:** the bot frequently engages a monster that isn't the obviously-nearest one, appearing to pick "randomly" and wasting travel time.

**Diagnosis:** `BotBehavior.PickTarget` is *not* random — it builds the winnable/safe candidate list, sorts by `score` (`Dist` Chebyshev distance + grey-mob penalty), and returns the first **reachable** one (`BotBehavior.cs:240`). So it is *designed* to take the nearest reachable target. It deviates from "visually closest" for four reasons, in likely order of impact:

1. **Stale positions for moving monsters (primary).** The bot records a monster's position only at walk *start*/*stop*: `BotSession.ReadStartWalk` reads just the start cell and never interpolates the walk (`var startCell = r.ReadPosition(); UpdatePosition(id, startCell, Moving)`). A roaming Poring/Lunatic's tracked position therefore lags by up to its full walk distance, so the "nearest by tracked position" is often *not* the actually-nearest monster.
2. **Grey-level penalty.** `score += 100` when `monsterLevel <= selfLevel - 15` (`BotBehavior.cs:254`). At higher bot levels this deprioritizes nearby low-level monsters in favor of a farther appropriate-level one — intentional, but reads as "ignoring the closest."
3. **Commit-to-target.** Once engaged, the bot stays on that target until it dies/leaves (anti-mobbing fix). It won't switch to a monster that becomes closer mid-fight.
4. **A\* reachability filter.** `IsReachable` (`BotBehavior.cs:272`) can return false for a *near* monster whose tracked tile resolves as non-walkable (stale-into-wall) or whose path exceeds the 6000-node A\* cap on a long detour, causing it to skip the near one for a farther reachable one.

**Proposed fix:**
- Primary: **interpolate moving-monster positions** — parse the walk destination from the StartWalk packet (the bot currently drops it) and estimate the monster's current cell from elapsed time × move speed, so the nearest computation uses live positions.
- Reconsider the grey penalty magnitude (or make it a soft tiebreak rather than +100 tiles).
- Optionally allow re-targeting if another huntable monster is *much* closer than the committed one (without reintroducing target thrash).

---

## #1 — Futile restock trips to a trader that doesn't stock recovery items

**Status:** OPEN
**Found:** live test, 2026-05-27 (`[BOT] Filcher`). Repeated `AutoShop: heading to ... to restock at 'Flower Girl'` every ~60–90s, each completing in ~20s with no purchase.

**Symptom:** with AutoShop on, the bot endlessly trips to the nearest trader (the **Flower Girl** on prt_fild08) to "restock," never buys anything, and loops. The trips also drag it next to the camp **Target Dummy**, producing repeated `Avoiding 'Target Dummy'` flees.

**Diagnosis:**
- `ResolveShop` (`BotBehavior.Shopping.cs:81`) picks the **nearest trader by warp-hops**, only tie-breaking toward a restock-stocking trader *at equal hops*. The Flower Girl is 0 hops (same map) and always wins; the real Red-Potion (501) sellers are Tool Dealers 1+ hops away (towns), so they can never beat her.
- The Flower Girl is a trader but stocks flowers, not potions: `npcdatabase.json` → `"Flower Girl","Map":"prt_fild08","IsTrader":true,"SellsItems":[712,2207]` (no 501).
- `DoBuyAsync` looks up the price of 501 in her shop list; it isn't there, so `price == 0` → it closes without buying.
- The restock trigger is `restockHeld < RestockBelow` (a fresh Novice has 0 Red Potions < 10), which stays true because the purchase never happens. `EndShopTrip` sets a 60s cooldown → the trigger refires → **infinite loop of futile restock trips**.

**Proposed fix:**
- For a **buy/restock** goal, choose the nearest trader that **actually stocks `RestockItemId`** (`SellsItems.Contains`), not just any nearest trader.
- If **no reachable trader stocks it**, don't enqueue the restock (and back off with a long cooldown / one-time log) so it can't loop.
- Optionally split goals: **sell** at the nearest trader, **buy** at the nearest restock-stocker (often different NPCs).
- Bonus: avoid choosing a shop NPC that sits inside a known hazard's aggro zone (the Flower Girl is beside the Target Dummy).

---

# MCP server — improvement backlog

From a 4-angle review (tool-surface design, engine/exposure gaps, live read-only dogfooding of the running
server, and intent/roadmap reconciliation), 2026-05-27. The core is genuinely solid and **not** a rewrite:
verb-based tool names, consistent `botN` ids, structured `{error:"..."}` results with no stack traces,
`find_monsters` already paginated, `simulate_fight` is a standout (side-effect-free access to the server's own
damage math), engine thread-safety is correct (consistent `_stateLock`, concurrent collections, send semaphore,
no blocking `.Result`/`.Wait()` — the async-deadlock worry does **not** apply), and all 10 read-only tools were
driven correctly first try. The items below are one real bug plus cheap, high-value ergonomics.

## MCP-P0 — Mutations report false success (reliability bug) — VERIFIED

**Status:** OPEN — highest value.
Every action tool (`allocate_stats`, `allocate_skill`, `equip_item`, `go_to`, `force_sell`) returns an
optimistic `"Requested …"` string the instant it queues the packet. Server rejections arrive as
`PacketType.ErrorMessage`, which is **silently dropped once in-game**: `BotSession.DispatchBody` has no
`case PacketType.ErrorMessage` (falls to `default: break`); it's handled only in the pre-enter login loop
(`BotSession.cs:123`). Verified by grep — the only post-enter mention is a comment at `:373`.

**Net effect:** an agent that equips a too-high-level item, allocates an unaffordable stat, or learns a skill it
lacks prereqs for gets a success-shaped reply and **never sees an error**. A designed-in blind spot for any
autonomous driver.

**Fix (S–M):** (1) add `case PacketType.ErrorMessage` to `DispatchBody`, pushing the text onto a short per-bot
ring buffer / telemetry; (2) have action tools read back state after a brief `await` and return the actual
outcome (skill level rose? bagId now in `EquippedBagIds`?), or at minimum the latest server error line.

## MCP-P1 — Cheap, high-leverage agent ergonomics

**Status:** OPEN. The annotations+instructions item is the biggest bang after P0.
- **No tool annotations or server instructions.** None of the 20 tools set `readOnlyHint`/`destructiveHint`/
  `idempotentHint`, and `AddMcpServer()` sets no server instructions — an agent can't tell `get_monster` from
  `stop_bot`/`force_sell` and gets 20 flat tools with no workflow orientation. SDK 1.3.0 supports both. (S)
- **Fractional params have no stated units.** `healHpPercent`/`winMargin` are fractions in code but the
  descriptions don't say so; an agent passing `50` for "50%" sets 5000%. Document **and** clamp. (S)
- **No payload-trim knobs.** `get_bot` dumps an unbounded log (dogfood saw 37 lines), `get_monster` always
  includes the full spawns list (~23 maps), `find_traders` is unbounded. Add `logLines`/`fields`/`limit`. (S)
- **Enum discoverability.** `allocate_skill` needs a `CharacterSkill` name with no way to enumerate valid
  skills — add a `list_skills` tool (mirror of `find_monsters`). Monster ids are non-standard (Poring is 4000,
  not the lore 1002) — add an id hint to the not-found error. (S)
- **Consistency nits (dogfood-found).** `get_telemetry` `minLevel` zeroes the event counters but still returns
  full `secondsPerMap`; `read_build` returns `""` for not-found, indistinguishable from an empty build —
  return `{found:false}` like the other lookups. (S)
- **Doc/security hygiene.** `Program.cs` comment + PLAN.md say port 5080 but `launchSettings.json` binds 5299;
  the "Localhost only" comment isn't enforced (`AllowedHosts:"*"`, no auth) on an endpoint that can
  spawn/stop/relocate bots. Worth a deliberate note even for a dev tool. (S)

## MCP-P2 — Capability & build-workflow gaps (need a scope decision)

**Status:** OPEN — needs product direction.
- **Thin/asymmetric control surface.** The engine has ~17 clean primitives (`WalkToAsync`, `AttackAsync`,
  `UseInventoryItemAsync`, `PickUpAsync`, `SayAsync`, the NPC-dialog trio, `StopAsync`) but only ~7 are exposed,
  and there's no read access to what the bot *sees* (entities, ground items, NPC dialog are never projected —
  `get_bot` is self-only). **Decision:** is the product "configure the autopilot and supervise" (current shape
  — then this is fine) or "let an agent directly puppeteer a bot"? If the latter, add `get_surroundings` + thin
  action wrappers + NPC-dialog tools (each ~3 lines, mirroring `equip_item`).
- **Build workflow is inert.** `write_build`/`read_build` are free-form `.md` CRUD with no schema and **no code
  path that applies a build to a bot**. Builds are keyed by character name while bots are keyed by `botId`, with
  no resolver (dogfood saw a saved `[BOT] BotZero` build for a bot that wasn't running). PLAN.md scopes builds
  as agent resume-memory, so `apply_build`/`spawn_from_build` is genuinely new scope — but the name↔botId
  disconnect and the not-found ambiguity are worth closing regardless. (M)
- **`configure_bot` covers ~half of `BotBehaviorConfig`.** An agent can enable `autoShop` but can't set what to
  restock, or the sell/flee/stuck thresholds. Each missing field is one more `if (x.HasValue)` line. (S–M)

## MCP-P3 — Testing

**Status:** OPEN. Only the protocol codec + login handshake are unit-tested. The entire MCP surface, `BuildStore`
(name sanitization), `ShopPolicy.ShouldSell`, `BattleSimulator`, and `BotManager` have zero tests despite several
being pure/deterministic and trivial to cover. (M)

## Deliberately excluded (already planned or verified non-issues)

Battle-sim fidelity (element/size/crit — known `BattleSimulator` TODOs); README/reconnect/combat-log (Phase 7);
level-based map auto-select and AI-aware routing (planned in PLAN.md); the manual stat/skill UI (Phase E, done);
and the async-deadlock/locking concern (engine verifiably has none).

> Fastest payback: **MCP-P0** (error visibility) + the **annotations/instructions** item in MCP-P1 — together
> ~half a day, and they turn this from "works if you watch it" into "safe for an agent to drive unattended."
