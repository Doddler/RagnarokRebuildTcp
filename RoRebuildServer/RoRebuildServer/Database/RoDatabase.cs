using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RoRebuildServer.Data;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Database;

public static class RoDatabase
{
    private static IServiceScopeFactory scopeFactory;
    private static IConfiguration config;

    private static Channel<IDbRequest> dbRequestChannel;
    private static Thread dbProcessThread;

    private static int activeDbRequestThreads;

    static RoDatabase()
    {
        config = ServerConfig.GetConfig();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<RoContext>(options =>
        {
            options.UseSqlite(config.GetConnectionString("DefaultConnection"));
        });

        var provider = services.BuildServiceProvider();
        scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        
    }

    public static RoContext GetUnscopedDbContext()
    {
        ServerLogger.LogWarning("Using unscoped db context, please do not use this outside of database migrations.");
        var scope = scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<RoContext>();
    }

    public static Task ExecuteDbRequestAsync(IDbRequest request)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoContext>();
        return request.ExecuteAsync(db);
    }

    public static void EnqueueDbRequest(IDbRequest request)
    {
        if (!dbRequestChannel.Writer.TryWrite(request))
            ServerLogger.LogError("Failed to enque a database request!");
    }

    private static async Task PerformDatabaseRequests()
    {
        Interlocked.Increment(ref activeDbRequestThreads);

        while (await dbRequestChannel.Reader.WaitToReadAsync())
        {
            while (dbRequestChannel.Reader.TryRead(out var req))
            {
                var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RoContext>();
                await req.ExecuteAsync(db);

                scope.Dispose();
            }
        }

        Interlocked.Decrement(ref activeDbRequestThreads);
        ServerLogger.Log("Leaving database update loop.");
    }

    private static void StartDbRequestThread()
    {
        Task.Run(PerformDatabaseRequests).ConfigureAwait(false);
    }

    public static async Task WaitForQueueClear()
    {
        dbRequestChannel.Writer.Complete();

        if (activeDbRequestThreads > 0)
            ServerLogger.Log("Waiting for database queue to clear.");

        while (activeDbRequestThreads > 0)
        {
            await Task.Delay(100);
        }
        ServerLogger.Log("Database queue is clear.");
    }

    public static async Task Shutdown()
    {
        await WaitForQueueClear();
    }

    public static void Initialize()
    {
        ServerLogger.Log("Initializing Character Database.");

        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<RoContext>();
        db.Database.Migrate();

        dbRequestChannel = Channel.CreateUnbounded<IDbRequest>(new UnboundedChannelOptions
            { SingleReader = true, SingleWriter = false });

        //dbProcessThread = new Thread(StartDbRequestThread);
        //dbProcessThread.Priority = ThreadPriority.Normal;
        //dbProcessThread.Start();
        StartDbRequestThread();
    }
}

//this guy allows us to create migrations via entity framework command line tools
public class DesignTimeContextFactory : IDesignTimeDbContextFactory<RoContext>
{
    public RoContext CreateDbContext(string[] args)
    {
        return RoDatabase.GetUnscopedDbContext();
    }
}