using RoRebuildServer.Database;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.Requests;

namespace RoRebuildServer.Data.Scripting;

public static class ScriptGlobalManager
{
    private static Dictionary<string, ScriptGlobalVar> scriptGlobals = new();

    private static ReaderWriterLockSlim varLock = new();

    public static async Task LoadGlobalsFromDatabase()
    {
        var req = new LoadScriptGlobalVariableRequest();

        await RoDatabase.ExecuteDbRequestAsync(req);

        foreach (var v in req.ScriptVariables)
            scriptGlobals.Add(v.VariableName, v);
    }

    public static int IntValue(string varName)
    {
        varLock.EnterReadLock();

        var intVal = 0;
        if (scriptGlobals.TryGetValue(varName, out var val))
            intVal = val.IntValue;

        varLock.ExitReadLock();
        return intVal;
    }
    
    public static string StringValue(string varName)
    {
        varLock.EnterReadLock();

        var strVal = "";
        if (scriptGlobals.TryGetValue(varName, out var val) && val.StringValue != null)
            strVal = val.StringValue;

        varLock.ExitReadLock();
        return strVal;
    }

    public static void SetInt(string varName, int intVal)
    {
        varLock.EnterWriteLock();

        if (scriptGlobals.TryGetValue(varName, out var gVal))
        {
            gVal.IntValue = intVal;
        }
        else
        {
            gVal = new ScriptGlobalVar() { VariableName = varName, IntValue = intVal };
            scriptGlobals.Add(varName, gVal);
        }

        RoDatabase.EnqueueDbRequest(new SaveScriptGlobalVariableRequest(new ScriptGlobalVar() { VariableName = gVal.VariableName, IntValue = gVal.IntValue, StringValue = gVal.StringValue }));

        varLock.ExitWriteLock();
    }

    public static int IncrementInt(string varName)
    {
        varLock.EnterWriteLock();

        if (scriptGlobals.TryGetValue(varName, out var gVal))
        {
            gVal.IntValue += 1;
        }
        else
        {
            gVal = new ScriptGlobalVar() { VariableName = varName, IntValue = 1 };
            scriptGlobals.Add(varName, gVal);
        }

        RoDatabase.EnqueueDbRequest(new SaveScriptGlobalVariableRequest(new ScriptGlobalVar() { VariableName = gVal.VariableName, IntValue = gVal.IntValue, StringValue = gVal.StringValue }));

        var r = gVal.IntValue;
        varLock.ExitWriteLock();

        return r;
    }



    public static void SetString(string varName, string strVal)
    {
        varLock.EnterWriteLock();

        if (scriptGlobals.TryGetValue(varName, out var gVal))
        {
            gVal.StringValue = strVal;
        }
        else
        {
            gVal = new ScriptGlobalVar() { VariableName = varName, StringValue = strVal};
            scriptGlobals.Add(varName, gVal);
        }

        RoDatabase.EnqueueDbRequest(new SaveScriptGlobalVariableRequest(new ScriptGlobalVar() { VariableName = gVal.VariableName, IntValue = gVal.IntValue, StringValue = gVal.StringValue }));

        varLock.ExitWriteLock();
    }
}