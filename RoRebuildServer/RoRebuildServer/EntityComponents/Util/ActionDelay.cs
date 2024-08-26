using RoRebuildServer.Logging;

namespace RoRebuildServer.EntityComponents.Util;

public enum CooldownActionType
{
    Click,
    FaceDirection,
    SitStand,
    StopAction,
    UseItem,
    Teleport
}

public static class ActionDelay
{
    public const float ClickCooldownTime = 0.25f;
    public const float FaceDirectionCooldown = 0.10f;
    public const float SitStandCooldown = 0.25f;
    private const float StopActionCooldown = 0.20f;
    private const float UseItemCooldown = 0.10f;
    private const float TeleportCooldown = 0.5f;

    public static float CooldownTime(CooldownActionType type)
    {
        switch (type)
        {
            case CooldownActionType.Click: return ClickCooldownTime;
            case CooldownActionType.FaceDirection: return FaceDirectionCooldown;
            case CooldownActionType.SitStand: return SitStandCooldown;
            case CooldownActionType.StopAction: return StopActionCooldown;
            case CooldownActionType.UseItem: return UseItemCooldown;
            case CooldownActionType.Teleport: return TeleportCooldown;
            default:
                ServerLogger.LogWarning($"Could not get ActionDelay Cooldown for type {type} (value {(int)type}.");
                return 0.3f;
        }
    }
}