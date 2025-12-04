using System.ComponentModel.DataAnnotations.Schema;
using RebuildSharedData.Enum;

namespace RoRebuildServer.EntityComponents.Character
{
    public class PlayerSkills
    {
        [NotMapped] public Dictionary<CharacterSkill, int> UnlockedSkills { get; set; } = new();
    }
}