using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using Dahomey.Json;
using RebuildData.Server.Data.CsvDataTypes;
using RebuildData.Server.Data.Types;
using RebuildData.Shared.ClientTypes;
using RebuildData.Shared.Util;
using RebuildZoneServer.Data.Management.Types;

namespace DataToClientUtility
{
	class Program
	{
		private const string path = @"..\..\..\..\RebuildZoneServer\Data\";
		private const string outPath = @"..\..\..\..\..\RebuildClient\Assets\Data\";
		private const string outPathStreaming = @"..\..\..\..\..\RebuildClient\Assets\StreamingAssets\";

		static void Main(string[] args)
		{
			WriteMonsterData();
			WriteServerConfig();
			WriteMapList();
		}


		private static void WriteServerConfig()
		{
			var inPath = Path.Combine(path, "ServerSettings.csv");
			var tempPath = Path.Combine(Path.GetTempPath(), @"ServerSettings.csv"); //copy in case file is locked
			File.Copy(inPath, tempPath);

			using (var tr = new StreamReader(tempPath) as TextReader)
			using (var csv = new CsvReader(tr, CultureInfo.CurrentCulture))
			{

				var entries = csv.GetRecords<CsvServerConfig>().ToList();

				//var ip = entries.FirstOrDefault(e => e.Key == "IP").Value;
				//var port = entries.FirstOrDefault(e => e.Key == "Port").Value;
				var url = entries.FirstOrDefault(e => e.Key == "Url").Value;

                var configPath = Path.Combine(outPathStreaming, "serverconfig.txt");

				File.WriteAllText(configPath, $"{url}");
			}

			File.Delete(tempPath);
		}

		private static void WriteMapList()
		{
			using var tempPath = new TempFileCopy(Path.Combine(path, "Maps.csv"));
			using var tr = new StreamReader(tempPath.Path) as TextReader;
			using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

            //using var tw = new StreamWriter(Path.Combine(path, "Maps2.csv"));
            //using var csvOut = new CsvWriter(tw, CultureInfo.CurrentCulture);


            var entries = csv.GetRecords<MapEntry>().ToList();
			var mapList = new ClientMapList();
			mapList.MapEntries = new List<ClientMapEntry>();

			foreach (var e in entries)
			{
				mapList.MapEntries.Add(new ClientMapEntry()
				{
					Code = e.Code,
					Name = e.Name,
					Music = e.Music
				});
			}
			
    //        foreach (var l in File.ReadAllLines(@"G:\Projects2\Ragnarok\Resources\data\mp3nametable.txt"))
    //        {
				//if(string.IsNullOrWhiteSpace(l) || l.StartsWith("//") || l.StartsWith("#"))
				//	continue;

    //            var s = l.Split('#');
    //            if (l.Length < 2)
    //                continue;

    //            var code = s[0].Substring(0, s[0].IndexOf('.'));

    //            var dir = s[1].Split("\\\\");
    //            if (dir.Length < 2)
    //                continue;

    //            var map = entries.FirstOrDefault(m => m.Code == code);
    //            if (map == null)
    //                continue;

    //            map.Music = dir[1];
    //        }

			//csvOut.WriteRecords(entries);

			JsonSerializerOptions options = new JsonSerializerOptions();
			options.SetupExtensions();
			
			var json = JsonSerializer.Serialize(mapList, options);

			var mapDir = Path.Combine(outPath, "maps.json");

			File.WriteAllText(mapDir, json);
		}

		private static List<MapEntry> GetMapList()
		{
			using var tempPath = new TempFileCopy(Path.Combine(path, "Maps.csv"));
			using var tr = new StreamReader(tempPath.Path) as TextReader;
			using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);
			
			return csv.GetRecords<MapEntry>().ToList();
		}

		private static List<CsvMapSpawnEntry> GetSpawnEntries()
		{
			var inPath = Path.Combine(path, "MapSpawns.csv");
			var tempPath = Path.Combine(Path.GetTempPath(), "MapSpawns.csv"); //copy in case file is locked
			File.Copy(inPath, tempPath, true);

			List<CsvMapSpawnEntry> monsters;

			using (var tr = new StreamReader(tempPath) as TextReader)
			using (var csv = new CsvReader(tr, CultureInfo.CurrentCulture))
			{
				monsters = csv.GetRecords<CsvMapSpawnEntry>().ToList();
			}

			File.Delete(tempPath);

			return monsters;
		}

		private static void WriteMonsterData()
		{
			var inPath = Path.Combine(path, "Monsters.csv");
			var tempPath = Path.Combine(Path.GetTempPath(), "Monsters.csv"); //copy in case file is locked
			File.Copy(inPath, tempPath, true);

			var monSpawns = GetSpawnEntries();
			var maps = GetMapList();

			monSpawns = monSpawns.Where(m => maps.Any(m2 => m2.Code == m.Map)).ToList();

			using (var tr = new StreamReader(tempPath) as TextReader)
			using (var csv = new CsvReader(tr, CultureInfo.CurrentCulture))
			{
				var monsters = csv.GetRecords<CsvMonsterData>().ToList();
				var mData = new List<MonsterClassData>(monsters.Count);

				foreach (var monster in monsters)
				{
					if (monster.Id >= 4000 && monSpawns.All(m => m.Class != monster.Code))
						continue;

					var mc = new MonsterClassData()
					{
						Id = monster.Id,
						Name = monster.Name,
						SpriteName = monster.ClientSprite,
						Offset = monster.ClientOffset,
						ShadowSize = monster.ClientShadow,
						Size = monster.ClientSize
					};

					mData.Add(mc);
				}

				var dbTable = new DatabaseMonsterClassData();
				dbTable.MonsterClassData = mData;

				JsonSerializerOptions options = new JsonSerializerOptions();
				options.SetupExtensions();

				var json = JsonSerializer.Serialize(dbTable, options);

				var monsterDir = Path.Combine(outPath, "monsterclass.json");

				File.WriteAllText(monsterDir, json);
			}

			File.Delete(tempPath);
		}
	}
}
