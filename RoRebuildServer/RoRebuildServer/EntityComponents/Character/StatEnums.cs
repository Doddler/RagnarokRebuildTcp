namespace RoRebuildServer.EntityComponents.Character;

public enum PlayerStat
{
    Level,
    Status,
    Gender,
    Head,
    Job,
    Exp,
    Experience = Exp,
    JobExp,
    JobExperience = JobExp,
    Str,
    Strength = Str,
    Dex,
    Dexterity = Dex,
    Agi,
    Agility = Agi,
    Int,
    Intelligence = Int,
    Vit,
    Vitality = Vit,
    Luk,
    Luck = Luk,
    Facing,
    Hp,
    Mp,
    PlayerStatsMax
}

public enum CharacterStat
{
    Level,
    Hp,
    MaxHp,
    Mp,
    MaxMp,
    Range,
    Attack,
    Attack2,
    Str,
    Strength = Str,
    Dex,
    Dexterity = Dex,
    Agi,
    Agility = Agi,
    Int,
    Intelligence = Int,
    Vit,
    Vitality = Vit,
    Luk,
    Luck = Luk,
    Def,
    Defense = Def,
    MDef,
    MagicDefense = MDef,
    CharacterStatsMax
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