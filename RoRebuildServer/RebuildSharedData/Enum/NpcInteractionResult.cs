namespace RebuildSharedData.Enum;

public enum NpcInteractionResult : byte
{
    None,
    WaitForContinue,
    WaitForInput,
    WaitForItemPrompt,
    WaitForTime,
    WaitForShop,
    WaitForTrade,
    WaitForRefine,
    WaitForStorageAccess,
    WaitForStorage,
    EndInteraction
}