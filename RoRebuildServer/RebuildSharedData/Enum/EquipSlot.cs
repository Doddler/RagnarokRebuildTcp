namespace RebuildSharedData.Enum;

[Flags]
public enum HeadSlots
{
    None = 0,
    Top = 1,
    Mid = 2,
    Bottom = 4
}

public enum EquipSlot
{
    HeadTop,
    HeadMid,
    HeadBottom,
    Body,
    RightHand,
    LeftHand,
    Garment,
    Footgear,
    Accessory1,
    Accessory2,
    CostumeTop,
    CostumeMid,
    CostumeBottom,
    Ammunition,
    Weapon = RightHand,
    Shield = LeftHand,
    None = -1,
    
}