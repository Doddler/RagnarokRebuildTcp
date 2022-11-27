namespace RebuildSharedData.Config;

public static class SharedConfig
{
    public const int MaxPathLength = 16;
    public const int MaxPlayerName = 40; //if you change this you'll need to add a database migration for it
}