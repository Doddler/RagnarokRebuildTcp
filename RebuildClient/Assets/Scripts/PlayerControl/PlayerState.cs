using System.Collections.Generic;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;

namespace Assets.Scripts.PlayerControl
{
    public class PlayerState
    {
        public bool IsValid { get; set; } = false;
        public int EntityId { get; set; }
        public int Level;
        public int Exp;
        public int Hp;
        public int MaxHp;
        public int Sp;
        public int MaxSp;
        public ClientSkillTree SkillTree;
        public int SkillPoints;
        public int JobId;
        public bool IsMale;
        public int HairStyleId;
        public int HairColorId;
        public int CurrentWeight = 0;
        public int MaxWeight = 3000;
        public Dictionary<CharacterSkill, int> KnownSkills = new();
        public bool IsAdminHidden = false;
        public ClientInventory Inventory = new();
        public ClientInventory Cart = new();
        public ClientInventory Storage = new();
        public int[] EquippedItems = new int[10];
        public HashSet<int> EquippedBagIdHashes = new();
    }
}