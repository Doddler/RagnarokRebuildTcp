using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;

namespace RoBotClient.Bot.State;

public readonly record struct SkillEntry(CharacterSkill Skill, int Level);

public sealed class InventoryItemView
{
    public int BagId;
    public int ItemId;
    public int Count;
    public bool IsUnique;
    public byte Refine;
    public Guid UniqueId;
    public int[] Cards = Array.Empty<int>();
}

/// <summary>The bot's own character: the data delivered in UpdatePlayerData plus its map.</summary>
public sealed class SelfState
{
    public int EntityId;
    public string Name = "";
    public string Map = "";

    // Raw values keyed by the same enums the server packs them under (see PlayerClientStatusDef).
    public readonly Dictionary<PlayerStat, int> Data = new();
    public readonly Dictionary<CharacterStat, int> Stats = new();

    public float AttackSpeed;
    public int Weight;
    public int MaxWeight;
    public int CartWeight;

    public readonly List<SkillEntry> KnownSkills = new();
    public readonly List<SkillEntry> GrantedSkills = new();
    public readonly List<InventoryItemView> Inventory = new();
    public readonly int[] EquippedBagIds = new int[10];
    public int AmmoId;
    public int BaseExp;
    public int Kills;
    public int JobId; // current job/class id (captured from the self spawn; not in UpdatePlayerData)

    public int Level => Data.GetValueOrDefault(PlayerStat.Level);
    public int JobLevel => Data.GetValueOrDefault(PlayerStat.JobLevel);
    public int Zeny => Data.GetValueOrDefault(PlayerStat.Zeny);
    public int Str => Data.GetValueOrDefault(PlayerStat.Str);
    public int Agi => Data.GetValueOrDefault(PlayerStat.Agi);
    public int Vit => Data.GetValueOrDefault(PlayerStat.Vit);
    public int Int => Data.GetValueOrDefault(PlayerStat.Int);
    public int Dex => Data.GetValueOrDefault(PlayerStat.Dex);
    public int Luk => Data.GetValueOrDefault(PlayerStat.Luk);
    public int JobExp => Data.GetValueOrDefault(PlayerStat.JobExp);
    public int SkillPoints => Data.GetValueOrDefault(PlayerStat.SkillPoints);
    public int StatPoints => Data.GetValueOrDefault(PlayerStat.StatPoints);

    public int Hp => Stats.GetValueOrDefault(CharacterStat.Hp);
    public int MaxHp => Stats.GetValueOrDefault(CharacterStat.MaxHp);
    public int Sp => Stats.GetValueOrDefault(CharacterStat.Sp);
    public int MaxSp => Stats.GetValueOrDefault(CharacterStat.MaxSp);
    public int Attack => Stats.GetValueOrDefault(CharacterStat.Attack);     // min attack (atk1)
    public int AttackMax => Stats.GetValueOrDefault(CharacterStat.Attack2); // max attack (atk2)
    public int Def => Stats.GetValueOrDefault(CharacterStat.Def);
    public int Flee => Stats.GetValueOrDefault(CharacterStat.AddFlee);
    public int Hit => Stats.GetValueOrDefault(CharacterStat.AddHit);
    /// <summary>Crit roll bonus in server units (out of 1000 — so 100 = 10%). Reflects LUK contribution
    /// PLUS gear/skill bonuses, so reading this in the simulator is strictly more accurate than
    /// recomputing crit chance from LUK alone.</summary>
    public int AddCrit => Stats.GetValueOrDefault(CharacterStat.AddCrit);
    /// <summary>Magic defense — only used by the simulator when forecasting incoming MAGIC damage.</summary>
    public int MDef => Stats.GetValueOrDefault(CharacterStat.MDef);
    /// <summary>Min/max magic attack power as the server tracks it (driven by INT + gear MATK).
    /// Used by the simulator's caster-DPS path so a Mage's expected per-cast damage isn't just guessed
    /// from melee ATK.</summary>
    public int MagicAtkMin => Stats.GetValueOrDefault(CharacterStat.MagicAtkMin);
    public int MagicAtkMax => Stats.GetValueOrDefault(CharacterStat.MagicAtkMax);
    /// <summary>Effective attack range in tiles, as the server publishes via CharacterStat.Range — driven
    /// by equipped weapon (bow = ~9, staff = 1 unless a magic bolt is queued, melee = 1). Defaults to 1
    /// when the server hasn't synced this stat yet; the simulator then falls back to the skill-based
    /// heuristic in <c>InferAttackRange</c>.</summary>
    public int AttackRange => Stats.GetValueOrDefault(CharacterStat.Range);

    public float HpPercent => MaxHp > 0 ? (float)Hp / MaxHp : 0f;
}
