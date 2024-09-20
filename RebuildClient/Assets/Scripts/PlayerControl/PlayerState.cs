using System.Collections.Generic;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;

namespace Assets.Scripts.PlayerControl
{
    public class PlayerState
    {
        public bool IsValid = false;
        public int EntityId;
        public int Level;
        public int Exp;
        public int Hp;
        public int MaxHp;
        public int Sp;
        public int MaxSp;
        public ClientSkillTree SkillTree;
        public int SkillPoints;
        public int JobId;
        public Dictionary<CharacterSkill, int> KnownSkills = new();
        public bool IsAdminHidden = false;
        public ClientInventory Inventory = new();
        public ClientInventory Cart = new();
        public ClientInventory Storage = new();
    }
}