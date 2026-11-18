using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using RebuildSharedData.Networking;
using RoBotClient.Bot.Manager;
using RoBotClient.Bot.Protocol;

namespace RoBotClient.Web.Features.Watch;

/// <summary>
/// Watch a bot through the real Unity client, without touching the game server: log the client in with server
/// ws://127.0.0.1:&lt;dashboard port&gt;/spectate and the bot's character name as the username (any password).
/// This endpoint plays the server: it answers the login with the bot's EnterServer and then mirrors the bot's
/// packets (PovRecorder). Loopback only. The Watch tab embeds a WebGL build of the client served from /watch-client
/// (built by RebuildClient/Assets/Scripts/Editor/WatchWebGLBuild.cs; it logs in from ?watch=BotName).
/// To remove the feature: delete this folder and the app.MapWatch() line in Program.cs, the Watch link in NavMenu,
/// and BotSession.PacketTap.
/// </summary>
public static class WatchEndpoint
{
    public const string Path = "/spectate";
    public const string ClientPath = "/watch-client";

    /// <summary>The WebGL build's folder (Watch:ClientDir in config, else RebuildClient/Build/WatchWebGL in this repo).</summary>
    public static string ClientDir(IConfiguration config, IWebHostEnvironment env) =>
        System.IO.Path.GetFullPath(config["Watch:ClientDir"] ?? System.IO.Path.Combine(env.ContentRootPath, "../../../RebuildClient/Build/WatchWebGL"));

    public static void MapWatch(this WebApplication app)
    {
        PovRecorder.Install();
        app.UseWebSockets();

        var clientDir = ClientDir(app.Configuration, app.Environment);
        Directory.CreateDirectory(clientDir); // the provider needs it to exist; the build fills it later
        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = ClientPath,
            FileProvider = new PhysicalFileProvider(clientDir),
            ContentTypeProvider = new FileExtensionContentTypeProvider(), // .wasm, .js, .json, .html
            ServeUnknownFileTypes = true, // .data, .bundle, .hash
            DefaultContentType = "application/octet-stream",
        });
        // The build's own index.html is the public site's page (fixed 960x600 canvas, footer, links), which the dashboard's
        // iframe crops. This page loads the same build into a canvas that fills the frame; Unity renders at that size.
        app.MapGet(ClientPath + "/watch.html", () =>
        {
            var index = System.IO.Path.Combine(clientDir, "index.html");
            var build = File.Exists(index) ? System.Text.RegularExpressions.Regex.Match(File.ReadAllText(index), "buildUrl = \"([^\"]+)\"").Groups[1].Value : "";
            return Results.Content($$"""
                <!DOCTYPE html><html><head><meta charset="utf-8"><title>Watch</title>
                <style>html,body{margin:0;height:100%;overflow:hidden;background:#231F20}canvas{display:block;width:100%;height:100%}</style>
                </head><body><canvas id="c" tabindex="-1"></canvas>
                <script src="{{build}}/WatchWebGL.loader.js"></script>
                <script>createUnityInstance(document.getElementById("c"), {
                  dataUrl: "{{build}}/WatchWebGL.data", frameworkUrl: "{{build}}/WatchWebGL.framework.js", codeUrl: "{{build}}/WatchWebGL.wasm",
                  streamingAssetsUrl: "StreamingAssets", companyName: "Ragnarok", productName: "RebuildClient", productVersion: "0.1",
                }).catch(e => document.body.textContent = e);</script></body></html>
                """, "text/html");
        });
        app.Map(Path, async (HttpContext ctx, BotManager bots) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest || ctx.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var ct = ctx.RequestAborted;
            var name = await ReadLoginNameAsync(socket, ct);
            if (name == null)
                return;

            var bot = bots.GetSnapshots().FirstOrDefault(s => s.InGame && string.Equals(s.CharacterName, name, StringComparison.OrdinalIgnoreCase));
            var recorder = bot != null && bots.GetSession(bot.Id) is { } session ? PovRecorder.For(session) : null;
            if (recorder == null)
            {
                await DenyAsync(socket, $"No bot named '{name}' is in game to watch.", ct);
                return;
            }

            await recorder.WatchAsync(socket, ct);
        });
    }

    /// <summary>The Unity client's login packet (BinaryWriter): version, isNewAccount, isToken, requestToken, username, password.</summary>
    private static async Task<string?> ReadLoginNameAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, ct);
        if (result.MessageType == WebSocketMessageType.Close)
            return null;
        using var br = new BinaryReader(new MemoryStream(buffer, 0, result.Count));
        br.ReadInt16();
        br.ReadBoolean();
        br.ReadBoolean();
        br.ReadBoolean();
        return br.ReadString().Trim();
    }

    private static async Task DenyAsync(WebSocket socket, string message, CancellationToken ct)
    {
        var w = new PacketWriter();
        w.WritePacketType(PacketType.ConnectionDenied);
        w.Write(message);
        await socket.SendAsync(w.AsMemory(), WebSocketMessageType.Binary, true, ct);
        await Task.Delay(200, ct); // let the message land before the close
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "denied", ct);
    }
}
