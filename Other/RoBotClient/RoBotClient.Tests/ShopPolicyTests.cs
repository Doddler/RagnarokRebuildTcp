using RoBotClient.Bot.Behavior;

namespace RoBotClient.Tests;

public class ShopPolicyTests
{
    private static BotBehaviorConfig Cfg() => new() { SellWeightPercent = 0.9f, SellItemStacks = 30, RestockItemId = 0, JunkSellCount = 0 };

    [Theory]
    [InlineData(899, false)]
    [InlineData(900, true)]
    [InlineData(901, true)]
    public void WeightThreshold_IsInclusive(int weight, bool expected)
        => Assert.Equal(expected, ShopPolicy.ShouldStartTrip(weight, 1000, 0, 0, Cfg(), () => 0));

    [Fact]
    public void ZeroMaxWeight_NeverTriggersOnWeight()
        => Assert.False(ShopPolicy.ShouldStartTrip(5000, 0, 0, 0, Cfg(), () => 0));

    [Fact]
    public void RestockBelowTarget_Triggers()
    {
        var cfg = Cfg();
        cfg.RestockItemId = 501;
        cfg.RestockBelow = 10;
        Assert.True(ShopPolicy.ShouldStartTrip(0, 1000, 0, 9, cfg, () => 0));
        Assert.False(ShopPolicy.ShouldStartTrip(0, 1000, 0, 10, cfg, () => 0));
    }

    [Fact]
    public void SaleableCount_OnlyEvaluatedWhenCheaperThresholdsMiss()
    {
        var cfg = Cfg();
        cfg.JunkSellCount = 2;
        var calls = 0;
        Assert.True(ShopPolicy.ShouldStartTrip(0, 1000, 30, 0, cfg, () => { calls++; return 0; }));
        Assert.Equal(0, calls);
        Assert.True(ShopPolicy.ShouldStartTrip(0, 1000, 0, 0, cfg, () => { calls++; return 2; }));
        Assert.Equal(1, calls);
    }
}
