using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Database.Domain;

namespace RoRebuildServer.Database.Requests;

public class SaveScriptGlobalVariableRequest(ScriptGlobalVar globalVar) : IDbRequest
{
    public async Task ExecuteAsync(RoContext dbContext)
    {
        var existing = await dbContext.ScriptGlobalVars.FirstOrDefaultAsync(existing => existing.VariableName == globalVar.VariableName);
        if (existing != null)
        {
            existing.IntValue = globalVar.IntValue;
            existing.StringValue = globalVar.StringValue;
        }
        else
            dbContext.Add(globalVar);

        await dbContext.SaveChangesAsync();
    }
}