using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RoBotClient.Web.Mcp;

/// <summary>MCP tools to persist and resume per-bot build plans (markdown), keyed by bot/character name.</summary>
[McpServerToolType]
public static class McpBuildTools
{
    [McpServerTool(Name = "read_build"),
     Description("Read a saved build plan (markdown) for a bot. 'name' is usually the bot's character name. Returns { found, content }; found=false means nothing has been saved under that name yet.")]
    public static object ReadBuild(BuildStore store, string name)
    {
        var content = store.Read(name) ?? "";
        return new { found = !string.IsNullOrEmpty(content), content };
    }

    [McpServerTool(Name = "write_build"),
     Description("Save/overwrite a build plan (markdown) for a bot so it can be resumed in a later session. 'name' is usually the bot's character name; 'markdown' is the full plan document.")]
    public static string WriteBuild(BuildStore store, string name, string markdown)
    {
        store.Write(name, markdown);
        return $"Saved build '{name}' ({markdown?.Length ?? 0} chars).";
    }

    [McpServerTool(Name = "list_builds"), Description("List the names of all saved build plans.")]
    public static object ListBuilds(BuildStore store) => store.List();
}
