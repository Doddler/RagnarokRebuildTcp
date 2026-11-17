using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.Manager;
using RoBotClient.Web;
using RoBotClient.Web.Components;
using RoBotClient.Web.Mcp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Load the exported game data once (monsters/maps/warps + .walk grids), and register the bot manager.
// The warp script dir feeds the region-aware WorldGraph — gracefully no-ops if the server repo isn't
// alongside the dashboard.
var gameData = GameDatabase.Load(
    ResolveRepoSubdir(Path.Combine("RebuildClient", "Assets", "StreamingAssets", "ClientConfigGenerated")),
    ResolveRepoSubdir(Path.Combine("RebuildClient", "Assets", "Maps", "exportdata")),
    ResolveRepoSubdir(Path.Combine("RoRebuildServer", "GameConfig", "ServerData", "Script", "Warps")));
builder.Services.AddSingleton(gameData);
builder.Services.AddSingleton(new MonsterSprites(
    ResolveRepoSubdir(Path.Combine("RebuildClient", "Assets", "StreamingAssets", "ClientConfigGenerated")),
    ResolveRepoSubdir(Path.Combine("RebuildClient", "Assets", "Sprites", "Imported", "Monsters"))));

// Persistent registry of (account, password, character) tuples — populated when a bot enters the world, so
// the UI and MCP can later "reconnect" to a previously-used character without remembering credentials.
var repoRoBot = ResolveRepoSubdir(Path.Combine("Other", "RoBotClient"));
builder.Services.AddSingleton(new AccountStore(string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "accounts.json")));

// Per-character behavior config persisted next to it. configure_bot / dashboard Apply save here; spawn
// rehydrates over the spawn-form defaults so settings survive a stop+respawn or dashboard restart.
builder.Services.AddSingleton(new BotConfigStore(string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "bot-configs.json")));

// Per-character party state (InParty / IsLeader / LeaderName) so MCP get_party reflects reality across
// reconnects — the server keeps the bot in the party but the bot's local flags would otherwise reset.
builder.Services.AddSingleton(new PartyStateStore(string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "bot-party-state.json")));

// Per-character event log persistence — kills/deaths/loot/items/skill-casts/map-changes/level-ups survive
// across stop+respawn / dashboard restart so the controlling agent always has historical context.
builder.Services.AddSingleton(new BotTelemetryStore(string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "bot-telemetry")));

// Per-character OnLog mirror file at bot-logs/<character>.log — tee'd from runner buffer for autonomous
// agents that want to tail the bot's narrative without round-tripping through MCP.
builder.Services.AddSingleton(new BotLogStore(string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "bot-logs")));

// Per-character progression curve (playtime vs lifetime base exp) at bot-progress/<character>.jsonl.
builder.Services.AddSingleton(new BotProgressStore(string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "bot-progress")));

// Per-character map move audit (predicted vs measured breakdown per move) at bot-map-audit/<character>.jsonl.
builder.Services.AddSingleton(new MapAuditStore(string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "bot-map-audit")));

// Per-character learned state (map bans, death/flee counters, monster avoidance, kill timings) at bot-memory.json.
builder.Services.AddSingleton(new BotMemoryStore(string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "bot-memory.json")));

builder.Services.AddSingleton<BotManager>();

// Auto-formed dynamic squads: every ~8s, scan each bot's visible-players list and cluster bots that can
// see each other into squads, electing leaders by SquadRanking. Manual squads (SquadId not starting with
// `auto-`) are immune. Enable/disable from MCP via set_auto_squad.
builder.Services.AddSingleton(sp => new SquadAutoFormer(sp.GetRequiredService<BotManager>()));

// Per-bot build-plan files (markdown), under Other/RoBotClient/BotBuilds when resolvable.
builder.Services.AddSingleton(new BuildStore(string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "BotBuilds")));

// MCP server: an agent endpoint to read the DB, run the battle simulator, read telemetry, persist build
// files, and control bots. Tools = the [McpServerToolType] classes in this assembly. Localhost only.
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Eagerly resolve the SquadAutoFormer so its timer starts at boot (and bots auto-cluster as soon as the
// dashboard is up). Without this, the singleton is constructed only on the first MCP toggle.
_ = app.Services.GetRequiredService<SquadAutoFormer>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// MCP endpoint (Streamable HTTP) for an agent to connect at http://localhost:5080/mcp.
app.MapMcp("/mcp");

// Game art exported by the Unity importers: minimaps (one pixel per map cell; the dashboard maps fall back to
// the walk grid without one) and item/skill icons (<item Code>.png, skill_<SkillData.Icon>.png).
// Items that share a sprite have no file of their own; the client's icon importer records which icon they
// reuse in SharedItemIcons.txt ("<item code>\t<icon name>" per line).
var sharedIconsFile = Path.Combine(ResolveRepoSubdir(Path.Combine("RebuildClient", "Assets", "Data")), "SharedItemIcons.txt");
var sharedIcons = File.Exists(sharedIconsFile)
    ? File.ReadLines(sharedIconsFile).Select(l => l.Split('\t')).Where(p => p.Length == 2)
        .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1])
    : new Dictionary<string, string>();
MapRepoPngs("/minimap/{name}.png", Path.Combine("RebuildClient", "Assets", "Maps", "minimap"), new Dictionary<string, string>());
MapRepoPngs("/icons/{name}.png", Path.Combine("RebuildClient", "Assets", "Sprites", "Imported", "Icons", "Sprites"), sharedIcons);
MapRepoPngs("/monster-atlas/{name}.png", Path.Combine("RebuildClient", "Assets", "Sprites", "Imported", "Monsters", "Atlas"), new Dictionary<string, string>());

// Optional demo seed: spawn N hunting bots on startup (set the ROBOT_SEED env var).
if (int.TryParse(Environment.GetEnvironmentVariable("ROBOT_SEED"), out var seedCount) && seedCount > 0)
{
    try
    {
        var manager = app.Services.GetRequiredService<BotManager>();
        for (var i = 0; i < seedCount; i++)
            manager.SpawnBot(new BotBehaviorConfig { HomeMap = "prt_fild08" });
    }
    catch (Exception ex) { app.Logger.LogError(ex, "Bot seed failed"); }
}

// Dev loop: remember which bots were online and bring them back after a restart (including the automatic
// restarts `dotnet watch` does for edits hot reload can't apply). Shutdown logs them out cleanly first so
// the server releases the accounts instead of answering "username taken".
var runningBotsFile = string.IsNullOrEmpty(repoRoBot) ? "" : Path.Combine(repoRoBot, "running-bots.json");
if (runningBotsFile.Length > 0)
{
    var botManager = app.Services.GetRequiredService<BotManager>();
    // Bots still waiting in the staggered respawn below: a restart during it must keep them, or they're lost.
    var pendingRespawn = new HashSet<string>();
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        List<string> names;
        lock (pendingRespawn) names = botManager.RunningCharacters().Union(pendingRespawn).ToList();
        if (names.Count > 0) File.WriteAllText(runningBotsFile, System.Text.Json.JsonSerializer.Serialize(names));
        botManager.StopAllAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
    });
    app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
    {
        if (!File.Exists(runningBotsFile)) return;
        var names = System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(runningBotsFile)) ?? new();
        File.Delete(runningBotsFile); // a crash mid-respawn shouldn't replay forever
        lock (pendingRespawn) pendingRespawn.UnionWith(names);
        await Task.WhenAll(names.Select(async (name, i) =>
        {
            try {
            await Task.Delay(TimeSpan.FromMilliseconds(500 * i)); // stagger logins a little
            // Saved per-character config (hunt map, build, shop settings) is rehydrated inside spawn.
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    if (botManager.SpawnExistingBot(new BotBehaviorConfig { HomeMap = "prt_fild08" }, name) == null) break; // unknown character
                    await Task.Delay(TimeSpan.FromSeconds(4)); // login takes a moment; failures land in the spawn-failure list
                    if (!botManager.SpawnFailedRecently(name, TimeSpan.FromSeconds(5))) break;
                }
                catch (InvalidOperationException) { } // session cooldown: the old login is still being released
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            } finally { lock (pendingRespawn) pendingRespawn.Remove(name); }
        }));
        app.Logger.LogInformation("Respawned {Count} bot(s) from the previous run.", names.Count);
    }));
}

app.Run();

// Serves <relativeDir>/<name>.png, falling back to aliases[name]. Names never contain a slash, and the resolved
// file must stay in the folder.
void MapRepoPngs(string pattern, string relativeDir, Dictionary<string, string> aliases)
{
    var dir = ResolveRepoSubdir(relativeDir);
    app.MapGet(pattern, (string name) =>
    {
        if (dir.Length == 0 || !System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9_@'().-]+$"))
            return Results.NotFound();
        if (!File.Exists(Path.Combine(dir, name + ".png")) && aliases.TryGetValue(name, out var alias))
            name = alias;
        var path = Path.GetFullPath(Path.Combine(dir, name + ".png"));
        return Path.GetDirectoryName(path) == Path.GetFullPath(dir) && File.Exists(path)
            ? Results.File(path, "image/png")
            : Results.NotFound();
    });
}

// Walk up from the app's base directory to the repo, then into a client subfolder.
static string ResolveRepoSubdir(string relative)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, relative);
        if (Directory.Exists(candidate))
            return candidate;
    }
    return "";
}
