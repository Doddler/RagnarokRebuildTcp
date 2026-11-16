using RoBotClient.Bot.Behavior;

namespace RoBotClient.Tests;

public class ShopPolicyTests
{
    private static BotBehaviorConfig Cfg() => new() { SellWeightPercent = 0.9f, RestockItemId = 0, JunkSellCount = 8 };

    [Theory]
    [InlineData(899, 4, false)]
    [InlineData(900, 4, true)]
    [InlineData(900, 3, false)] // heavy, but barely anything would sell: not worth the walk
    [InlineData(900, 0, false)]
    public void Weight_TriggersOnlyWithEnoughToSell(int weight, int saleable, bool expected)
        => Assert.Equal(expected, ShopPolicy.ShouldStartTrip(weight, 1000, int.MaxValue, Cfg(), () => saleable));

    [Fact]
    public void ManyKeptStacks_DoNotTrigger() // the old loop: 31 kept stacks (food, cards, equipped gear) + 7 junk
        => Assert.False(ShopPolicy.ShouldStartTrip(100, 1000, int.MaxValue, Cfg(), () => 7));

    [Fact]
    public void RestockBelowTarget_Triggers()
    {
        var cfg = Cfg();
        cfg.RestockItemId = 501;
        cfg.RestockBelow = 10;
        Assert.True(ShopPolicy.ShouldStartTrip(0, 1000, 9, cfg, () => 0));
        Assert.False(ShopPolicy.ShouldStartTrip(0, 1000, 10, cfg, () => 0));
    }
}
