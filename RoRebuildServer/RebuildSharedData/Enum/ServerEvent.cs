﻿using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Enum;

public enum ServerEvent
{
    None = 0,
    TradeSuccess,
    NoAmmoEquipped,
    WrongAmmoEquipped,
    OutOfAmmo,
    GetZeny,
    GetMVP,
    EligibleForJobChange,
    MemoLocationSaved,
}

public enum ServerResult
{
    PartyInviteSent,
    InviteFailedSenderNoBasicSkill,
    InviteFailedRecipientNoBasicSkill,
    InviteFailedAlreadyInParty,
}