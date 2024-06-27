using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum
{
    public enum ItemType
    {
        None,
        UseItem,
        Equipment,
        Ammunition
    }

    [Flags]
    public enum EquipPosition
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
    }
}
