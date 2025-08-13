namespace RebuildSharedData.Enum;

public enum EquipmentChangeAction
{
    Equip,
    Unequip,
    UnequipAll
}

public enum EquipChangeResult
{
    Success,
    InvalidItem,
    LevelTooLow,
    NotApplicableJob,
    InvalidPosition,
    AlreadyEquipped
}