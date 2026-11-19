using System.Text;
using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.GameData;
using RoBotClient.Bot.Session;
using RoBotClient.Bot.State;

// Usage:  [mode] [seconds]
//   hunt (default) : log in, enter, then hunt nearby monsters / flee when hurt for N seconds (default 300)
//   smoke          : log in, enter, print one state dump, exit
var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "hunt";
var seconds = args.Length > 1 && int.TryParse(args[1], out var ss) ? ss : 300;

void Log(string s) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {s}");

var gameData = GameDatabase.Load(ResolveRepoSubdir(Path.Combine("RebuildClient", "Assets", "StreamingAssets", "ClientConfigGenerated")),
                                 ResolveRepoSubdir(Path.Combine("RebuildClient", "Assets", "Maps", "exportdata")));
Log($"GameData: {gameData.MonstersById.Count} monsters, {gameData.MapsByCode.Count} maps loaded.");

var config = new BotConfig { CharacterBaseName = "BotZero" };
await using var bot = new BotSession(config, gameData);
bot.OnLog += Log;

if (!await bot.ConnectAndEnterAsync())
{
    Log("Could not enter the world. Is the server running?");
    return;
}

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(mode == "smoke" ? 5 : seconds));
var runTask = bot.RunAsync(cts.Token);
await Task.Delay(2500); // let the world-state burst (UpdatePlayerData + CreateEntity2 x N) arrive

if (mode == "smoke")
{
    PrintState(null);
    cts.Cancel();
    try { await runTask; } catch { }
    Log("Done.");
    return;
}

// HUNT: target weak field monsters, ignore the indestructible dummy, flee the boss or when hurt.
var behaviorConfig = new BotBehaviorConfig { HomeMap = "prt_fild08", Verbose = true };
AddCodes(behaviorConfig.IgnoreClassIds, "TARGET_DUMMY");
Log($"Engaging any monster the battle forecast deems winnable (margin {behaviorConfig.WinMargin}, <= {behaviorConfig.MaxRoundsToKill} hits); ignoring [{string.Join(", ", behaviorConfig.IgnoreClassIds)}].");

var behavior = new BotBehavior(bot, behaviorConfig, gameData);
behavior.OnLog += Log;
var behaviorTask = behavior.RunAsync(cts.Token);

Log($"HUNT mode for {seconds}s on '{bot.World.Self.Map}'. Watch '{bot.World.Self.Name}' in your client.");
var nextPrint = DateTime.UtcNow;
while (!cts.IsCancellationRequested)
{
    if (DateTime.UtcNow >= nextPrint) { PrintState(behavior); nextPrint = DateTime.UtcNow.AddSeconds(5); }
    try { await Task.Delay(1000, cts.Token); } catch { break; }
}

cts.Cancel();
try { await Task.WhenAll(runTask, behaviorTask); } catch { }
Log("Done.");
DumpTelemetry(bot);

void DumpTelemetry(BotSession b)
{
    var t = b.Telemetry;
    var sb = new StringBuilder();
    sb.AppendLine("──────── TELEMETRY ────────");
    sb.AppendLine($" Kills {t.Count(TelemetryEventType.Kill)}  Deaths {t.Count(TelemetryEventType.Death)}  Looted {t.Count(TelemetryEventType.Loot)}  Used {t.Count(TelemetryEventType.UsedItem)}  LevelUps {t.Count(TelemetryEventType.LevelUp)}");
    foreach (var kv in t.CountByDetail(TelemetryEventType.Kill).OrderByDescending(k => k.Value))
        sb.AppendLine($"   kill: {kv.Key} x{kv.Value}");
    foreach (var kv in t.CountByDetail(TelemetryEventType.Loot).OrderByDescending(k => k.Value))
        sb.AppendLine($"   loot: {kv.Key} x{kv.Value}");
    foreach (var kv in t.CountByDetail(TelemetryEventType.UsedItem).OrderByDescending(k => k.Value))
        sb.AppendLine($"   used: {kv.Key} x{kv.Value}");
    foreach (var kv in t.CountByDetail(TelemetryEventType.Death).OrderByDescending(k => k.Value))
        sb.AppendLine($"   died to: {kv.Key} x{kv.Value}");
    foreach (var kv in t.TimeByMap())
        sb.AppendLine($"   map {kv.Key}: {kv.Value.TotalSeconds:F0}s");
    sb.Append("───────────────────────────");
    Console.WriteLine(sb.ToString());
}

void AddCodes(HashSet<int> set, params string[] codes)
{
    foreach (var code in codes)
    {
        var id = gameData.ClassIdOf(code);
        if (id.HasValue) set.Add(id.Value);
        else Log($"  (warning) unknown monster code '{code}'");
    }
}

void PrintState(BotBehavior? b)
{
    var text = bot.WithState(w =>
    {
        var s = w.Self;
        var pos = w.SelfPosition;
        var monsters = w.Entities.Values.Count(e => e.IsMonster && e.Id != s.EntityId);
        var npcCount = w.Entities.Values.Count(e => e.IsNpc);
        EntityView? nearNpc = null; var nearNpcD = int.MaxValue;
        foreach (var e in w.Entities.Values)
        {
            if (!e.IsNpc) continue;
            var d = Math.Abs(e.Position.X - pos.X) + Math.Abs(e.Position.Y - pos.Y);
            if (d < nearNpcD) { nearNpcD = d; nearNpc = e; }
        }
        var selfEntity = w.Entities.TryGetValue(s.EntityId, out var se) ? se : null;
        var hp = selfEntity?.Hp ?? s.Hp;       // the live, combat-tracked HP
        var maxHp = selfEntity?.MaxHp ?? s.MaxHp;
        var sb = new StringBuilder();
        sb.AppendLine("──────── STATE ────────");
        sb.AppendLine($" {s.Name}  Lv {s.Level}/{s.JobLevel}  HP {hp}/{maxHp}  SP {s.Sp}/{s.MaxSp}  Kills {s.Kills}  BaseExp {s.BaseExp}");
        sb.AppendLine($" Map {s.Map}  Pos ({pos.X},{pos.Y})  Zeny {s.Zeny}  Items {s.Inventory.Count}  GroundDrops {w.GroundItems.Count}");
        if (b != null)
            sb.AppendLine($" Mode {b.Mode}  Target {(b.TargetId != 0 ? $"{b.TargetName} (#{b.TargetId})" : "none")}  Monsters in view {monsters}");
        sb.AppendLine($" NPCs in view {npcCount}; nearest {(nearNpc != null ? $"'{nearNpc.Name}'@({nearNpc.Position.X},{nearNpc.Position.Y}) d{nearNpcD}" : "none")}");
        sb.Append("───────────────────────");
        return sb.ToString();
    });
    Console.WriteLine(text);
}

static string ResolveRepoSubdir(string relative)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, relative);
        if (Directory.Exists(candidate))
            return candidate;
    }
    return "";
}
