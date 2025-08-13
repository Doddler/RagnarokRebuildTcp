namespace RebuildSharedData.Networking;

public enum ServerConnectResult
{
    Success,
    FailedLogin,
    FailedCreate,
    InvalidOrExpiredToken,
    UsernameInUse,
    PasswordInsufficient,
    Banned,
    ServerError
}