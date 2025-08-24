using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Networking.PacketHandlers.Character
{
    [ClientPacketHandler(PacketType.ApplySkillPoint)]
    public class PacketApplySkillPoint : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (!connection.IsConnectedAndInGame || connection.Player == null)
                return;

            var player = connection.Player;
            var skillId = (CharacterSkill)msg.ReadByte();
            var skill = DataManager.SkillData[skillId];

            //do we have enough skill points?
            var points = player.GetData(PlayerStat.SkillPoints);
            if (points <= 0)
            {
                CommandBuilder.ErrorMessage(player, "Insufficient skill points.");
                return;
            }
            
            //do we already know the max level of this skill?
            var knownLevel = player.LearnedSkills.GetValueOrDefault(skillId, 0);
            if (knownLevel >= skill.MaxLevel)
            {
                CommandBuilder.ErrorMessage(player, "Cannot apply more skill points to this skill.");
                return;
            }

            if (player.JobSkillTree == null)
                return;

            if (player.JobSkillTree.JobRank > 0)
            {
                var skillPointsUsed = 0;
                foreach (var s in player.LearnedSkills)
                    skillPointsUsed += s.Value;

                var targetTree = player.JobSkillTree;
                while (!targetTree.SkillTree.ContainsKey(skillId) && targetTree.Parent != null)
                    targetTree = targetTree.Parent;

                var targetRank = targetTree.JobRank;
                var minUsedPoints = 9 + (targetRank - 1) * 49;
                if (skillPointsUsed < minUsedPoints)
                {
                    CommandBuilder.ErrorMessage(player, "You must apply skill points earned as a previous job before applying points in this skill.");
                    return;
                }
            }
            
            if (!CheckPrereqFromTree(skillId, player))
            {
                CommandBuilder.ErrorMessage(player, "You do not meet the requirements to level up this skill.");
                return;
            }
            
            player.AddSkillToCharacter(skillId, knownLevel + 1);
            player.SetData(PlayerStat.SkillPoints, points - 1);

            //CommandBuilder.ApplySkillPoint(player, skillId);
            player.RefreshWeaponMastery();
            player.UpdateStats(true, true);
        }

        private bool CheckPrereqFromTree(CharacterSkill skill, Player player)
        {
            List<SkillPrereq>? prereqs;
            var found = false;
            PlayerSkillTree? tree = player.JobSkillTree;
            if (tree == null)
                return false;

            do
            {
                if (tree.SkillTree.TryGetValue(skill, out prereqs))
                {
                    found = true;
                    break;
                }

                tree = tree.Parent;
            } while (tree != null);

            if (!found)
                return false;

            if(prereqs == null) return true;
            for (var i = 0; i < prereqs.Count; i++)
            {
                var prereq = prereqs[i];
                if (!player.LearnedSkills.TryGetValue(prereq.Skill, out var learned) || learned < prereq.RequiredLevel)
                    return false;
            }

            return true;
        }
    }
}
