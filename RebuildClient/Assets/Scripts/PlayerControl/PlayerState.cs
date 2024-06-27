using System.Collections.Generic;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;

namespace Assets.Scripts.PlayerControl
{
    public class PlayerState
    {
        public bool IsValid = false;
        public int Level;
        public int Exp;
        public ClientSkillTree SkillTree;
        public int SkillPoints;
        public int JobId;
        public Dictionary<CharacterSkill, int> KnownSkills = new();
    }
}