using System.Diagnostics;
using RoServerScript;

namespace RoRebuildServer.ScriptSystem;

public struct ScriptTopLevelParameter
{
    public bool IsString { get; set; }
    public int Int { get; set; }
    public string String { get; set; }

    public ScriptTopLevelParameter(string text)
    {
        String = String.Empty;

        if (int.TryParse(text, out var i))
        {
            IsString = false;
            Int = i;
            return;
        }

        IsString = true;
        String = text;
    }

    public ScriptTopLevelParameter(string text, int value, bool isString)
    {
        IsString = isString;
        String = text;
        Int = value;
    }

    public override string ToString() => IsString ? String : Int.ToString();
    public static implicit operator string(ScriptTopLevelParameter p) => p.String;
    public static implicit operator int(ScriptTopLevelParameter p) => p.Int;
}

public class ScriptTopLevelParameters
{
    public List<ScriptTopLevelParameter> Parameters { get; set; } = new();
    public Dictionary<string, ScriptTopLevelParameter> ParametersMap { get; set; } = new();

    private static readonly ScriptTopLevelParameter Empty = new("\"\"", 0, false);

    public bool HasParametersSet() => ParametersMap.Count > 0;

    public ScriptTopLevelParameter this[string index]
    {
        get
        {
            if (ParametersMap.ContainsKey(index))
                return ParametersMap[index];
            return Empty;
        }
    }

    public ScriptTopLevelParameter this[int index]
    {
        get
        {
            if (index < 0 || index >= Parameters.Count)
                throw new IndexOutOfRangeException();
            return Parameters[index];
        }
    }

    public ScriptTopLevelParameters(RoScriptParser.ExpressionContext[] expressions)
    {
        foreach (var e in expressions)
        {
            Parameters.Add(new ScriptTopLevelParameter(e.GetText()));
        }
    }

    public void SetParameters(string[] values)
    {
        Debug.Assert(Parameters.Count == values.Length);

        for (var i = 0; i < Parameters.Count; i++)
            ParametersMap.Add(values[i], Parameters[i]);
    }

    public bool VerifySignature(string signature)
    {
        if (signature.Length != Parameters.Count)
            return false;
        for (var i = 0; i < signature.Length; i++)
        {
            var c = signature[i];
            switch (c)
            {
                case 'i':
                    if (Parameters[i].IsString)
                        return false;
                    continue;
                case 's':
                    if (!Parameters[i].IsString)
                        return false;
                    continue;
                default:
                    throw new Exception($"Unknown method signature value '{c}'.");
            }
        }

        return true;
    }
}