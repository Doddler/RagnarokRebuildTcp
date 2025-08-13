namespace RebuildSharedData.Enum;

[Flags]
public enum StatusUpdateMode : byte
{
    Default = 0,
    OnCalculateDamageTaken = 1,
    OnPreCalculateDamageDealt = 2,
    OnTakeDamage = 4,
    OnDealDamage = 8,
    OnUpdate = 16,
    OnChangeEquipment = 32,
    OnMove = 64,
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