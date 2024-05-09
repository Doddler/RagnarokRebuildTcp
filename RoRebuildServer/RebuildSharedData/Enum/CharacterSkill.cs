using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum
{
    public enum CharacterSkill : byte
    {
        None,
        //swordsman
        Bash,
        Endure,
        MagnumBreak,
        Provoke,
        //mage
        FireBolt,
        ColdBolt,
        FireBall,
        FireWall,
        FrostDriver,
        LightningBolt,
        SoulStrike,
        ThunderStorm,
        SafetyWall,
        //archer
        DoubleStrafe,
        ArrowShower,
        ImproveConcentration,
        //acolyte
        Heal,
        //merchant
        Mammonite,

        //monster specific
        CallMinion,
        Haste
    }
}
