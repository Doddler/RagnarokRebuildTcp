using System.Diagnostics.CodeAnalysis;
using RoServerScript;

namespace RoRebuildServer.ScriptSystem;

public class ScriptMacro
{
    private RoScriptParser.ExpressionContext[] Values;
    private Dictionary<string, int> VariableIds;
    public string CurrentScript;

    public RoScriptParser.StatementblockContext Context;

    public ScriptMacro(string scriptName, int paramCount, RoScriptParser.StatementblockContext context)
    {
        CurrentScript = scriptName;
        Context = context;
        Values = new RoScriptParser.ExpressionContext[paramCount];
        VariableIds = new Dictionary<string, int>(paramCount);

        if (Context == null)
            throw new Exception($"Missing context!");
    }

    public void DefineVariable(int paramId, string name)
    {
        VariableIds.Add(name, paramId);
    }

    public void SetValue(int id, RoScriptParser.ExpressionContext value)
    {
        Values[id] = value;
    }

    public bool HasVariable(string name) => VariableIds.ContainsKey(name);

    public RoScriptParser.ExpressionContext GetVariable(string name) => Values[VariableIds[name]];

    public bool TryGetVariable(string name, [NotNullWhen(true)] out RoScriptParser.ExpressionContext? output)
    {
        output = null;
        if (!VariableIds.TryGetValue(name, out var id))
            return false;

        output = Values[id];

        return true;
    }
}