using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Database;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;

namespace RoWikiGenerator
{
    internal class Program
    {


        static void FixName()
        {
            var lines = File.ReadAllLines(@"G:\Projects2\Ragnarok\Zone\npcdata\mobname.def");
            foreach (var line in lines)
            {
                var s = line.Split(' ');
                if (s.Length < 2)
                    continue;

                var id = int.Parse(s[1]);
                var txt = s[0].ToLower();
                var path = $"../../../images/monsters/{id}.png";
                if (!File.Exists(path))
                    continue;

                File.Copy(path, $"../../../images/rebuildmonsters/{txt}.png");

                Console.WriteLine(path);

            }
        }

        static void Main(string[] args)
        {
            ServerLogger.Log("Ragnarok Rebuild Zone Server, starting up!");

            //FixName();
            //DistanceCache.Init();
            //RoDatabase.Initialize();


            var sharedIcons = new Dictionary<string, string>();

            foreach (var l in File.ReadAllLines(
                         @"../../../../../RebuildClient/Assets/Data/SharedItemIcons.txt"))
            {
                var s = l.Split('\t');
                sharedIcons.Add(s[0], s[1]);
            }

            //var cfg = ServerConfig.GetConfig();

            Console.WriteLine($"Min spawn time: {ServerConfig.DebugConfig.MinSpawnTime}");
            Console.WriteLine($"Max spawn time: {ServerConfig.DebugConfig.MaxSpawnTime}");

            DataManager.Initialize(true);

            var monsterMapSpawns = new Dictionary<int, List<(string map, MapSpawnRule spawn)>>();

            foreach (var map in DataManager.Maps)
            {
                var spawns = new WikiSpawnConfig();
                if (!DataManager.MapConfigs.TryGetValue(map.Code, out var loader))
                    continue;
                if (!DataManager.InstanceList.Any(i => i.Maps.Contains(map.Code)))
                    continue;

                loader(spawns);
                foreach (var spawn in spawns.SpawnRules)
                {
                    if (!monsterMapSpawns.TryGetValue(spawn.MonsterDatabaseInfo.Id, out var monList))
                    {
                        monList = new List<(string map, MapSpawnRule spawn)>();
                        monsterMapSpawns.Add(spawn.MonsterDatabaseInfo.Id, monList);
                    }

                    var existing = monList.FirstOrDefault(m =>
                        m.spawn.MonsterDatabaseInfo.Id == spawn.MonsterDatabaseInfo.Id && m.map == map.Code
                        && m.spawn.MinSpawnTime == spawn.MinSpawnTime && m.spawn.MaxSpawnTime == spawn.MaxSpawnTime);

                    if (existing.spawn != null)
                        existing.spawn.Count += spawn.Count;
                    else
                        monList.Add((map.Code, spawn));
                }
            }

            var monsters = DataManager.MonsterIdLookup.Select(m => m.Value)
                //.OrderBy(m => m.Level).ThenBy(m => m.Name).ToList();
                .OrderBy(m => m.Id).ToList();

            var txtOut = new StringBuilder();

            foreach (var m in monsters)
            {
                if (m.Id < 4000 || m.Id == 5000)
                    continue;
                var mvp = "";
                if (DataManager.MvpMonsterCodes.Contains(m.Code))
                    mvp = "MVP Boss";
                if (m.Special == CharacterSpecialType.Boss && mvp == "")
                    mvp = "Boss";
                var name = m.Id switch
                {
                    4146 => "Whisper (Stationary)",
                    4147 => "Whisper (Giant)",
                    6000 => "Moonlight Flower (Clone)",
                    6001 => "Thief Bug Egg (Summon)",
                    _ => m.Name
                };
                var special = "";

                var sprite = m.Code.ToLower();
                var magicMin = m.Int + m.Int / 7 * m.Int / 7;
                var magicMax = m.Int + m.Int / 5 * m.Int / 5;
                txtOut.Append($""" 
                        
                            <table cellpadding=4 style="width: 98%">
                            <tr>
                                <td style="vertical-align:top; width:20%;">
                                    <H2 class="subheader">{name}</H2>
                                    <br>
                                    <center>
                                        <img src="images/rebuildmonsters/{sprite}.png" />
                                    </center>
                                </td>
                            
                                <td style="vertical-align:top; width:22%;">
                                    <H2 class="subheader">Stats</H2>
                                    <table>
                            """);
                txtOut.AppendLine($"<tr><td><b>Level:</b></td><td>{m.Level}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>HP:</b></td><td>{m.HP:N0}</td></tr>");
                if (!string.IsNullOrWhiteSpace(mvp))
                    txtOut.AppendLine($"<tr><td><b>Type:</b></td><td>{mvp}</td></tr><tr>");
                txtOut.AppendLine($"<tr><td><b>Size:</b></td><td>{m.Size}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Element:</b></td><td>{m.Element}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Race:</b></td><td>{m.Race}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Base Exp:</b></td><td>{m.Exp:N0}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Job Exp:</b></td><td>N/A</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Attack Power:</b></td><td>{m.AtkMin:N0} ~ {m.AtkMax:N0}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Magic Attack:</b></td><td>{magicMin:N0} ~ {magicMax:N0}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Defence:</b></td><td>{m.Def}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Magic Defence:</b></td><td>{m.MDef}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>95% Flee:</b></td><td>{m.Dex + m.Level + 75}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>100% Hit:</b></td><td>{m.Agi + m.Level + 20}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Vit:</b></td><td>{m.Vit}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Int:</b></td><td>{m.Int}</td></tr>");
                txtOut.AppendLine($"<tr><td><b>Luck:</b></td><td>{m.Luk}</td></tr>");
                txtOut.AppendLine($"""
                                   </table></td>
                                   <td style="vertical-align:top; width:25%;">
                                       <H2 class="subheader">Drops</H2>
                                       <table>
                                   """);

                if (DataManager.MonsterDropData.TryGetValue(m.Code, out var drops))
                {
                    foreach (var drop in drops.DropChances)
                    {
                        var item = DataManager.ItemList[drop.Id];
                        var countStr = "";
                        var itemSprite = item.Name.Replace(" ", "_");
                        if (sharedIcons.TryGetValue(itemSprite, out var copy))
                            itemSprite = copy;
                        if (item.ItemClass == ItemClass.Card)
                            itemSprite = "card";
                        if (drop.CountMax > 1)
                            if (drop.CountMin == drop.CountMax)
                                countStr = $"[{drop.CountMax}x]";
                            else
                                countStr = $"[{drop.CountMin}x-{drop.CountMax}x]";
                        if (item.ItemClass == ItemClass.Weapon)
                            if (DataManager.WeaponInfo.TryGetValue(item.Id, out var weapon) && weapon.CardSlots > 0)
                                countStr = $"[{weapon.CardSlots}]";
                        if (item.ItemClass == ItemClass.Equipment)
                            if (DataManager.ArmorInfo.TryGetValue(item.Id, out var armor) && armor.CardSlots > 0)
                                countStr = $"[{armor.CardSlots}]";
                        txtOut.Append($"""
                                        <tr>
                                            <td style='text-align:center'><img src="images/rebuilditems/{itemSprite}.png"></td>
                                            <td height=24>{item.Name}{countStr} ({drop.Chance / 100f}%)</td>
                                        </tr>
                                        
                                       """);
                    }
                }

                txtOut.AppendLine($"</table></td>");
                txtOut.AppendLine($"""
                                   <td style="vertical-align:top; width:33%;">
                                    <H2 class="subheader">Spawns</H2>
                                   <table>
                                   """);

                string TimeSpanToTimeString(TimeSpan time)
                {
                    var sOut = new StringBuilder();
                    if (time.Hours > 0)
                        sOut.Append($"{time.Hours}h");
                    if (time.Minutes > 0)
                        sOut.Append($"{time.Minutes}m");
                    if (time.Seconds > 0)
                        sOut.Append($"{time.Seconds}s");
                    if (sOut.Length == 0)
                        return "0s";
                    return sOut.ToString();
                }


                if (monsterMapSpawns.TryGetValue(m.Id, out var spawn))
                {
                    if (spawn == null)
                        continue;
                    var s = spawn.OrderByDescending(e => e.spawn.Count);
                    foreach (var (mapCode, mapSpawn) in spawn)
                    {
                        var map = DataManager.Maps.FirstOrDefault(m => m.Code == mapCode);
                        var mapName = map != null ? map.Name : mapCode;
                        var mapSpecial = "";
                        if (mapSpawn.DisplayType == CharacterDisplayType.Boss)
                            mapSpecial = "<img src='images/iconboss.png' /> ";
                        if (mapSpawn.DisplayType == CharacterDisplayType.Mvp)
                            mapSpecial = "<img src='images/iconmvp.png' /> ";
                        var time = "Instant";
                        if (mapSpawn.MinSpawnTime > ServerConfig.DebugConfig.MinSpawnTime)
                        {
                            var min = TimeSpan.FromMilliseconds(mapSpawn.MinSpawnTime);
                            var max = TimeSpan.FromMilliseconds(mapSpawn.MaxSpawnTime);
                            if (min == max)
                                time = TimeSpanToTimeString(min);
                            else
                                time = $"{TimeSpanToTimeString(min)} - {TimeSpanToTimeString(max)}";
                        }
                        txtOut.AppendLine($"<tr><td width=60%>{mapSpecial}{mapName} ({mapCode})</td><td width=15%>{mapSpawn.Count}</td><td width=25%>{time}</td>");
                    }
                }

                txtOut.AppendLine("</table></td></tr></table>");

            }

            var template = File.ReadAllText("template.html");
            template = template.Replace("!!!!--MonsterArea--!!!!", txtOut.ToString());
            File.WriteAllText(@"../../../rebuildmonsters.html", template);

            //var world = new World();
            //NetworkManager.Init(world);

            Console.WriteLine("Hello, World!");
        }
    }
}
