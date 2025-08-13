using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using CsvHelper.Configuration.Attributes;

namespace RoRebuildServer.Data.CsvDataTypes;


public class CsvItemRegular
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required int Price { get; set; }
    public required int Weight { get; set; }
    public required string Usage { get; set; }
    public required string Sprite { get; set; }
}

public class CsvItemUseable
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required int Weight { get; set; }
    public required int Price { get; set; }
    public required ItemUseType UseMode { get; set; }
    public required string UseEffect { get; set; }
    public required string Sprite { get; set; }
}

public class CsvItemAmmo
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required int Attack { get; set; }
    public required int Price { get; set; }
    public required int Weight { get; set; }
    public required AttackElement Property { get; set; }
    public required int MinLvl { get; set; }
    public required AmmoType Type { get; set; }
    public required string Sprite { get; set; }
}

public class CsvItemCard
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required int Price { get; set; }
    public required int Weight { get; set; }
    public required EquipPosition EquipableSlot { get; set; }
    public required string Prefix { get; set; }
    public required string Postfix { get; set; }
    public required string Sprite { get; set; }
}

public class CsvItemWeapon
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required int Attack { get; set; }
    public required int Slot { get; set; }
    public required string Type { get; set; }
    public required int Range { get; set; }
    public required int Price { get; set; }
    public required int Weight { get; set; }
    public required WeaponPosition Position { get; set; }
    public required AttackElement Property { get; set; }
    public required int MinLvl { get; set; }
    public required int Rank { get; set; }
    public required string EquipGroup { get; set; }
    public required string Refinable { get; set; }
    public required string Breakable { get; set; }
    public required string Sprite { get; set; }
    public required string WeaponSprite { get; set; }
}

public class CsvItemEquipment
{
    public required int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required int Defense { get; set; }
    public required int MagicDef { get; set; }
    public required int Slot { get; set; }
    public required int Price { get; set; }
    public required int Weight { get; set; }
    public required EquipPosition Type { get; set; }
    public HeadgearPosition Position { get; set; }
    [Optional]
    public CharacterElement? Property { get; set; }
    public required int MinLvl { get; set; }
    public required string EquipGroup { get; set; }
    public required string Refinable { get; set; }
    public required string Breakable { get; set; }
    public required string Sprite { get; set; }
    public required string DisplaySprite { get; set; }

}

[Flags]
public enum HeadgearPosition : byte
{
    [Name("")]
    None = 0,
    Top = 1,
    Mid = 2,
    Bottom = 4,
    TopMid = Top | Mid,
    TopBottom = Top | Bottom,
    MidBottom = Mid | Bottom,
    All = Top | Mid | Bottom
}

public class CsvItemBoxSummonEntry
{
    public required string Type { get; set; }
    public required string Code { get; set; }
    public required int Chance { get; set; }
}

public class CsvItemMonsterSummonEntry
{   
    public required string Type { get; set; }
    public required string Monster { get; set; }
    public required int Chance { get; set; }
}

public class CsvNonCardPrefixes
{
    public required string Code { get; set; }
    public string? Prefix { get; set; }
    public string? Postfix { get; set; }
}