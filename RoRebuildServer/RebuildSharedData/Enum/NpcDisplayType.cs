using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum;

public enum NpcDisplayType : byte
{
    Sprite,
    Effect,
    MaskedEffect
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

    //custom
    LightOrb,
}