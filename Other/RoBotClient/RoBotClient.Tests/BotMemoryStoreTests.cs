using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.Manager;

namespace RoBotClient.Tests;

public class BotMemoryStoreTests
{
    [Fact]
    public void Memory_survives_a_new_store_instance()
    {
        var path = Path.Combine(Path.GetTempPath(), "bot-memory-" + Guid.NewGuid() + ".json");
        var until = DateTime.UtcNow.AddMinutes(30);
        new BotMemoryStore(path).Save("Bot5", new BotMemory
        {
            MapBannedUntil = { ["mjolnir_05"] = until },
            MapDeaths = { ["mjolnir_05"] = new() { DateTime.UtcNow } },
            AvoidClasses = { [1095] = until },
            KillSplitMap = "pay_fild10",
            KillSplits = { new ActivityTime(12, 8.6, 3.9, 0) },
        });

        var read = new BotMemoryStore(path).Get("bot5"); // names are case-insensitive, like the other stores
        Assert.NotNull(read);
        Assert.Equal(until, read.MapBannedUntil["mjolnir_05"]);
        Assert.Single(read.MapDeaths["mjolnir_05"]);
        Assert.Equal(until, read.AvoidClasses[1095]);
        Assert.Equal("pay_fild10", read.KillSplitMap);
        Assert.Equal(new ActivityTime(12, 8.6, 3.9, 0), Assert.Single(read.KillSplits));
        File.Delete(path);
    }
}
