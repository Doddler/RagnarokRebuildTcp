using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.Manager;

namespace RoBotClient.Tests;

public class MapAuditStoreTests
{
    [Fact]
    public void Move_round_trips_with_its_breakdown()
    {
        var dir = Path.Combine(Path.GetTempPath(), "map-audit-" + Guid.NewGuid());
        var store = new MapAuditStore(dir);
        var predicted = new MapBreakdown("pay_fild07", 9324, false, 2, 107, new ActivityTime(12, 8.6, 15.1, 0), 5.6, 20000, 0.37, 0, 0, 0);
        var left = new MapBreakdown("pay_fild06", 9803, true, 0, 82, new ActivityTime(12, 8.6, 3.9, 0.5), 5.6, 20000, 1, 40, 1200, 3280);
        store.Append("Bot1", new MapMoveAudit(DateTime.UtcNow, 30, "pay_fild06", "pay_fild07", "why", predicted, left, 0.37));
        store.Append("Bot2", new MapMoveAudit(DateTime.UtcNow, 30, "a", "b", "why", predicted, null, 0.37));

        var read = Assert.Single(store.Get("Bot1"));
        Assert.Equal(predicted, read.Predicted);
        Assert.Equal(left, read.Left);
        Assert.Equal(2, store.All().Count);
        Directory.Delete(dir, true);
    }
}
