namespace RebuildSharedData.Enum;

public enum NpcInteractionResult : byte
{
    None,
    WaitForContinue,
    WaitForInput,
    WaitForTime,
    EndInteraction
}