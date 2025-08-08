using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum;

public enum PartyUpdateType
{
    AddPlayer,
    RemovePlayer,
    UpdatePlayer,
    LogIn,
    LogOut,
    ChangeLeader,
    LeaveParty,
    DisbandParty,
    UpdateHpSp,
    UpdateMap,
}

public enum PartyClientAction
{
    LeaveParty,
    ChangeLeader,
    RemovePlayer,
    DisbandParty
}
