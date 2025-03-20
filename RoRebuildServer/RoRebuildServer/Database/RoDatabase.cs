using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Threading.Channels;
using Antlr4.Runtime;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RebuildSharedData.Networking;
using RebuildSharedData.Util;
using RebuildZoneServer.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Config;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.QueryData;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using Serilog;

namespace RoRebuildServer.Database;

public static class RoDatabase
{
    private static IServiceScopeFactory scopeFactory;
    private static IConfiguration config;

    private static Channel<IDbRequest> dbRequestChannel = null!;

    private static int activeDbRequestThreads;

    public struct LoginResult
    {
        public ServerConnectResult ResultCode;
        public int AccountId;
        public string AccountName;
        public byte[]? AccountToken;

        public static LoginResult FailureResult(ServerConnectResult result) => new() { ResultCode = result };
    }

    static RoDatabase()
    {
        config = ServerConfig.GetConfig();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<RoContext>(options =>
        {
            options.UseSqlite(config.GetConnectionString("DefaultConnection"));
            options.LogTo(Log.Logger.Information, LogLevel.Information, null);
        });


        services.AddIdentity<RoUserAccount, UserRole>(options =>
        {
            options.User.RequireUniqueEmail = false;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 0;
            options.Password.RequiredLength = 4; //the worst password policy known to man

        })
            .AddEntityFrameworkStores<RoContext>()
            .AddDefaultTokenProviders();

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(ServerConfig.OperationConfig.KeyPersistencePath ?? "Keys/"))
            .SetApplicationName("RagnarokRebuild");

        //services.AddScoped<UserManager<RoUserAccount>>();

        var provider = services.BuildServiceProvider();
        scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    }

    public static RoContext GetUnscopedDbContext()
    {
        ServerLogger.LogWarning("Using unscoped db context, please do not use this outside of database migrations.");
        var scope = scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<RoContext>();
    }

    public static async Task<IdentityResult> CreateUser(string userName, string password)
    {
        using var scope = scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetService<UserManager<RoUserAccount>>()!;

        var user = new RoUserAccount() { UserName = userName };
        return await userManager.CreateAsync(user, password);
    }


    //Token logins exist to allow users to login without using their password.
    //This is called remember password on the client, but passwords aren't actually remembered.
    //The token itself is just a guid that's then encoded and sent to the client.
    //For the login to succeed, we decode what they send us and check if it matches what we store.
    //Any time they log in we make a new token so any token created elsewhere becomes invalid.
    //Honestly signing the token would probably be better but I don't really want to figure that out.
    public static async Task<LoginResult> LogInWithToken(string userName, byte[] token)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoContext>();

        try
        {
            var user = await db.Users.FirstOrDefaultAsync(a => a.NormalizedUserName == userName.ToUpperInvariant());
            if (user == null || user.LoginToken == null) return LoginResult.FailureResult(ServerConnectResult.FailedLogin);
            
            var protector = scope.ServiceProvider.GetService<IDataProtectionProvider>()!;
            var code = protector.CreateProtector("RoLogin");
            var data = code.Unprotect(token);

            if (!data.SequenceEqual(user.LoginToken))
                return LoginResult.FailureResult(ServerConnectResult.InvalidOrExpiredToken);

            var newToken = Guid.NewGuid(); //lol. lmao.
            var newTokenData = newToken.ToByteArray();
            data = code.Protect(newTokenData);

            user.LoginToken = newTokenData;
            await db.SaveChangesAsync(); //we give them a new token

            return new LoginResult() { ResultCode = ServerConnectResult.Success, AccountId = user.Id, AccountName = user.UserName!, AccountToken = data };
        }
        catch (Exception e)
        {
            //an error occured, probably while un protecting the token data. Either way it's a failure.
            ServerLogger.LogWarning($"User {userName} generated an exception while attempting to sign in: {e.Message}");
            if(e.InnerException != null)
                ServerLogger.LogWarning($"Inner exception: {e.InnerException}");
            return LoginResult.FailureResult(ServerConnectResult.InvalidOrExpiredToken);
        }
    }

    public static async Task<(ServerConnectResult, LoginResult)> LogIn(string userName, string password, bool requestToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoContext>();
        var userManager = scope.ServiceProvider.GetService<UserManager<RoUserAccount>>();
        if (userManager == null) throw new Exception($"Unable to instantiate userManager!");

        var user = await userManager.FindByNameAsync(userName);
        if (user == null) return (ServerConnectResult.FailedLogin, default);
        var success = await userManager.CheckPasswordAsync(user, password);

        if (!success) return (ServerConnectResult.FailedLogin, default);

        if (!requestToken)
        {
            if (user.LoginToken != null)
            {
                user.LoginToken = null;
                await db.SaveChangesAsync(); //they don't want a token, but one is already registered. Let's get rid of it.
            }

            return (ServerConnectResult.Success, new LoginResult() { AccountId = user.Id, AccountName = user.UserName!, AccountToken = null });
        }

        var token = Guid.NewGuid(); //lol. lmao.
        var tokenBytes = token.ToByteArray();

        var protector = scope.ServiceProvider.GetService<IDataProtectionProvider>();
        if (protector == null)
        {
            ServerLogger.LogError($"Cannot get IDataProtectionProvider in order to perform LogIn task!");
            return (ServerConnectResult.ServerError, default);
        }
        var code = protector.CreateProtector("RoLogin");
        var data = code.Protect(tokenBytes);

        user.LoginToken = tokenBytes;
        await db.SaveChangesAsync();

        return (ServerConnectResult.Success, new LoginResult() { AccountId = user.Id, AccountName = user.UserName!, AccountToken = data });
    }

    public static async Task LoadCharacterSelectDataForPlayer(OutboundMessage msg, int accountId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoContext>();

        var characters = await db.Character.AsNoTracking().Where(c => c.AccountId == accountId)
            .Select(c => new QueryPlayerSummary() {Name = c.Name, Map = c.Map, CharacterSlot = c.CharacterSlot, SummaryData = c.CharacterSummary})
            .ToListAsync();

        msg.Write(characters.Count);
        foreach (var character in characters)
        {
            msg.Write(character.Name);
            msg.Write(character.CharacterSlot);
            msg.Write(character.Map ?? "");
            var summaryLength = character.SummaryData?.Length ?? 0;

            if(character.SummaryData == null || summaryLength < (int)PlayerSummaryData.SummaryDataMax)
                msg.Write(0);
            else
            {
                msg.Write((int)PlayerSummaryData.SummaryDataMax * sizeof(int));
                msg.Write(character.SummaryData, (int)PlayerSummaryData.SummaryDataMax * sizeof(int)); //set length because the buffer we have might exceed what we want to send to the client
            }
        }
    }

    public static Task ExecuteDbRequestAsync(IDbRequest request)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RoContext>();
            return request.ExecuteAsync(db);
        }
        catch (Exception ex)
        {
            ServerLogger.LogError($"Failed to execute database request {request.GetType()}: {ex}");
            return Task.CompletedTask;
        }
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
                try
                {
                    var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<RoContext>();
                    await req.ExecuteAsync(db);

                    scope.Dispose();
                }
                catch (Exception ex)
                {
                    ServerLogger.LogError($"Failed to perform database request {req.GetType()}: {ex}");
                }
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