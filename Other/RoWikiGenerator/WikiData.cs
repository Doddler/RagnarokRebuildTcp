using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoWikiGenerator
{
    internal class WikiData
    {
        public static Dictionary<CharacterSkill, string> SkillDesc;

        private static void LoadSkillDescriptions()
        {
            var lines = File.ReadAllLines(Path.Combine(ServerConfig.DataConfig.DataPath, "Skills/SkillDescriptions.txt"));
            var sb = new StringBuilder();
            var curSkill = CharacterSkill.None;
            SkillDesc = new Dictionary<CharacterSkill, string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("//"))
                    continue;
                if (line.StartsWith("::"))
                {
                    if (!Enum.TryParse<CharacterSkill>(line.Substring(2), true, out var type))
                        throw new Exception($"Could not parse skill {line} in SkillDescriptions.txt");
                    if (curSkill != CharacterSkill.None && sb.Length > 0)
                        SkillDesc.Add(curSkill, sb.ToString().Replace("\r\n", "<br>").Trim());
                    curSkill = type;
                    sb.Clear();
                    continue;
                }

                sb.AppendLine(line);
            }
            if (curSkill != CharacterSkill.None && sb.Length > 0)
                SkillDesc.Add(curSkill, sb.ToString().Replace("\r\n", "<br>").Trim());
        }

        public static void LoadData()
        {
            LoadSkillDescriptions();
        }
    }
}
