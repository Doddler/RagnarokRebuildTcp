# 6-bot party progression — Astrid / Nyx / Hestia / Selene / Wren / Mira

Synthesis of per-class build + route research into a unified plan for leveling 6 bots together as a party. Reference companion: see [BOTS.md](BOTS.md) for the underlying MCP/FSM operating guide (mandatory config, known bugs, account model). This file is the party-specific play sheet on top of that.

## Roster (live)

| Bot ID | Class | Name | Sex | Hair (style/color) | Role | Spawn `desiredJob` |
|---|---|---|---|---|---|---|
| `bot7` | Swordsman | **Bran** | ♂ | 5 / 3 | Tank, pull initiator, party leader candidate | 1 |
| `bot2` | Thief | **Nyx** | ♀ | 12 / 8 | Sustained melee DPS, pure passives | 5 |
| `bot8` | Acolyte | **Cael** | ♂ | 8 / 0 | Heals + buffs (manual ally targeting needed) | 4 |
| `bot9` | Mage | **Selene** | ♀ | 17 / 1 | Caster DPS, map-by-map element swap | 3 |
| `bot5` | Archer | **Hawke** | ♂ | 4 / 5 | Ranged DPS, arrow inventory mgmt | 2 |
| `bot6` | Merchant | **Mira** | ♀ | 11 / 7 | Economy multiplier (+24% sell / −24% buy), Mammonite burst | 6 |

3♂ / 3♀, all fresh creations on prt_fild08. The build/route summaries below are written in original-roster-name terms (Astrid/Hestia/Wren) before this rename; references to those names should be read as the current ones (Astrid→Bran, Hestia→Cael, Wren→Hawke).

## Build summaries (full detail in agent research bundles)

### Astrid — Swordsman / 2H tank
- **Stats by Job-50:** STR 50, VIT 50, AGI 25, DEX 25 (priority STR > VIT > DEX/AGI).
- **Skills:** Bash 10, Provoke 5, MagnumBreak 1, TwoHandSwordMastery 10, IncreasedHPRecovery 10, Endure 1 (37 of 49 pts; spillover to Provoke / Endure).
- **Gear:** Katana (1116, lvl 4) → Slayer (1151, lvl 18) → Two-Handed Sword (1157, lvl 33) → Zweihander (1168, lvl 48). Full Plate (2316) once lvl 40. Helm (2228).
- **Script:**
  ```
  use Bash 0 enemy if hp > 0.4
  use Endure 0 self if hp < 0.35
  use MagnumBreak 1 self if enemies >= 3
  use Provoke 0 enemy every 30
  ```

### Nyx — Thief / agi-dex melee
- **Stats by Job-50:** AGI 50, DEX 30, STR 20 (priority AGI > DEX > STR).
- **Skills:** DoubleAttack 10, ImproveDodge 10, Steal 1. **All passive — NO skill script needed.**
- **Gear:** Cutter (1204 or 1205 [4-slot]) free → Main Gauche (1208, lvl 12) → Stiletto (1217) → Gladius (1220, lvl 24).
- **Script:** _none_ — the passive ticks ARE the build.

### Hestia — Acolyte / healer+buffer
- **Stats by Job-50:** INT 50, DEX 30, VIT 25.
- **Skills (43 of 49 pts):** DivProt 3, DemonBane 3, Heal 10, IncAgi 10, Blessing 10, DecAgi 1, Teleport 1, Cure 1, Angelus 3.
- **Gear:** Club (1501) → Mace (1504) → Flail (1510, lvl 14) → Chain (1519, slotted) → Morning Star (1513, lvl 27) → Stunner (1522, lvl 27). Scapulare (2323) + Biretta (2216) + Rosary. Buy at **Prontera Sanctuary Nun (`prt_church` 108,124)**.
- **Script (self-only — see Orchestrator section):**
  ```
  use Heal 0 self if hp < 0.7
  use IncreaseAgility 0 self every 60
  use Blessing 0 self every 60
  use Teleport 1 self if hp < 0.25
  use DecreaseAgility 0 enemy if hp < 0.5
  ```

### Selene — Mage / caster DPS
- **Stats by Job-50:** INT 40, DEX 30, VIT 10 (priority INT > DEX > slight VIT).
- **Skills:** FireBolt 10, ColdBolt 5, LightningBolt 5, StoneCurse 1, FireWall 1, FrostDiver 1, Thunderstorm 3, SoulStrike 1, NapalmBeat 3 (only as Soul Strike gateway).
- **Gear:** Novice Rod (1639) free → Wand (1604, lvl 12) → Arc Wand (1610, lvl 24) → Gentleman_Staff (1629, lvl 50). Buy at Geffen `geffen_in` Magical Item Seller (77,173).
- **Script:** **map-dependent** — Selene's script is rewritten by the orchestrator at each map change (cheat sheet below).
- **⚠ Critical correction over the original brief:** `ElementalChart.csv` has **Ghost-property attacks doing 5–50% to Neutral targets** (not 175%). NapalmBeat is a *trap* on Porings/Lunatics/Drops/Fabres — use LightningBolt (Wind→Water1 = 150%) as the early workhorse on the Poring-heavy `prt_fild0X` route, FireBolt on Earth-heavy zones (Wilow, Orc, ants).

### Wren — Archer / ranged DPS
- **Stats by Job-50:** DEX 70, AGI 50, VIT 20.
- **Skills:** OwlEye 5/10, VultureEye 5/10, DoubleStrafe 10, ImproveConcentration 5/10, ArrowShower 5. Skip ChargeArrow (knockback breaks tank's aggro).
- **Gear:** Bow_ (1702) → Composite Bow (1704, lvl 4) → Great Bow (1707, lvl 18) → Gakkung Bow (1714, lvl 33). Bandana (2211), Tights (2330) at lvl 45.
- **⚠ Critical: arrows.** Bow auto-attacks fail without arrows in inventory. **Always stock**: Normal Arrow (1750, 1z), plus elemental for current map. Buy at Payon: `payon_in01` Weapon Dealer (15,119) sells Arrow / Silver Arrow / Fire Arrow + bows; `payon` Tool Dealer (159,96) sells Normal Arrow cheap. Quivers (12004-12015) open into 500 arrows.
- **Script:**
  ```
  use ImproveConcentration 0 self every 90
  use ArrowShower 0 ground if enemies >= 3
  use DoubleStrafe 0 enemy if target.dist >= 2 and sppct > 0.2
  ```

### Mira — Merchant / economy + burst
- **Stats by Job-50:** STR 40, VIT 40, DEX 40 (utility tank with Mammonite kicker).
- **Skills (50 pts):** EnlargeWeightLimit 10, Discount 10, Overcharge 10, PushCart 1, Mammonite 10, CartRevolution 1, CrazyUproar 1, ItemAppraisal 5.
- **Gear:** Club (1501) → Axe (1301) → Battle Axe (1351, lvl 3, 2H) → Buster (1357, lvl 30) → Two-Handed Axe (1360). Sandals, Hat (2220), Adventurer Suit (2305).
- **Script (HP-gated to avoid zeny drain):**
  ```
  use CrazyUproar 0 self every 120
  use Mammonite 0 enemy if target.hp > 300
  ```
- **Note:** Vending skipped (NPC-shop, useless to bot).

## Shared route by phase + sync points

Sync points are coordinated `configure_bot enabled=false` pauses on early finishers until the slowest bot catches up. **Re-equip every weapon + armor manually after each job change** (BOTS.md finding #4).

### Phase 1 — Novice Job 1 → 10 — `prt_fild08`
- **All 6 together.** Hunt: PORING, LUNATIC, DROPS, FABRE, PUPA.
- Avoid `TARGET_DUMMY` and `MASTERING` boss spawn (already in `ignoreCodes`).
- **Selene (Mage):** ranged Novice has only basic attack — let her tag-along; she'll get share-XP from party kills.
- **Wren (Archer):** Novice can't yet use Bow effectively without arrows — stock 500 Normal Arrows pre-Phase-2 but Phase 1 just runs Knife.
- **🛑 SYNC POINT 1 — Novice Job 10.** As each bot reaches Job 10, pause her (`configure_bot enabled=false`). When the last one lands, the orchestrator triggers all 6 class-changes in close sequence at the Adventuring Bard.

### Phase 2 — Class change (Novice Job 10)
- All 6 walk to the Adventuring Bard for their respective transitions.
- **Job change unequips weapon + armor on every class** (BOTS.md #4). Immediately after each "Job change to X complete" log line:
  1. `get_bot` to scrape new `bagId`s.
  2. `equip_item` weapon, armor, garment, footgear.
- Job rewards (~3000z, 120 Novice Potions, 15 Fly Wings, 5 Butterfly Wings, 2 Concentration Potions, class weapon) — same per class.
- Apply class-specific `set_skill_script` rules (above).
- **Wren:** also pre-stock 1000 Normal Arrows + 200 Fire Arrows.
- **Mira:** call `ensure_basic_mastery 6` to qualify her for party-invite plumbing (Astrid stays leader by default — her BasicMastery is also at 8 from auto-spend).

### Phase 3 — 1st-job Job 1 → 15 — `prt_fild05` → `prt_fild06`
- **All 6 together.** Map progression: prt_fild05 (Tool Dealer at 290,221 — first big sell-cycle through Mira) → prt_fild06 (60 Lunatic + 60 Poring + 10 Thief Bug for the Sword/Thief aggro toys).
- Hunt: PORING, LUNATIC, DROPS, FABRE, THIEF_BUG.
- **Selene script:** `use LightningBolt 0 enemy every 3 if sp > 18` — primary on Porings (150%).
- **Hestia:** self-Blessing + self-IncAgi cycling kicks in. Cast Heal on others manually.
- **Wren:** Normal Arrows. Stay 3+ cells from melee — script enforces.
- **🛑 SYNC POINT 2 — 1st-job Job 10.** Wait for slowest before Phase 4. Most bots are around base lvl 18-22 here.

### Phase 4 — 1st-job Job 10 → 25 — `pay_fild06` (best shared zone)
- **All 6.** Pay Field 06: WORM_TAIL ×120 (lvl 14, 542 HP, **Earth1**, 40 jExp), SPORE ×20 (lvl 16, **Water1**, 61 jExp), POPORING ×20 (Poison1), THIEF_BUG_ female ×40 (lvl 14, 363 HP, **Dark1**, 48 jExp, LooterAssist).
- Why excellent shared: dense, mixed-element, no MVPs, no aggressive packs, near Payon for restock.
- **Selene script switch:** `use FireBolt 0 enemy every 3 if sp > 18` — Worm Tail is Earth1 (150%), Spore is Water1 (FireBolt 75% but Spore HP low → still 1-shot at Mage 20).
- **Wren:** swap to **Fire Arrows (1752)** for the Worm Tail / Pupa-egg phase; Normal Arrows for the rest.
- **Hestia:** anti-Plant via Mace auto-attacks contributes. Self-buff cycle. Manual `use_skill Heal Astrid` on the tank.
- **Mira:** stays adjacent; Mammonite (gated > 300 HP) clears Worm Tail clusters. **First major sell-cycle:** route all 6 bots' loot through `force_sell` from Mira at Payon Tool Dealer for the Overcharge +24%.

### Phase 5 — 1st-job Job 25 → 35 — **rotation**
Three options the orchestrator rotates between for variety + elemental hot zones:

**5a. `pay_fild07` Elder Willow** (lvl 20, 695 HP, **Fire2**, 159 jExp, **aggressive**)
- **Selene script:** `use ColdBolt 0 enemy every 3 if sp > 18` — Water → Fire2 = **175%**. Massive hot zone.
- **Astrid** is mandatory — Elder Willows aggro on sight, lead-pull with Bash.
- **Wren:** Crystal Arrows (1754) for the element double-up.
- **Hestia:** Heal Astrid actively.
- Risk: DOKEBI (lvl 33, Dark1, AiPassiveSense), HORONG (lvl 45, Fire4 — far above level; flee).

**5b. `gef_fild10` Orc Village exterior**
- ORC_LADY (lvl 35, 2777 HP, **Earth2**, 626 jExp, AiStandardBoss), ORK_WARRIOR (lvl 28, Earth1, AiAngry), ORC_BABY (Earth1).
- **Selene:** `use FireBolt 0 enemy every 3 if sp > 18` — Fire → Earth2 = **175%** on Orc Lady.
- **Wren:** Fire Arrows.
- **Astrid:** tanks Orc Warrior aggro waves; Provoke single-targets the high-HP Orc Lady.
- ⚠ **ORC_LORD MVP** rare spawn — keep in `fleeCodes`.

**5c. `pay_dun00` Skeletons** (Acolyte's home turf)
- SKELETON (lvl 12, Undead/Undead1), ZOMBIE (lvl 16, Undead/Undead1, AiAngry), POPORING (filler).
- **Hestia carries** — DemonBane + DivineProtection passives bonus vs Undead/Demon race.
- **Wren:** Silver Arrows (1751, Holy) — Holy → Undead1 = **175%**; or Fire Arrows (Fire → Undead1 = 125%).
- **Selene:** FireBolt 125% vs Undead.
- ⚠ FARMILIAR (aggressive Flying lvl 8) in `fleeCodes`. Watch ARCHER_SKELETON (range 9, ranged).
- Best zone for Phase 5 if Hestia is lagging — let her solo-pull ahead briefly while the rest grind 5a or 5b.

- **🛑 SYNC POINT 3 — base lvl 30.** Gear upgrade window: 2H Sword for Astrid, Stiletto for Nyx, Arc Wand for Selene, Great Bow → Cross Bow for Wren, Flail/Chain for Hestia, Battle Axe for Mira. Travel: **Izlude** (Astrid weapons via `izlude_in 60,127`), **Geffen** (Selene), **Prontera Sanctuary** (Hestia), **Payon** (Wren arrows). The orchestrator drives this multi-town errand chain through Mira-priced purchases (Discount −24%).

### Phase 6 — Job 35 → 50 (or whatever first-class cap is — likely 50)
Class divergence becomes acceptable here. Suggested rotation:

- **`orcsdun01` (Orc dungeon)** — ORC_ZOMBIE (Undead1) ×80, ORC_SKELETON (Undead1) ×10, ZENORC (Dark1), FARMILIAR ×15 (aggressive — Astrid tanks). Mage FireBolt 125%, Acolyte DemonBane race bonus, Archer Silver Arrows 175%, Thief auto-attack (def 0 mobs).
- **`prt_fild10` Savage** — 70× SAVAGE (lvl 30, 2684 HP, Earth2, 492 jExp). Tank-favored; sustained DPS chews big HP bars; Selene FireBolt (175% vs Earth2).
- **`pay_fild02` Wolves** — WOLF (lvl 25, 1419 HP, Earth1, AiAssist, 236 jExp). Single-pull only — AiAssist chains. Astrid lead-pulls, Hestia heals.
- **Solo deviation — Hestia → `moc_pryd02`** (Mummy lvl 37, def 0!, Undead2, 777 jExp). If Hestia outpaces by ~2 base levels, park her on the pyramid solo while party stays at orcsdun01. Re-merge before Phase 7.

## Orchestrator playbook — what the bots can't self-do

The DSL doesn't cover these; the orchestrator (us) drives them:

1. **Hestia ally heals/buffs.** The skill-script DSL has only `self/enemy/ground` targets — `Heal <ally>` cannot auto-fire. Poll `list_bots` for low-HP party members and manually `use_skill bot=bot3 Heal targetId=<their entity id>`. Same for `Blessing <ally>` and `IncreaseAgility <ally>` on pre-fight buff cycles.
2. **Selene per-map bolt script.** Rewrite `set_skill_script` on map change using the cheat-sheet (table below).
3. **Wren arrow restock.** When her arrow count drops below ~200, `go_to wren payon 159 96` (Tool Dealer) or `payon_in01 15 119` (Weapon Dealer for elemental). With `autoShop=false`, this is manual.
4. **Mira-routed sell-cycle.** Every ~80% weight on any bot, gather to Mira's position, `force_sell` everyone through her vendor for +24% Overcharge.
5. **Mira-routed buy-cycle.** Arrow / potion / gear purchases through Mira for −24% Discount.
6. **Re-equip after every job change.** `equip_item` weapon + armor + garment + footgear; `bagId`s shift.
7. **Sync-pause coordination.** At each Phase boundary, `configure_bot enabled=false` for finishers, resume all when last lands.
8. **Party formation workaround.** `invite_to_party` by name is broken (BOTS.md finding #9). Until fixed, party-share-XP doesn't reliably activate — proximity-grinding works regardless. Try `ensure_basic_mastery` + invite-by-entity-id once the MCP login-by-name fix lands.
9. **Pre-fight buffs.** Before each significant pull, manually cast Hestia's IncreaseAgility on Astrid+Nyx (aspd cliff), Blessing on Wren+Selene (DEX = matk/bow ATK/cast time).
10. **Loot management.** No inter-bot trade tool. Each bot's autoLoot keeps her own drops. Weight monitoring per-bot; sell-cycles are the only redistribution.

## Selene's per-map bolt cheat sheet

Orchestrator pushes this exact `set_skill_script` body at each map change:

| Map | Primary bolt | Why | Secondary |
|---|---|---|---|
| `prt_fild08`, `prt_fild05`, `prt_fild06` | LightningBolt | Poring Water1 = 150% | FireBolt for Pupa/Fabre clusters |
| `prt_fild01` | LightningBolt | Same Poring-heavy | FireBolt vs Fabre |
| `pay_fild01` Wilow | **FireBolt** | Earth1 = 150% | LightningBolt vs Spore/Poring |
| `pay_fild06` | FireBolt | Worm Tail Earth1 = 150%, Spore HP-low | LightningBolt mop-up |
| `iz_dun00`–`iz_dun02` Byalan | **LightningBolt** | Water1-3 = 150–200% | — |
| `pay_fild07` Elder Willow | **ColdBolt** | Fire2 = 175% | NapalmBeat for Eggyra Ghost2 (170%) |
| `gef_fild10` Orc | **FireBolt** | Earth1/Earth2 = 150–175% | — |
| `moc_fild05`/`moc_fild11` ants | FireBolt | Andre/Deniro/Piere Earth1 = 150% | _skip Golems (Neutral3 def 28)_ |
| `orcsdun01` | FireBolt | Undead1 = 125% | — |
| Goblin Camps, Ghost dungeons | — | Wind1 / Ghost no advantage | **Bench Selene** — basic attack only |

SP-guard template: `if sp > 18` keeps a reserve so the bot falls back to basic attack when SP-starved rather than constantly failing casts.

## Cross-class synergies (the reason for the 6-bot party)

- **Hestia Blessing on Wren** → +10 DEX → bow ATK + hit + Double Strafe scaling. Manually casts every 60s.
- **Hestia IncreaseAgility on Nyx + Astrid** → ASPD cliff. Nyx's DoubleAttack procs amplify; Astrid's Bash rotation tightens.
- **Selene FireBolt + Wren Fire Arrows** on Earth maps (Wilow, Orc, ants) → double-element saturation. Adjacent kills, fast clears.
- **Mira Overcharge** → +24% on every loot drop the party generates. 6 farmers × +24% sell = arrow budget + gear-upgrade budget self-funding.
- **Mira Discount** → −24% on every NPC purchase. Arrows (Wren's continuous spend), potions (everyone), gear upgrades at lvl-30 checkpoint.
- **Hestia DemonBane + Wren Silver Arrows + Selene FireBolt** on Undead zones (pay_dun, orcsdun, glast) → triple-element + race multipliers stack.
- **Astrid Provoke + Endure** → aggro lock + flinch-immunity → Selene can cast uninterrupted.
- **Astrid MagnumBreak** when enemies ≥3 → AoE + Fire enchant for next few hits.

## Sync-pause protocol (the user's explicit rule)

> "for every bot that gets to job 10, change jobs and wait for the others to catch up"

Implementation:

1. Monitor `list_bots` (compact) every ~2 min in autonomous-grind phases.
2. When a bot's `jobLevel` hits 10 (Novice phase, or 1st-job-Job-50 cap later):
   - The class-change to 1st-job auto-fires (per `desiredJob` setting).
   - After class-change log line, immediately `equip_item` her gear and apply her script.
   - Then `configure_bot enabled=false` to pause her.
3. When the **last** of the 6 finishers triggers her class-change, `configure_bot enabled=true` on all 6 → resume Phase 3.
4. Repeat at each Phase boundary (Phase 3 → 4, 4 → 5, 5 → 6) using base level / job level milestones from the route table.

Pause windows are also used for non-blocking errands: Mira-led shopping trips, Wren arrow restock, gear upgrades — all bundled before the resume.

## Operating gotchas (from BOTS.md — applied per bot)

- **`autoShop=false`** on all 6 — autoShop loop bug (BOTS.md #2) bites everyone regardless of class.
- **`healingItemIds: 501,502,503,504,512,507,515,569`** on all 6 — looted-food fallback heal.
- **`healHpPercent: 0.6`** on all 6.
- **`ignoreCodes`** baseline + `TARGET_DUMMY,TEST_DRONE` (BOTS.md #11).
- **`fleeCodes`** per BOTS.md baseline + class-specific extras (Wren especially: aggressive ranged like KOBOLD_ARCHER, GOBLIN_ARCHER).
- **`autoLoot=false`** on Wren and Mira specifically during heavy-traffic shared maps — the protected-drop wedge (BOTS.md #8) is more disruptive when they're carrying the party economy.
- **Fleeing-overrides-go_to (BOTS.md #10):** never let a bot aggro something she can't kill. The `fleeCodes` + per-fight forecast guard handle this; if a bot wedges, `stop_bot` + respawn is the cleanest recovery.
- **Party invite by name silently drops (BOTS.md #9):** until the MCP login/invite fix lands, treat party formation as **best-effort**. Proximity grinding works without formal party state, just loses share-XP smoothing. The user is working on a fix.

## Initial spawn batch (when server is up)

```text
spawn_bot characterName=Astrid  desiredJob=1 isMale=false hairStyle=3  hairColor=5 homeMap=prt_fild08
spawn_bot characterName=Nyx     desiredJob=5 isMale=false hairStyle=6  hairColor=8 homeMap=prt_fild08
spawn_bot characterName=Hestia  desiredJob=4 isMale=false hairStyle=14 hairColor=0 homeMap=prt_fild08
spawn_bot characterName=Selene  desiredJob=3 isMale=false hairStyle=10 hairColor=1 homeMap=prt_fild08
spawn_bot characterName=Wren    desiredJob=2 isMale=false hairStyle=9  hairColor=2 homeMap=prt_fild08
spawn_bot characterName=Mira    desiredJob=6 isMale=false hairStyle=11 hairColor=7 homeMap=prt_fild08
```

Followed by per-bot `configure_bot` calls with the safety + class-specific lists, then phase 1 grinding begins.
