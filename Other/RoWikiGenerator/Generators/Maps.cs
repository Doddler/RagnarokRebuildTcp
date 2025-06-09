using RoRebuildServer.Data;
using RoWikiGenerator.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RoRebuildServer.Data.Map;

namespace RoWikiGenerator.Generators;

public record WorldMapModel();
public record MapListingModel(Dictionary<string, List<string>> MapGrouping);
public record MapGroupModel(string MapName, List<string> MapEntries);
public record IndividualMapModel(MapEntry Model);

internal static class Maps
{
    public static Dictionary<string, List<string>> DungeonGrouping = new();
    public static Dictionary<string, string> DungeonWorldMapEntries = new();

    public static Dictionary<string, List<string>> FieldGrouping = new();
    
    public static void PrepareDungeonGroupings()
    {
        var section = "";
        var sectionDungeons = new List<string>();

        void FinishSection()
        {
            if (string.IsNullOrWhiteSpace(section))
                return;
            if (sectionDungeons.Count == 0)
                return;
            var s = section.Split('#');
            DungeonGrouping.Add(s[0], sectionDungeons);
            DungeonWorldMapEntries.Add(s[0], s[0]);
            if(s.Length > 1 && int.TryParse(s[1], out var count))
                for(var i = 1; i < count; i++)
                    DungeonWorldMapEntries.Add($"{s[0]}{i+1}", s[0]);

            section = "";
            sectionDungeons = new List<string>();
        }

        foreach (var l in File.ReadAllLines(Path.Combine(AppSettings.BasePath, "DungeonMapDef.txt")))
        {
            if (!l.StartsWith("-"))
            {
                FinishSection();
                section = l;
                continue;
            }

            var mapName = l.Substring(1);
            if (!IsMapInstanced(mapName))
                continue;
            sectionDungeons.Add(mapName);
        }
        FinishSection();
    }

    public static void PrepareFieldGroupings()
    {
        var section = "";
        var sectionFields = new List<string>();

        void FinishSection()
        {
            if (string.IsNullOrWhiteSpace(section))
                return;
            if (sectionFields.Count == 0)
                return;
            var s = section.Split('#');
            FieldGrouping.Add(s[0], sectionFields);

            section = "";
            sectionFields = new List<string>();
        }

        foreach (var l in File.ReadAllLines(Path.Combine(AppSettings.BasePath, "FieldMapDef.txt")))
        {
            if (!l.StartsWith("-"))
            {
                FinishSection();
                section = l;
                continue;
            }

            var mapName = l.Substring(1);
            if (!IsMapInstanced(mapName))
                continue;
            sectionFields.Add(mapName);
        }
        FinishSection();
    }

    public static async Task<string> RenderRegion(string groupName, List<string> maps)
    {
        var model = new MapGroupModel(groupName, maps);

        return await Program.RenderPage<MapGroupModel, MapRegions>(model);
    }


    public static bool IsMapInstanced(string map)
    {
        return DataManager.InstanceList.Any(i => i.Maps.Contains(map));
    }

    public static async Task<string> GetSpecificMap(string map)
    {

        return await Program.RenderPage<MapEntry, MapView>(DataManager.Maps.First(m => m.Code == map));
    }

    public static async Task<string> GetWorldMap()
    {
        return await Program.RenderPage<WorldMapModel, WorldMap>(new WorldMapModel());
    }

    public static async Task<string> GetDungeons()
    {
        return await Program.RenderPage<MapListingModel, MapList>(new MapListingModel(DungeonGrouping));
    }


    public static async Task<string> GetFields()
    {
        return await Program.RenderPage<MapListingModel, MapList>(new MapListingModel(FieldGrouping));
    }
}