using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation;

namespace RoWikiGenerator
{
    internal class WikiSpawnConfig : IServerMapConfig
    {
        public WikiSpawnConfig()
        {
            SpawnRules = new List<MapSpawnRule>();
        }

        public void AttachKillEvent(string spawnId, string name, int incAmnt)
        {
            //throw new NotImplementedException();
        }

        public void CreateSpawnEvent(string ev, int interval, string mobname, int spawnPer, int maxSpawn, int respawnTime)
        {
            //throw new NotImplementedException();
        }

        public void ApplySpawnsToMap()
        {
            //throw new NotImplementedException();
        }

        public MapSpawnRule CreateSpawn(string mobName, int count, Area area, int respawn, int variance,
            SpawnCreateFlags flags = SpawnCreateFlags.None)
        {
            if (!DataManager.MonsterCodeLookup.TryGetValue(mobName.Replace(" ", "_").ToUpper(), out var mobStats))
            {
                ServerLogger.LogError($"Could not spawn monster with name of '{mobName}', name not found.");
                return null!;
            }

            var minTime = ServerConfig.DebugConfig.MinSpawnTime;
            var maxTime = ServerConfig.DebugConfig.MaxSpawnTime;

            if (minTime > 0 && respawn < minTime)
                respawn = minTime;

            var respawnMax = respawn + variance;

            if (maxTime > 0 && respawn > maxTime)
                respawn = maxTime;
            if (maxTime > 0 && respawnMax > maxTime)
                respawnMax = maxTime;

            DataManager.ServerConfigScriptManager.SetMonsterSpawnTime(mobStats, "", ref respawn, ref respawnMax);

            //area = area.ClipArea(Map.MapBounds);

            var displayType = CharacterDisplayType.Monster;
            if (flags.HasFlag(SpawnCreateFlags.Boss))
                displayType = CharacterDisplayType.Boss;
            if (flags.HasFlag(SpawnCreateFlags.MVP))
                displayType = CharacterDisplayType.Mvp;

            var spawn = new MapSpawnRule(SpawnRules.Count, mobStats, area, count, respawn, respawnMax, displayType);
            if (!area.IsZero)
            {
                spawn.UseStrictZone = flags.HasFlag(SpawnCreateFlags.StrictArea);
                spawn.GuaranteeInZone = flags.HasFlag(SpawnCreateFlags.GuaranteeInZone);
            }

            SpawnRules.Add(spawn);

            if (flags.HasFlag(SpawnCreateFlags.LockToSpawnZone))
                spawn.LockToSpawnZone();

            return spawn;
        }

        public MapSpawnRule? CreateSpawn(string mobName, int count, int respawn, int variance, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
            CreateSpawn(mobName, count, Area.Zero, respawn, variance, flags);

        public MapSpawnRule? CreateSpawn(string mobName, int count, Area area, int respawn, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
            CreateSpawn(mobName, count, area, respawn, 0, flags);

        public MapSpawnRule? CreateSpawn(string mobName, int count, Area area, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
            CreateSpawn(mobName, count, area, 0, 0, flags);

        public MapSpawnRule? CreateSpawn(string mobName, int count, int respawn, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
            CreateSpawn(mobName, count, respawn, 0, flags);

        public MapSpawnRule? CreateSpawn(string mobName, int count, SpawnCreateFlags flags = SpawnCreateFlags.None) =>
            CreateSpawn(mobName, count, 0, 0, flags);

        public List<MapSpawnRule> SpawnRules { get; set; }
    }
}
