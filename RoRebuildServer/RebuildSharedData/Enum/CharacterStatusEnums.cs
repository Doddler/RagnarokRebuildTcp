using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum;

public enum StatusEffectUpdateFlags : byte
{
    None = 0,
    OnApply = 1,
    OnRemove = 2,
    TestApply = 4,
    //------combined-------
    SupportBuff = 3, //OnApply + OnRemove
    OffensiveDebuff = 7 //TestApply + OnApply + OnRemove
}

public enum StatusEffectClass : byte
{
    None,
    Buff,
    Debuff
}

public enum StatusClientVisibility : byte
{
    None,
    Owner,
    Ally,
    Everyone
}