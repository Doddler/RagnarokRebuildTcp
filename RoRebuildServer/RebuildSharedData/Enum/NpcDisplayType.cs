namespace RebuildSharedData.Enum;

public enum NpcDisplayType : byte
{
    Sprite,
    Effect,
    MaskedEffect,
    VendingProxy
}

public enum NpcEffectType : byte
{
    None,
    Firewall,
    Pneuma,
    SafetyWall,
    WarpPortalOpening,
    WarpPortal,
    MapWarp,
    Demonstration,
    WaterBall,
    Sanctuary,
    MagnusExorcismus,
    AnkleSnare,

    //custom
    LightOrb,
}