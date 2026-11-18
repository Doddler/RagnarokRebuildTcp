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

    [Fact]
    public void AttackDelay_ComesFromMonstersCsv()
    {
        var warps = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..", "RoRebuildServer", "GameConfig", "ServerData", "Script", "Warps"));
        var db = GameDatabase.Load("", "", warps);
        Assert.Equal(0.432, db.MonsterAttackDelay(4132), 3); // Matyr
        Assert.Equal(1.78, db.MonsterAttackDelay(4185), 3);  // Zerom
    }
}
