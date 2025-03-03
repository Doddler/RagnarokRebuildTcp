using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum;

[Flags]
public enum StatusUpdateMode : byte
{
    Default = 0,
    OnCalculateDamageTaken = 1,
    OnTakeDamage = 2,
    OnDealDamage = 4,
    OnUpdate = 8,
    OnChangeEquipment = 16,
    OnMove = 32,
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