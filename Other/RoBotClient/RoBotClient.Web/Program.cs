using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.Manager;
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

app.Run();

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
