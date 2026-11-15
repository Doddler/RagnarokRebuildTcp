# Operating the robot MCP bots

Practical operating notes for the `robot` MCP (Ragnarok Rebuild bot orchestration), verified through stress-test runs in May 2026. Read this **before** spawning/configuring — most defaults bite back.

## Tool inventory

- **Lifecycle:** `spawn_bot`, `stop_bot`, `list_bots`, `get_bot`, `configure_bot`
- **Stats/skills:** `allocate_stats`, `allocate_skill`, `list_skills`
- **Active skills:** `use_skill`, `get_skill_script`, `set_skill_script`
- **Combat sim:** `simulate_fight` (1v1), `simulate_group_fight` (pack)
- **Gear:** `equip_item`, `unequip_item`, `force_sell`
- **Movement:** `go_to` (HOLD at coords — overrides hunting), `resume_bot` (release)
- **Party:** `create_party`, `invite_to_party`, `accept_party_invite`, `get_party`, `leave_party`
- **Read-only DB:** `find_monsters`, `get_monster`, `find_traders`, `get_item`
- **Telemetry/persistence:** `get_telemetry`, `list_builds`, `read_build`, `write_build`

## Account / slot model — the spawn gotcha

- Each bot binds to a backend account (`bot_01`, `bot_02`, …). The runtime bot id (`bot1`, `bot2`, …) is just a session handle and **resets each server restart**.
- `spawn_bot` picks the **lowest-numbered free account**.
- **With** `characterName`: tries to *create* a character in slot 0. If slot 0 holds a *different* character it hard-fails with `Failed to create character, character slot is already occupied.` — it does not try another slot, doesn't auto-login.
- **Without** `characterName`: logs into whatever character already exists in slot 0 of the next free account. Cleanest way to resurrect leftover characters from prior sessions.
- Characters persist across server restarts; bot ids do not.
- If a spawn lands you in someone else's old leftover (different name), just keep it — re-equip, allocate, drive on. Don't fight the slot model.

## Always-apply `configure_bot` after spawn

Defaults are unsafe. Apply these every time:

| Field | Value | Why |
|---|---|---|
| `autoShop` | **false** | Default `true` causes a **tight restock loop** when zeny=0 and the auto-picked NPC (e.g. Flower Girl on prt_fild08) doesn't buy loot — kills ~70% of grind throughput. Novice Job 5 once didn't advance for 4 min because of this. |
| `healingItemIds` | `501,502,503,504,512,507,515,569` | Defaults to only the 4 potion IDs. With 0 zeny the heal-at-X% never fires. Adding Apple (512), Red Herb (507), Carrot (515), Novice Potion (569) lets the bot eat looted food as fallback. |
| `healHpPercent` | `0.6` | Default 0.5 is too late once you chain mobs. |
| `huntCodes` | safe-mob whitelist | See route below. |
| `ignoreCodes` | `GREEN_PLANT,RED_PLANT,BLUE_PLANT,YELLOW_PLANT,WHITE_PLANT,SHINING_PLANT,RED_MUSHROOM,BLACK_MUSHROOM,TARGET_DUMMY,TEST_DRONE` | Without this the win-margin guard still engages def-200 plants (time-wasters). **`TARGET_DUMMY` and `TEST_DRONE` are critical:** they're boss-tagged training props with 100k HP that trigger infinite-flee loops (see finding #11). |
| `fleeCodes` | aggressive list | Safety net: `FARMILIAR,ZOMBIE,MANDRAGORA,HYDRA,ELDER_WILLOW,POISON_SPORE,SCORPION,DRAINLIAR,THIEF_BUG__,GOBLIN_1,GOBLIN_2,GOBLIN_3,GOBLIN_4,GOBLIN_5,VOCAL,MASTERING`. |

## Stats and skills

- **Stats do NOT auto-allocate** — you must call `allocate_stats` manually.
- **Skill points DO auto-spend** on `BasicMastery` before a class change; any leftover Novice point auto-goes to `FirstAid`. Don't bother manually managing Novice skill points.
- Stat cost formula (RO classic): raising a stat from `s` to `s+1` costs `floor(s/10) + 2`. So 1→9 cost 2 each, 10→19 cost 3 each, 20→29 cost 4 each.
- **`allocate_stats` is all-or-nothing on overspend** — if total requested cost exceeds available points, the *whole call is a no-op*. Compute carefully or under-spend. A leftover 2-3 points is fine; losing the whole request hurts.
- `allocate_skill` spends one point per call; server validates prereqs/max.

## Safe leveling route (verified, zero deaths to Thief Job 9)

| Phase | Map | Hunt | Notes |
|---|---|---|---|
| Novice → Job 10 | `prt_fild08` | PORING, LUNATIC, DROPS, FABRE | Densest Poring spawn (100). Only a Flower Girl as on-map NPC. |
| Thief Job 2→7 | `prt_fild05` | + THIEF_BUG | Has a Tool Dealer for restock once zeny exists. |
| Thief Job 7→10 | `prt_fild06` | LUNATIC-rich (60+60+10) | Higher job-exp density. |

**Avoid** Prontera sewers (`prt_sewb*`) — aggressive lvl-23 Male Thief Bugs (`THIEF_BUG__`, atk 81–100) one-shot early thieves.

## Class quick notes (1st jobs)

| Job | id | Stats | Key skills | Needs `set_skill_script`? |
|---|---|---|---|---|
| Swordsman | 1 | STR/VIT/AGI | Bash, Endure | optional |
| Archer | 2 | AGI/DEX | Owl's Eye, Double Strafe | yes for actives |
| Mage | 3 | INT/DEX | FireBolt, ColdBolt, LightningBolt | **yes** — caster |
| Acolyte | 4 | INT/DEX | Heal, Increase Agi, Bless | **yes** — caster + buffer |
| Thief | 5 | AGI/DEX | DoubleAttack (passive), ImproveDodge (passive) | no — pure passives |
| Merchant | 6 | STR/DEX | EnlargeWeightLimit, Discount, Overcharge | optional |

For Thief specifically: equip **Cutter** (item 1204, 3-slot dagger, given as job-change reward).

## Job-change quirks

1. **Gear is unequipped** by the class change ("Job change to X complete." in log). Always re-`equip_item` weapon + armor after — `bagId`s shift, so `get_bot` first.
2. Job rewards include: ~3000 zeny, 120 Novice Potions (item 569), 15 Fly Wings, 5 Butterfly Wings, 2 Concentration Potions, a class-appropriate weapon (Cutter for Thief, etc).
3. Logs show the actual transition: "Spending a leftover skill point on First Aid before changing job." → "Job change: heading to the Adventuring Bard to become a X." → "Job change to X complete."

## Skill scripts (`set_skill_script`)

One rule per line: `use <Skill> <level> [self|enemy|ground] [every <sec>] [if <cond> and <cond>...]`. `level 0` = max known. Empty script disables auto-cast.

Examples:
```
use Heal 0 self if hp < 0.5
use FireBolt 0 enemy every 3
use IncreaseAgi 0 self every 60
```

The full DSL lives in the tool description for `set_skill_script` — consult it before writing a script.

## Party formation — the proximity gotcha

`invite_to_party` reports success on the leader's side but the target only registers the invite when **in view-range**. Scattered bots silently drop invites. Proven procedure:

1. Leader must have `BasicMastery` level **6+** (Novice Job 10 reaches 8, perfect).
2. `create_party` on leader; verify with `get_party` (expect `inParty: true`).
3. `go_to` each invitee to leader's coordinates (this puts them in *hold mode* — they stop hunting).
4. Wait ~60s for travel.
5. `invite_to_party` from leader, **by character name** (`"[BOT] Foo"`).
6. `accept_party_invite` on each invitee (`partyId=0` accepts the most recent pending invite).
7. `resume_bot` on each invitee to release the `go_to` hold so they hunt again.

## Known FSM / MCP bugs (current workarounds)

Real, reproduced findings as of 2026-05-28. Expect these until patched.

1. **Transient MCP "Unable to connect"** — robot-MCP's client link to the game server drops without auto-reconnect; bots vanish from `list_bots`, new spawns fail. Recovers on retry (sometimes needs the user to `/mcp` reconnect). Game server itself (port 5000) stays up.
2. **autoShop tight loop** with 0 zeny + non-buying NPC — see config above.
3. **No fallback heal from looted food** unless `healingItemIds` is widened — see config above.
4. **Job change unequips gear** — always re-equip.
5. **Map-boundary thrash** — bot bouncing across adjacent maps re-engaging an edge target. Move it away from edges; no clean automated workaround yet.
6. **Stale `targetName` on Idle snapshots** — cosmetic.
7. **`spawn_bot` slot collision** with differently-named occupant — see Account model above.
8. **autoLoot retries protected drops** — server replies "You are unable to pick up this item yet" (loot owner-lock) and the FSM **idles for ~5 minutes** before recovering. Workaround: ride it out, or `autoLoot=false` for sensitive runs.
9. **Party invite by name** — was broken in the 2026-05-28 session (silent drop); **now fixed as of 2026-05-29**. `invite_to_party` by character name now delivers correctly even across the map (verified: leader at (180,350), invitee at (340,214), invite landed and accepted cleanly). Proximity is no longer required.
10. **`Fleeing` mode overrides `go_to`** — once a bot enters `mode: Fleeing` it ignores all subsequent `go_to` orders. Confirmed by sending go_to to a bot fleeing a Target Dummy; bot stayed put. Workaround: `stop_bot` + respawn.
11. **Infinite-flee loop on Target Dummy** (and similar boss-tagged but harmless mobs) — the Target Dummy is `aggressive=true` `special=Boss` with HP 100k and atk 1-1. The bot's win-margin guard sees "can't kill in 30 rounds" and flees, but the dummy is stationary and the bot re-detects it every time it gets within scanDist 10. Net effect: bot wanders the map in a flee loop, never making progress. A bot can lose minutes here before either escaping out of range or being manually stopped. Worse: this can interrupt mid-task travel like the trip to the job-change NPC, leaving the bot stuck as a Novice forever.
12. **Auto-spend of BasicMastery on Novice skill points only fires near Job 10** (not continuously) — so a mid-novice bot (Job 4-9) has unspent skill points and CANNOT be a party leader (needs BasicMastery 6+). Workaround: manually `allocate_skill BasicMastery` ×N before requesting party leadership.

## Context-cost gotcha

`get_bot` returns the **full combat log** (huge — easily 10k+ tokens after a session). For routine progress checks use `list_bots` (compact: level, job, hp, map, kills, mode). Reserve `get_bot` for moments when you actually need stat/skill counts or inventory layout. `logLines: 0` on `get_bot` suppresses the log entirely.

## Cross-references

- Prior session memory: `~/.claude/projects/D--Unity-Projects-Ragnarok-Rebuild-RagnarokRebuildTcp/memory/project_robot_mcp_stress_test_filcher.md` (Filcher solo run, Novice→Thief J9).
- Build plans saved server-side: `read_build "[BOT] Filcher"` (may be cleared if MCP DB was reset).
