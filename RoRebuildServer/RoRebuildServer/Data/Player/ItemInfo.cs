using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.EntityComponents.Items;

namespace RoRebuildServer.Data.Player;

public class ItemInfo
{
    public int Id;
    public string Code = null!;
    public string Name = null!;
    public int Weight;
    public int Price;
    public bool IsUnique;
    public ItemClass ItemClass;
    public ItemInteractionBase? Interaction = null!;
}

public class WeaponInfo
{
    public int Attack;
    public int Range;
    public int CardSlots;
    public int WeaponClass;
    public int WeaponLevel;
    public bool IsTwoHanded;
    public AttackElement Element;
    public int MinLvl;
    public required string EquipGroup;
    public bool IsRefinable;
    public bool IsBreakable;
}

public class ArmorInfo
{
    public int Defense;
    public int MagicDefense;
    public int CardSlots;
    public EquipPosition EquipPosition;
    public HeadgearPosition HeadPosition;
    public CharacterElement Element;
    public int MinLvl;
    public required string EquipGroup;
    public bool IsRefinable;
    public bool IsBreakable;
}

public class CardInfo
{
    public EquipPosition EquipPosition;
}

public class AmmoInfo
{
    public int Attack;
    public AmmoType Type;
    public AttackElement Element;
    public int MinLvl;
}

public struct UseItemInfo
{
    public ItemUseType UseType;
    public int Effect;
}