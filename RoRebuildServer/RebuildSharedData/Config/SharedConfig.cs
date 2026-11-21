namespace RebuildSharedData.Config;

public static class SharedConfig
{
    public const int MaxPathLength = 16;
    public const int MinPlayerName = 2;
    public const int MaxPlayerName = 16; //if you change this you'll need to add a database migration for it
    public const int MinPartyName = 2;
    public const int MaxPartyName = 16; //if you change this you'll need to add a database migration for it
    public const int MaxPartySize = 5;
}