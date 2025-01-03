namespace RebuildSharedData.Enum;

public enum NpcInteractionResult : byte
{
    None,
    WaitForContinue,
    WaitForInput,
    WaitForTime,
    WaitForShop,
    WaitForRefine,
    WaitForStorageAccess,
    WaitForStorage,
    EndInteraction
}