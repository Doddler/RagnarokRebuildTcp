using RoBotClient.Bot.GameData;

namespace RoBotClient.Tests;

public class MonsterSkillParseTests
{
    [Fact]
    public void GreatestGeneral_PunishesRangedAttacks_ElderWillowDoesNot()
    {
        var warps = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..", "RoRebuildServer", "GameConfig", "ServerData", "Script", "Warps"));
        var db = GameDatabase.Load("", "", warps);
        Assert.True(db.PunishesRangedAttacks("GREATEST_GENERAL"));
        Assert.False(db.PunishesRangedAttacks("ELDER_WILLOW"));
    }
}
