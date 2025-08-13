namespace RebuildSharedData.Enum
{
    public enum ItemType : byte
    {
        NoItem,
        RegularItem,
        UniqueItem
    }

    public enum ItemUseType : byte
    {
        NotUsable,
        Use,
        UseOnAlly,
        UseOnEnemy
    }

    public enum ItemClass : byte
    {
        None,
        Useable,
        Weapon,
        Equipment,
        Ammo,
        Card,
        Etc
    }

    public enum WeaponPosition : byte
    {
        MainHand,
        BothHands
    }

    public enum AmmoType : byte
    {
        Arrow,
        ThrowingDagger,
        Bullet,
        Grenade,
        Shuriken,
        Kunai
    }

    [Flags]
    public enum ItemFlags : byte
    {
        None = 0,
        Unique = 1,
        CanEquip = 2,
        PlayerBound = 4,
        NoSell = 8
    }

    [Flags]
    public enum EquipPosition : short
    {
        HeadUpper = 1,
        HeadMid = 2,
        HeadLower = 4,
        Body = 8,
        MainHand = 16,
        OffHand = 32,
        Garment = 64,
        Footgear = 128,
        Accessory = 256,
        Weapon = MainHand,
        Shield = OffHand,
        Armor = Body,
        Boots = Footgear,
        BothHands = MainHand | OffHand,
        AnyHand = MainHand | OffHand,
        Headgear = HeadUpper | HeadMid | HeadLower,
        Any = HeadUpper | HeadMid | HeadLower | Body | MainHand | OffHand | Garment | Footgear | Accessory
    }

    
}
