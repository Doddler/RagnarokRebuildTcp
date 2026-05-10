namespace RebuildSharedData.Enum;

[Flags]
public enum StatusUpdateMode : byte
{
    Default = 0,
    OnCalculateDamageTaken = 1 << 1,
    OnPreCalculateDamageDealt = 1 << 2,
    OnTakeDamage = 1 << 3,
    OnDealDamage = 1 << 4,
    OnUpdate = 1 << 5,
    OnChangeEquipment = 1 << 6,
    OnMove = 1 << 7,

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