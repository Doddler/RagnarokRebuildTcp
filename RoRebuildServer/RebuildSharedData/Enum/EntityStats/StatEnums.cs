using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum.EntityStats;


public enum PlayerStat
{
    Level,
    Status,
    Gender,
    Head,
    HairId,
    Job,
    Exp,
    Experience = Exp,
    JobExp,
    JobExperience = JobExp,
    Str,
    Strength = Str,
    Agi,
    Agility = Agi,
    Vit,
    Vitality = Vit,
    Int,
    Intelligence = Int,
    Dex,
    Dexterity = Dex,
    Luk,
    Luck = Luk,
    Hp,
    Mp,
    SkillPoints,
    StatPoints,
    Zeny,
    PlayerStatsMax
}

public enum CharacterStat
{
    Level,
    Hp,
    MaxHp,
    Sp,
    MaxSp,
    Range,
    Attack,
    Attack2,
    MagicAtkMin,
    MagicAtkMax,
    Str,
    Strength = Str,
    Agi,
    Agility = Agi,
    Vit,
    Vitality = Vit,
    Int,
    Intelligence = Int,
    Dex,
    Dexterity = Dex,
    Luk,
    Luck = Luk,
    Def,
    Defense = Def,
    MDef,
    MagicDefense = MDef,
    AspdBonus,
    MoveSpeedBonus,
    AddStr,
    AddAgi,
    AddDex,
    AddInt,
    AddVit,
    AddLuk,
    AddDef,
    AddMDef,
    AddSoftDef,
    AddSoftMDef,
    AddFlee,
    AddHit,
    AddMaxHp,
    AddMaxSp,
    AddAttackPercent,
    AddMagicAttackPercent,
    AddAttackPower,
    AddMagicAttackPower,
    AddDefPercent,
    AddMDefPercent,
    Disabled,
    OverrideElement,
    MonsterStatsMax, //any stats after this are stats only players will be able to hold.

    WeightCapacity,
    WeaponMastery,
    EquipmentRefineDef,
    DoubleAttackChance,
    PercentVsDemon,
    PercentVsUndead,
    ReductionFromDemon,
    ReductionFromUndead,
    AddSpRecoveryPercent,
    AddHpRecoveryPercent,

    CharacterStatsMax,
}

public enum TimingStat
{
    MoveSpeed,
    AttackMotionTime,
    AttackDelayTime,
    HitDelayTime,
    SpriteAttackTiming,
    TimingStatsMax
}

public static class PlayerClientStatusDef
{
    public static PlayerStat[] PlayerUpdateData = new[]
    {
        PlayerStat.Level,
        PlayerStat.Zeny,
        PlayerStat.Str,
        PlayerStat.Agi,
        PlayerStat.Int,
        PlayerStat.Vit,
        PlayerStat.Dex,
        PlayerStat.Luk,
        PlayerStat.SkillPoints,
        PlayerStat.StatPoints
    };

    public static CharacterStat[] PlayerUpdateStats = new[]
    {
        CharacterStat.Hp,
        CharacterStat.MaxHp,
        CharacterStat.Sp,
        CharacterStat.MaxSp,
        CharacterStat.AddStr,
        CharacterStat.AddAgi,
        CharacterStat.AddInt,
        CharacterStat.AddVit,
        CharacterStat.AddDex,
        CharacterStat.AddLuk,
        CharacterStat.Def,
        CharacterStat.MDef,
        CharacterStat.Attack,
        CharacterStat.Attack2,
        CharacterStat.MagicAtkMin,
        CharacterStat.MagicAtkMax,
        CharacterStat.AddFlee,
        CharacterStat.AddHit,
        CharacterStat.WeightCapacity,
    };
}