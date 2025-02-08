using System.Collections.Generic;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.PlayerControl
{
    public class PlayerState
    {
        public static PlayerState Instance;
        
        public bool IsValid { get; set; } = false;
        public int EntityId { get; set; }
        public string PlayerName;
        public int Level;
        public int Exp;
        public int Hp;
        public int MaxHp;
        public int Sp;
        public int MaxSp;
        public int AmmoId;
        public int[] CharacterData = new int[(int)PlayerStat.PlayerStatsMax];
        public int[] CharacterStats = new int[(int)CharacterStat.CharacterStatsMax];
        public float AttackSpeed;
        public ClientSkillTree SkillTree;
        public int SkillPoints;
        public int JobId;
        public bool IsMale;
        public int HairStyleId;
        public int HairColorId;
        public int CurrentWeight = 0;
        public int MaxWeight = 3000;
        public int Zeny = 0;
        public Dictionary<CharacterSkill, int> KnownSkills = new();
        public bool IsAdminHidden = false;
        public ClientInventory Inventory = new();
        public ClientInventory Cart = new();
        public ClientInventory Storage = new();
        public int[] EquippedItems = new int[10];
        public HashSet<int> EquippedBagIdHashes = new();
        public MapMemoLocation[] MemoLocations = new MapMemoLocation[4];

        public int GetData(PlayerStat stat) => CharacterData[(int)stat];
        public int GetStat(CharacterStat stat) => CharacterStats[(int)stat];

        public PlayerState()
        {
            Instance = this;
        }
    }
}