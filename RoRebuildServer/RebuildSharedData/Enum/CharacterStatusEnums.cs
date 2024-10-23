using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum;

[Flags]
public enum StatusUpdateMode : byte
{
    Default = 0,
    OnTakeDamage = 1,
    OnDealDamage = 2,
    OnUpdate = 4,
    OnChangeEquipment = 8,
}

public enum StatusUpdateResult : byte
{
    Continue,
    EndStatus
}

public enum StatusEffectClass : byte
{
    None,
    Buff,
    Debuff
}

[Flags]
public enum StatusEffectFlags : byte
{
    None = 0,
    StayOnClear = 1,
    NoSave = 2,
}

public enum StatusClientVisibility : byte
{
    None,
    Owner,
    Ally,
    Everyone
}