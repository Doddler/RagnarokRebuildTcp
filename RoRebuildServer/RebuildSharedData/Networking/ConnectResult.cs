using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Networking;

public enum ServerConnectResult
{
    Success,
    FailedLogin,
    FailedCreate,
    InvalidOrExpiredToken,
    UsernameInUse,
    PasswordInsufficient,
    Banned
}