using RoRebuildServer.Logging;

namespace RoRebuildServer.EntityComponents.Util;

public enum InputActionCooldownType
{
    Click,
    FaceDirection,
    MoveInDirection,
    SitStand,
    StopAction,
    UseItem,
    PickUpItem,
    Teleport
}

public static class InputActionDelay
{
    public const float ClickCooldownTime = 0.25f;
    public const float FaceDirectionCooldown = 0.10f;
    public const float MoveInDirectionCooldown = 0.15f;
    public const float SitStandCooldown = 0.25f;
    private const float StopActionCooldown = 0.20f;
    private const float UseItemCooldown = 0.20f;
    private const float PickUpItemCooldown = 0.20f;
    private const float TeleportCooldown = 0.50f;

    public static float CooldownTime(InputActionCooldownType type)
    {
        switch (type)
        {
            case InputActionCooldownType.Click: return ClickCooldownTime;
            case InputActionCooldownType.FaceDirection: return FaceDirectionCooldown;
            case InputActionCooldownType.MoveInDirection: return MoveInDirectionCooldown;
            case InputActionCooldownType.SitStand: return SitStandCooldown;
            case InputActionCooldownType.StopAction: return StopActionCooldown;
            case InputActionCooldownType.UseItem: return UseItemCooldown;
            case InputActionCooldownType.PickUpItem: return PickUpItemCooldown;
            case InputActionCooldownType.Teleport: return TeleportCooldown;
            default:
                ServerLogger.LogWarning($"Could not get ActionDelay Cooldown for type {type} (value {(int)type}.");
                return 0.3f;
        }
    }
}