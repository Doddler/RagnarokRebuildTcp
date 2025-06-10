using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Database.Domain;

namespace RoRebuildServer.Database.Requests;

public class LoadScriptGlobalVariableRequest : IDbRequest
{
    public List<ScriptGlobalVar> ScriptVariables;

    public async Task ExecuteAsync(RoContext dbContext)
    {
        ScriptVariables = await dbContext.ScriptGlobalVars.AsNoTracking().ToListAsync();
    }
}