namespace RoBotClient.Bot.Session;

public sealed class BotConfig
{
    public string ServerUri = "ws://127.0.0.1:5000/ws";
    public short ServerVersion = 8;

    public string Account = "bot_01";
    public string Password = "botbot01";

    /// <summary>The base name; the in-game character name is always prefixed with "[BOT] ".</summary>
    public string CharacterBaseName = "BotZero";

    // Default newbie build for auto-created characters (must each be 1-9 and sum to 33).
    public byte Str = 9, Agi = 8, Vit = 5, Int = 1, Dex = 9, Luk = 1;
    public bool IsMale = true;

    // Appearance for auto-created characters. The server validates ranges (hair style 0-19, hair color 0-8).
    public int HairStyle = 0;
    public int HairColor = 0;

    public string CharacterName => $"[BOT] {CharacterBaseName}";
}
