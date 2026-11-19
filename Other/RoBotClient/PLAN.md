# RoBotClient — multi-bot web dashboard for Ragnarok Rebuild

A standalone C#/.NET app that runs simple bot characters (walk + attack, hunt/flee) against the
live `RoRebuildServer`, with a Blazor Server dashboard to manage many bots at once.

## Why C#/.NET (not Python/JS)

The client↔server transport is **WebSocket binary frames** at `ws://127.0.0.1:5000/ws` (one frame =
one packet, first byte = `PacketType`). The wire format has two hard C# dependencies that make any
other language fragile:

1. **Lidgren `NetBitWriter` bit-packing** — fields are *not* byte-aligned (a `bool` is 1 bit), so the
   body can't be read as plain little-endian structs.
2. **MemoryPack** — the live `CreateEntity2` spawn packet (how a bot learns about monsters and its own
   state) is a MemoryPack blob of the shared `*SpawnParameters` structs. MemoryPack is C#-only.

So the bot engine is **.NET 9**, references the game's **`RebuildSharedData.dll`** (exact `PacketType`
enum + MemoryPack structs, zero drift), and copies the ~3-file bit-packer from the server.

## Solution layout (`Other/RoBotClient/`)

```
RoBotClient.sln
├── RoBotClient.Bot/      net9.0 class library — protocol + engine (no web deps)
│   └── Protocol/         NetBitWriter, SingleUIntUnion, NetException, PacketWriter, PacketReader
├── RoBotClient.Tests/    xUnit — protocol round-trip + golden-vector tests
└── RoBotClient.Web/      Blazor Server dashboard            (added in Phase 5)
```

Shared types are consumed as **compiled DLLs** via `<HintPath>` into the server's build output
(`RoRebuildServer/RoRebuildServer/bin/Debug/net9.0`), matching the `Other/RoWikiGenerator` convention.
**Prerequisite: build `RoRebuildServer` (Debug) before building this**, so the DLLs exist.

## Architecture

```
RoBotClient.Web (Blazor Server)  —  list view "/" + detail "/bot/{id}" + <canvas> map
        │ SignalR circuit (4 Hz snapshot refresh + event push)
RoBotClient.Bot
  BotManager        owns N BotSession, account provisioning, immutable snapshots
   └─ BotSession    per-bot async loop; self-state + world-state
        ├─ Behavior FSM   Idle │ Travel │ Hunt │ Flee │ Dead   (driven by BotConfig)
        ├─ GameConnection ClientWebSocket, login handshake, ping
        │     └─ Protocol PacketWriter/Reader, NetBitWriter, PacketType, MemoryPack
        └─ GameData       MonsterDb/Maps/WarpGraph/Items/Skills (JSON) + WalkMap (.walk) + A*
        │ ws://127.0.0.1:5000/ws (binary frames)
RoRebuildServer (authoritative; unchanged)
```

The server is authoritative — it pathfinds, auto-approaches, and does combat math. The bot sends small
intent packets (`StartWalk`, `Attack`) and reconstructs state from broadcasts.

## Wire protocol (the crux)

- **Handshake (first frame only):** plain .NET `BinaryWriter` — `short version`, `bool isNewAccount`,
  `bool isTokenLogin`, `bool requestLoginToken`, `string username`, `string password`. `version` must
  equal the server's (read `ClientConfigGenerated/ServerVersion.txt`). `isNewAccount=true` auto-creates.
- **Everything after:** bit-packed — `byte`=8b, `short/ushort`=16b LE, `int`=32b LE, `float`=32b IEEE-754,
  **`bool`=1 bit**, `string`=`ushort` UTF-8-byte-length + bytes, `Position`=2×int16. Some payloads are
  `int32`-length-prefixed MemoryPack (e.g. `CreateEntity2`).
- **Connect → in-game:** handshake → `ConnectionApproved` (char list) → `EnterServer` (select existing
  by name, or create new with stats summing to **33**) → `EnterServer` resp (entityId, map, GUID) +
  `UpdatePlayerData` → `PlayerReady` (single byte, send promptly) → `CreateEntity2` burst → `Ping` every ~5s.
- **Walk:** `StartWalk [int16 x][int16 y]` (server paths, max 20 tiles). **Attack:** `Attack [int32 id]`
  once; server auto-approaches and auto-swings. HP is tracked by subtracting damage from last-known HP.

## Game data (read existing exporter output; no server changes)

| Need | Source |
|---|---|
| Monster id→name/stats, drops, spawn maps | `Assets/StreamingAssets/ClientConfigGenerated/monsterdatabase.json` |
| Map list + display names | `maps.json` |
| Inter-map warp graph + portal tiles | `mapwarps.json` |
| Items / skills / status | `items.json`, `skillinfo.json`, `statusinfo.json` |
| Walkability (pathfinding) | `Assets/Maps/exportdata/<map>.walk` — `int32 w, int32 h, byte[w*h]`; walkable iff `(cell & 1)==1` |
| Server version | `ClientConfigGenerated/ServerVersion.txt` |

If game data changes, re-run `DataToClientUtility.exe` to refresh these files (the bot only reads them).

## Behavior model

`BotConfig { TargetMap, HuntClassIds[], FleeClassIds[], FleeHpPercent, ReturnToSavePointOnDeath, Leash }`
drives a per-bot FSM: **Travel** (warp-graph route + ≤20-tile waypoint walking) → **Hunt** (nearest
in-view monster in HuntClassIds → `Attack`) → **Flee** (low HP or flee-monster near → move away/warp) →
**Dead** (respawn).

## Roadmap & status

- [x] **Phase 0 — Scaffold + protocol codec + round-trip tests.** 33/33 tests pass; bool-bit-packing
      and little-endian layout byte-verified.
- [x] **Phase 1 — Connect + login handshake.** Verified live: bot logs in, auto-creates account,
      creates/selects a character, enters the world (`prt_fild08`), sends PlayerReady, receives the
      world-state burst, and stays alive with pings. Smoke test in `RoBotClient.Host`.
- [x] **Phase 2 — World + self state tracking.** Verified: `BotSession` decodes UpdatePlayerData
      (stats/skills/inventory via the shared data-driven arrays), builds a live entity table from
      CreateEntity2/RemoveEntity, tracks positions/HP from movement & combat packets, and loads
      GameData (monsters/maps JSON). Bot characters are named `[BOT] <name>`.
- [x] **Phase 3 — Actions + behavior FSM.** Verified live: the bot hunts whitelisted monsters
      (auto-approach via the server), flees below an HP threshold, roams to find monsters, travels
      between maps via the warp graph (re-sending PlayerReady on ChangeMaps), and recovers from death
      (correct Respawn packet + retry cooldown, then resumes). Walk-grid (.walk) used for fleeing/roaming.
- [x] **Phase 4 — BotManager (multi-bot)**: supervises N bots (BotSession + BotBehavior per task),
      auto-provisions accounts, exposes immutable `BotSnapshot`s + live session/behavior/config/log
      accessors. Registered as a DI singleton in the Blazor app.
- [x] **Phase 5/6 — Blazor UI** (built by a parallel agent group, then reviewed + integrated):
      dashboard list + nav, bot detail (stats/skills/inventory/log), live `<canvas>` map (BotMap + map.js),
      spawn/controls. Runs at `http://localhost:5080`; verified serving with live seeded bots.
      Integration fixes: `@code`-vs-loop-var collision, `cells` byte[]→int[] (JS-interop base64 trap),
      added Bootstrap, dropped HTTPS redirect for local use, optional `ROBOT_SEED` startup seed.
- [ ] **Phase 5 — Blazor UI** (list + detail + canvas map).
- [ ] **Phase 6 — UI controls** (spawn/stop, live params, click-to-walk).
- [ ] **Phase 7 — Polish** (reconnect, combat log, README).

## Build & test

```powershell
# 1. Build the server first so the shared DLLs exist at the HintPath:
dotnet build ..\..\RoRebuildServer\RoRebuildServer.sln -c Debug
# 2. Build + test the bot:
dotnet test RoBotClient.sln -c Debug
```

## Decisions (defaults; adjustable)

- Bots **auto-create throwaway accounts** (`bot_<n>`) via the `isNewAccount` handshake path.
- New characters use a fixed default stat build (summing to 33), so a fresh server needs no setup.
- Project lives in `Other/RoBotClient/` (DLL references, per the `RoWikiGenerator` precedent).
- Designed for **tens** of concurrent bots (one async task + one ClientWebSocket each).

## Behavior Refinement (v2) — finalized plan

Decisions: (1) progression **auto-selects** hunting grounds by level, with a **manual override** in the UI;
(2) **auto-sell by rarity** (sell common equipment + junk; keep rare), also triggered by full bag /
overweight, governed by a **keep-list**; (3) stat/skill allocation is **manual** via the UI for now;
(4) the **MCP server** also exposes the full game DB, a `simulate_fight` tool (BattleSimulator), the
bot's telemetry, and **read/write of a per-bot build `.md`** so an agent resumes across sessions;
(5) monster **AI values are classified** so safe-routing avoids aggressive monsters the bot can't beat.

Cross-cutting: **per-bot telemetry** — a timestamped + **level-stamped** event log (kills by monster,
deaths by monster/map, time-on-map, items looted/sold/used, exp/zeny); the level stamp lets the agent
age out stale data. Exposed via BotManager + the UI + MCP.

Verified protocol (all via the `PacketType` enum): Say(text<=140, type); UseInventoryItem(id,target) —
invalid id disconnects; NpcClick -> NpcSelectOption -> OpenShop -> ShopBuySell (NPC entity id from spawn
packets, <=20 lines); DropItem/PickUpItem (drop-id; Chebyshev<=1; ~0.3s throttle); ApplyStatPoints
(6 int deltas, silent fail); ApplySkillPoint(byte skill, +1/pkt, prereqs enforced).

Phases:
- **A - Foundations:** GameData loads items/skills/NPC data; item **names** in UI; capture `JobId`;
  ground-item state (`DropItem`/`PickUpItem`); **verbose chat** toggle; **telemetry recorder**.
- **B - Survive & loot:** auto-use **consumables** (potions); **auto-loot** + item **blacklist**.
- **C - Economy:** NPC shop dialog; **restock** potions + **auto-sell by rarity** (full/overweight) + keep-list.
- **D - Progression:** level-appropriate target/map selection + **AI-aware safe routing**.
- **E - Stat/skill UI:** manual allocation panel.
- **F - MCP server:** read DB + `simulate_fight` + telemetry + read/write build `.md` + bot control; localhost-only.
