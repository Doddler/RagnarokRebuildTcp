using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Player;
using RoWikiGenerator.Pages;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RoWikiGenerator.Generators;

public class JobModel
{

    public required JobInfo Info;
    public required PlayerSkillTree SkillTree;

    public string GetSpCostStringForSkill(SkillData data)
    {
        var spCost = "N/A";
        if (data.SpCost != null && data.SpCost.Length > 0)
        {
            var lastCost = data.SpCost[0];
            spCost = data.SpCost[0].ToString();
            var multipleCosts = false;
            for(var i = 1; i < data.SpCost.Length; i++)
            {
                spCost += $"/{data.SpCost[i]}";

                if (data.SpCost[i] != lastCost)
                    multipleCosts = true;
            }

            if (!multipleCosts)
                spCost = $"{data.SpCost[0]} Sp";
            else
                spCost += " Sp";
        }

        return spCost;
    }

    public string GetPrereqStringForSkill(CharacterSkill skill, List<SkillPrereq>? prereqs)
    {
        var prereq = "None";
        if (prereqs == null)
            return prereq;

        var pList = new List<string>();
        foreach (var p in prereqs)
        {
            var pSkill = DataManager.SkillData[p.Skill];
            pList.Add($"{pSkill.Name} Lv {p.RequiredLevel}");
        }

        if (pList.Count > 0)
            prereq = string.Join(", ", pList);

        return prereq;
    }
}

internal class Jobs
{
    public static async Task<string> GetJobPageData(int jobId)
    {
        var info = DataManager.JobInfo[jobId];
        var skillTree = DataManager.SkillTree[jobId];
        
        var jobModel = new JobModel()
        {
            Info = info,
            SkillTree = skillTree
        };
        
        return await Program.RenderPage<JobModel, RebuildJobs>(jobModel);
    }
}