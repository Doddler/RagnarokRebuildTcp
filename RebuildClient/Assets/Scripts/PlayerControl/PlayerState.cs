using System;
using System.Collections.Generic;
using Assets.Scripts.Data;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace Assets.Scripts.PlayerControl
{
    public class PlayerState
    {
        public static PlayerState Instance = new();

        public event Action AppearanceChanged;
        public event Action VitalsChanged;
        public event Action ExpChanged;
        public event Action CharacterInfoChanged;
        public event Action MapChanged;
        public event Action<CharacterStatusEffect, float> StatusAdded;
        public event Action<CharacterStatusEffect> StatusRemoved;
        //statuses are added again as the spawn applies them
        public event Action StatusesReset;
        public event Action FollowerChanged;
        public event Action StatsChanged;
        public event Action JobChanged;
        public event Action SkillsChanged;
        public event Action<CharacterSkill, int> SkillLevelChanged;
        public event Action WeightChanged;
        public event Action ZenyChanged;
        public event Action CartWeightChanged;
        public event Action WeaponClassChanged;
        public event Action MemoLocationsChanged;
        public event Action EquipmentChanged;

        public bool IsValid { get; set; } = false;
        //a HUD OnEnable can run at scene load before the player or game data exist
        public bool IsReadyForDisplay => IsValid && ClientDataLoader.Instance?.IsInitialized == true;
        public int EntityId { get; set; }
        public string PlayerName { get; private set; }
        public int Exp { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        //only the player has sp, and the server sends it apart from the spawn
        public int Sp { get; private set; }
        public int MaxSp { get; private set; }
        public int AmmoId { get; private set; }
        private readonly int[] characterData = new int[(int)PlayerStat.PlayerStatsMax];
        private readonly int[] characterStats = new int[(int)CharacterStat.CharacterStatsMax];
        public float AttackDelay { get; private set; }
        public ClientSkillTree SkillTree;
        public int SkillPoints => GetData(PlayerStat.SkillPoints);
        public int WeaponClass { get; private set; }

        //unset until the first spawn; a default appearance is a real look
        private PlayerAppearance? appearance;
        public PlayerAppearance Appearance => appearance.GetValueOrDefault();
        public int JobId => Appearance.JobId;

        public int CartWeight { get; private set; }
        public int CurrentWeight { get; private set; }
        public int MaxWeight { get; private set; } = 3000;
        public int Zeny => GetData(PlayerStat.Zeny);
        private readonly Dictionary<CharacterSkill, int> knownSkills = new();
        private readonly Dictionary<CharacterSkill, int> grantedSkills = new();
        public IReadOnlyDictionary<CharacterSkill, int> KnownSkills => knownSkills;
        public IReadOnlyDictionary<CharacterSkill, int> GrantedSkills => grantedSkills;
        public bool IsAdminHidden = false;
        public readonly ClientInventory Inventory = new();
        public readonly ClientInventory Cart = new();
        public readonly ClientInventory Storage = new();
        private readonly int[] equippedItems = new int[10];
        public IReadOnlyList<int> EquippedItems => equippedItems;
        private readonly MapMemoLocation[] memoLocations = new MapMemoLocation[4];
        public IReadOnlyList<MapMemoLocation> MemoLocations => memoLocations;
        public bool HasCart => HasFollower(CharacterFollowerState.AnyCart);
        public bool HasFalcon => HasFollower(CharacterFollowerState.Falcon);
        public bool HasPeco => HasFollower(CharacterFollowerState.Mounted);
        public string MapName { get; private set; }
        public readonly PartyState Party;

        public int Level => GetData(PlayerStat.Level);
        public int JobLevel => GetData(PlayerStat.JobLevel);
        public int JobExp => GetData(PlayerStat.JobExp);
        public int BaseExpRequired => ClientDataLoader.Instance.GetBaseExpRequired(Level);
        public int JobExpRequired => ClientDataLoader.Instance.GetJobExpRequired(JobId, JobLevel);
        public float? BaseExpProgress => ExpProgress(Exp, BaseExpRequired);
        public float? JobExpProgress => ExpProgress(JobExp, JobExpRequired);

        //A destroyed character stays here until the next spawn binds a new one.
        public ServerControllable Character { get; private set; }

        public PlayerState()
        {
            Party = new PartyState(this);
            Party.Changed += OnPartyChanged;
        }

        public int GetData(PlayerStat stat) => characterData[(int)stat];
        public int GetStat(CharacterStat stat) => characterStats[(int)stat];
        public bool IsEquipped(int bagId) => bagId > 0 && Array.IndexOf(equippedItems, bagId) >= 0;

        //Forgets the previous character on logout, so the next login reports its look and doesn't inherit its party.
        //No events: the previous scene's listeners are going away.
        public void ResetForNewSession()
        {
            IsValid = false;
            appearance = null;
            Party.ResetForNewSession();
        }

        //once per new character, before its hp and statuses are applied
        public void BindCharacter(ServerControllable character)
        {
            //by reference, so a destroyed character is unsubscribed too
            if (!ReferenceEquals(Character, null))
            {
                Character.NameChanged -= OnNameChanged;
                Character.LevelChanged -= OnLevelChanged;
                Character.AppearanceChanged -= OnAppearanceChanged;
                Character.WeaponClassChanged -= OnWeaponClassChanged;
                Character.FollowerChanged -= OnFollowerChanged;
                Character.HpChanged -= OnHpChanged;
                Character.StatusAdded -= OnStatusAdded;
                Character.StatusRemoved -= OnStatusRemoved;
            }

            Character = character;
            character.NameChanged += OnNameChanged;
            character.LevelChanged += OnLevelChanged;
            character.AppearanceChanged += OnAppearanceChanged;
            character.WeaponClassChanged += OnWeaponClassChanged;
            character.FollowerChanged += OnFollowerChanged;
            character.HpChanged += OnHpChanged;
            character.StatusAdded += OnStatusAdded;
            character.StatusRemoved += OnStatusRemoved;

            //the new character's current values count as changes
            StatusesReset?.Invoke();
            OnNameChanged();
            OnLevelChanged();
            OnAppearanceChanged();
            OnWeaponClassChanged();
            OnFollowerChanged();
            OnHpChanged();
            character.ShowSp(Sp, MaxSp);
        }

        private void OnNameChanged()
        {
            PlayerName = Character.Name;
            CharacterInfoChanged?.Invoke();
        }

        private void OnLevelChanged()
        {
            characterData[(int)PlayerStat.Level] = Character.Level;
            NotifyLevelsChanged();
        }

        private void OnAppearanceChanged() => SetAppearance(Character.Appearance);

        //only fires on a real change, avoiding sprite reloads
        private void SetAppearance(PlayerAppearance newAppearance)
        {
            if (appearance == newAppearance)
                return;

            var oldJobId = JobId;
            appearance = newAppearance;
            AppearanceChanged?.Invoke();
            if (JobId == oldJobId)
                return;
            JobChanged?.Invoke();
            ExpChanged?.Invoke();
        }

        private void OnWeaponClassChanged()
        {
            if (WeaponClass == Character.WeaponClass)
                return;
            WeaponClass = Character.WeaponClass;
            WeaponClassChanged?.Invoke();
        }

        private void OnFollowerChanged() => FollowerChanged?.Invoke();

        private bool HasFollower(CharacterFollowerState follower) => Character != null && (Character.Follower & follower) != 0;

        private void OnHpChanged()
        {
            Hp = Character.Hp;
            MaxHp = Character.MaxHp;
            VitalsChanged?.Invoke();
        }

        private void OnStatusAdded(CharacterStatusEffect status, float duration) => StatusAdded?.Invoke(status, duration);

        private void OnStatusRemoved(CharacterStatusEffect status) => StatusRemoved?.Invoke(status);

        //also shown on the character's bar; a new character takes it from here when bound
        public void SetSp(int sp, int maxSp)
        {
            sp = Math.Max(sp, 0);
            if (Sp == sp && MaxSp == maxSp)
                return;
            Sp = sp;
            MaxSp = maxSp;
            VitalsChanged?.Invoke();
            if (Character != null)
                Character.ShowSp(Sp, MaxSp);
        }

        private void NotifyLevelsChanged()
        {
            CharacterInfoChanged?.Invoke();
            ExpChanged?.Invoke();
        }

        //values in PlayerClientStatusDef order; the server sends all of them on any change
        public void SetCharacterData(ReadOnlySpan<int> data, ReadOnlySpan<int> stats, float attackDelay)
        {
            var oldZeny = Zeny;
            var oldLevel = Level;
            var oldJobLevel = JobLevel;
            var oldJobExp = JobExp;

            var statsChanged = AttackDelay != attackDelay;
            AttackDelay = attackDelay;
            for (var i = 0; i < data.Length; i++)
            {
                ref var value = ref characterData[(int)PlayerClientStatusDef.PlayerUpdateData[i]];
                statsChanged |= value != data[i];
                value = data[i];
            }

            for (var i = 0; i < stats.Length; i++)
            {
                ref var value = ref characterStats[(int)PlayerClientStatusDef.PlayerUpdateStats[i]];
                statsChanged |= value != stats[i];
                value = stats[i];
            }

            var maxWeight = GetStat(CharacterStat.WeightCapacity);
            if (maxWeight != MaxWeight)
            {
                MaxWeight = maxWeight;
                WeightChanged?.Invoke();
            }

            if (Zeny != oldZeny)
                ZenyChanged?.Invoke();

            var levelsChanged = Level != oldLevel || JobLevel != oldJobLevel;
            if (levelsChanged)
                CharacterInfoChanged?.Invoke();
            if (levelsChanged || JobExp != oldJobExp)
                ExpChanged?.Invoke();
            if (statsChanged)
                StatsChanged?.Invoke();
        }

        public void SetSkills(IReadOnlyList<(CharacterSkill skill, int level)> known, IReadOnlyList<(CharacterSkill skill, int level)> granted)
        {
            CopySkills(knownSkills, known);
            CopySkills(grantedSkills, granted);
            SkillsChanged?.Invoke();
        }

        public void SetGrantedSkills(IReadOnlyList<(CharacterSkill skill, int level)> granted)
        {
            CopySkills(grantedSkills, granted);
            SkillsChanged?.Invoke();
        }

        private static void CopySkills(Dictionary<CharacterSkill, int> target, IReadOnlyList<(CharacterSkill skill, int level)> skills)
        {
            target.Clear();
            foreach (var (skill, level) in skills)
                target.Add(skill, level);
        }

        public void SetSkillLevel(CharacterSkill skill, int level, int skillPoints)
        {
            knownSkills[skill] = level;
            characterData[(int)PlayerStat.SkillPoints] = skillPoints;
            SkillLevelChanged?.Invoke(skill, level);
        }

        public void SetWeight(int weight)
        {
            if (CurrentWeight == weight)
                return;
            CurrentWeight = weight;
            WeightChanged?.Invoke();
        }

        public void SetZeny(int zeny)
        {
            if (Zeny == zeny)
                return;
            characterData[(int)PlayerStat.Zeny] = zeny;
            ZenyChanged?.Invoke();
        }

        public void SetCartWeight(int weight)
        {
            if (CartWeight == weight)
                return;
            CartWeight = weight;
            CartWeightChanged?.Invoke();
        }

        public void SetMemoLocations(IReadOnlyList<MapMemoLocation> locations)
        {
            for (var i = 0; i < memoLocations.Length; i++)
                memoLocations[i] = locations[i];
            MemoLocationsChanged?.Invoke();
        }

        //a bag id of 0 is an empty slot
        public void SetEquipment(ReadOnlySpan<int> bagIds, int ammoId)
        {
            if (AmmoId == ammoId && bagIds.SequenceEqual(equippedItems))
                return;
            bagIds.CopyTo(equippedItems);
            AmmoId = ammoId;
            EquipmentChanged?.Invoke();
        }

        public void SetEquippedItem(EquipSlot slot, int bagId)
        {
            if (equippedItems[(int)slot] == bagId)
                return;
            equippedItems[(int)slot] = bagId;
            EquipmentChanged?.Invoke();
        }

        public void SetAmmo(int bagId)
        {
            if (AmmoId == bagId)
                return;
            AmmoId = bagId;
            EquipmentChanged?.Invoke();
        }

        public void SetExp(int baseExp, int jobExp)
        {
            if (Exp == baseExp && JobExp == jobExp)
                return;
            Exp = baseExp;
            characterData[(int)PlayerStat.JobExp] = jobExp;
            ExpChanged?.Invoke();
        }

        //at disconnect, so the config saves during the scene teardown skip the hotbar
        public void ForgetPlayerName() => PlayerName = null;

        public void SetMapName(string mapName)
        {
            MapName = mapName;
            MapChanged?.Invoke();
        }

        //0 to 1; null at the highest level
        public static float? ExpProgress(int exp, int required) => required > 0 ? Math.Clamp((float)exp / required, 0f, 1f) : null;

        private void OnPartyChanged()
        {
            if (Character != null)
                Character.SetPartyName(Party.IsInParty ? Party.Name : null);
        }
    }
}
