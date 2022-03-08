using System.Reflection;
using System.Text;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Map;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Logging;

namespace RoRebuildServer.ScriptSystem;

public class ScriptBuilder
{
    private StringBuilder scriptBuilder = new StringBuilder(5000);
    private StringBuilder blockBuilder = new StringBuilder(500);
    private StringBuilder lineBuilder = new StringBuilder(200);

    private List<string> blocks = new();
    private Dictionary<string, string> functionBaseClasses = new();
    private List<string> functionSources = new();

    private List<string> npcDefinitions = new();

    //state machine stuff
    private Dictionary<string, int> localIntVariables = new();
    private Dictionary<string, int> localStringVariables = new();
    private Dictionary<string, int> stateIntVariables = new();
    private Dictionary<string, int> stateStringVariables = new();
    private Dictionary<int, int> labels = new();
    private List<int> timerValues = new();
    private List<string> remoteCommands = new();
    private Dictionary<string, NpcInteractionResult> waitingFunctions = new();

    //private Dictionary<string, ScriptMethodHandler> methodHandlers = new();

    private string className = "";
    private string methodName = "";
    private string stateVariable = "";
    private string localvariable = "";
    private int pointerCount = 0;
    private int lineNumber = 1;
    private bool identifierSwap = false;
    private int curBlock;
    private string switchOption = "";
    private bool hasWait = false;
    private bool hasTouch = false;
    private bool hasInteract = false;
    private NpcInteractionResult waitType = NpcInteractionResult.WaitForContinue;

    public bool UseStateMachine;

    private int indentation = 1;

    public ScriptBuilder(string className, params string[] namespaceList)
    {
        this.className = className;

        foreach (var n in namespaceList)
            scriptBuilder.AppendLine($"using {n};");

        scriptBuilder.AppendLine("");
        scriptBuilder.AppendLine($"namespace RoRebuildGenData");
        scriptBuilder.AppendLine("{");
    }

    private void ResetMethod()
    {
        blockBuilder.Clear();
        blocks.Clear();
        localIntVariables.Clear();
        localStringVariables.Clear();

        pointerCount = 0;
        curBlock = 0;
    }

    public void SetLineNumber(int num)
    {
        lineNumber = num;
    }

    public StringBuilder StartIndentedScriptLine()
    {
        for (var i = 0; i < indentation; i++)
            scriptBuilder.Append("\t");

        return scriptBuilder;
    }

    public StringBuilder StartIndentedBlockLine()
    {
        for (var i = 0; i < indentation; i++)
            blockBuilder.Append("\t");

        return blockBuilder;
    }

    public int GotoFutureBlock()
    {
        StartIndentedBlockLine().AppendLine($"goto case ###{pointerCount}###;");
        pointerCount++;
        return pointerCount-1;
    }

    public void RegisterGotoDestination(int id)
    {
        labels.Add(id, curBlock);
    }

    public bool IsEmptyLine()
    {
        return lineBuilder.Length == 0;
    }

    public void OpenStateIf()
    {
        StartIndentedBlockLine().AppendLine($"\tgoto case {curBlock + 1};");
    }

    public int OpenStateElse()
    {
        StartIndentedBlockLine().AppendLine($"\tgoto case ###{pointerCount}###;");
        pointerCount++;
        return pointerCount-1;
    }

    public void AdvanceBlock(bool skipGoto = false)
    {
        curBlock++;
        if (!skipGoto)
            StartIndentedBlockLine().AppendLine($"goto case {curBlock};");

        indentation--;
        StartIndentedBlockLine().AppendLine($"case {curBlock}:");
        indentation++;
    }

    public void LoadFunctionSource(Type source, string varName)
    {
        var name = source.Name;
        if (functionSources.Contains(name))
            return;

        var methods = source.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(s => s.Name).ToList();
        foreach (var m in methods)
        {
            if (m == "GetType" || m == "ToString" || m == "Equals" || m == "GetHashCode")
                continue;

            if (!functionBaseClasses.ContainsKey(m))
                functionBaseClasses.Add(m, varName);
        }
    }

    public void StartMap(string name)
    {
        methodName = name.Replace(" ", "");
        UseStateMachine = false;
        identifierSwap = false;
        blockBuilder.Clear();


        StartIndentedScriptLine().AppendLine($"public static partial class RoRebuildGenMapData_{className}");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        StartIndentedBlockLine().AppendLine($"[ServerMapConfigAttribute(\"{methodName}\")]");
        StartIndentedBlockLine().AppendLine($"public static void Map_{methodName}(ServerMapConfig map)");
        StartIndentedBlockLine().AppendLine("{");

        LoadFunctionSource(typeof(ServerMapConfig), "map");

        indentation++;
    }



    public void StartNpc(string name)
    {
        methodName = name.Replace(" ", "");
        UseStateMachine = true;
        identifierSwap = true;
        stateVariable = "state";
        localvariable = "npc";
        blockBuilder.Clear();
        timerValues.Clear();
        remoteCommands.Clear();
        hasTouch = false;
        hasInteract = false;
        
        StartIndentedScriptLine().AppendLine($"public class RoRebuildNpcGen_{name} : NpcBehaviorBase");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        //StartIndentedBlockLine().AppendLine($"[ServerMapConfigAttribute(\"{methodName}\")]");
        StartIndentedBlockLine().AppendLine($"public override void Init(Npc npc)");
        StartIndentedBlockLine().AppendLine("{");
        indentation++;

        methodName = "Init";

        waitingFunctions.Clear();
        waitingFunctions.Add("Dialog", NpcInteractionResult.WaitForContinue);
        waitingFunctions.Add("Option", NpcInteractionResult.WaitForInput);
        waitingFunctions.Add("MoveTo", NpcInteractionResult.EndInteraction);

        LoadFunctionSource(typeof(Npc), "npc");

        
    }

    public void EndNpc(string name, string npcTag, string map, string sprite, string facing, int x, int y, int w, int h)
    {
        var behaviorName = $"RoRebuildNpcGen_{name}";
        npcDefinitions.Add($"DataManager.RegisterNpc({npcTag}, {map}, {sprite}, {x}, {y}, {facing}, {w}, {h}, {(hasInteract ? "true" : "false")}, {(hasTouch ? "true" : "false")}, new {behaviorName}());");
    }

    public void StartNpcSection(string section)
    {
        functionSources.Clear();
        functionBaseClasses.Clear();

        if (methodName == section)
            return;

        methodName = section;

        if (section == "OnClick" || section == "OnTouch")
        {
            CloseScope();
            StartIndentedBlockLine().AppendLine($"public override NpcInteractionResult {section}(Npc npc, Player player, NpcInteractionState state)");
            OpenScope();
            StartIndentedBlockLine().AppendLine($"switch ({stateVariable}.Step)");
            OpenScope();
            indentation--;
            StartIndentedBlockLine().AppendLine($"case 0:");
            indentation++;
            ApplyBlockToScript();
            
            pointerCount = 0;
            curBlock = 0;
            UseStateMachine = true;

            LoadFunctionSource(typeof(Npc), "npc");
            LoadFunctionSource(typeof(Player), "player");
            LoadFunctionSource(typeof(NpcInteractionState), "state");

            if (section == "OnClick")
                hasInteract = true;
            if (section == "OnTouch")
                hasTouch = true;
        }
        else
        {
            CloseScope();
            StartIndentedBlockLine().AppendLine($"public override void {section}(Npc npc)");
            OpenScope();

            UseStateMachine = false;

            LoadFunctionSource(typeof(Npc), "npc");
        }
    }

    public void FunctionCall(string name, bool isChained)
    {
        if (isChained)
            lineBuilder.Append($".{name}(");
        else
        {
            if (functionBaseClasses.TryGetValue(name, out var src))
                lineBuilder.Append($"{src}.{name}(");
            else
                throw new Exception($"Error in {className} line {lineNumber}: Function name {name} could not be found.");
        }

        if (UseStateMachine && waitingFunctions.TryGetValue(name, out var res))
        {
            hasWait = true;
            waitType = res;
        }
    }

    public void FunctionCallEnd()
    {
        lineBuilder.Append(")");
    }

    public void AddComma()
    {
        lineBuilder.Append(", ");
    }

    public void OutputRaw(string text)
    {
        lineBuilder.Append(text);
    }

    public void OutputReturn()
    {
        if (lineBuilder.Length > 0)
            EndLine();

        if (UseStateMachine)
            lineBuilder.Append("return NpcInteractionResult.EndInteraction;");
        else
            lineBuilder.Append("return;");

        EndLine();
    }

    public void OutputBreak()
    {
        if(lineBuilder.Length > 0)
            EndLine();

        lineBuilder.Append("break;");
        EndLine();
    }

    public void DefineVariable(string id, bool isString, bool useNpcStorage)
    {
        //NpcLocal means the npc has the variable and shared between players
        if (useNpcStorage)
        {
            if (isString)
            {
                if (!localStringVariables.ContainsKey(id))
                    localStringVariables.Add(id, localStringVariables.Count);
            }
            else
            {
                if (!localIntVariables.ContainsKey(id))
                    localIntVariables.Add(id, localIntVariables.Count);
            }
        }
        else
        {
            if (isString)
            {
                if (!stateStringVariables.ContainsKey(id))
                    stateStringVariables.Add(id, stateStringVariables.Count); 
            }
            else
            {
                if (!stateIntVariables.ContainsKey(id))
                    stateIntVariables.Add(id, stateIntVariables.Count);
            }
        }
    }

    public string GetConstValue(string id)
    {
        switch (id.ToLower())
        {
            case "result":
                return $"{stateVariable}.InteractionResult";
            case "left":
                return "0";
            case "right":
                return "1";
            case "center":
                return "2";
            case "s":
                return "0";
            case "sw":
                return "1";
            case "w":
                return "2";
            case "nw":
                return "3";
            case "n":
                return "4";
            case "ne":
                return "5";
            case "e":
                return "6";
            case "se":
                return "7";
            case "minutessincestartup":
                return "Time.MinutesSinceStartup()";
        }

        return string.Empty;
    }

    public string GetStringForVariable(string id)
    {
        int pos = 0;

        if (stateIntVariables.TryGetValue(id, out pos))
            return $"{stateVariable}.ValuesInt[{pos}]";

        if (stateStringVariables.TryGetValue(id, out pos))
            return $"{stateVariable}.ValuesString[{pos}]";
            
        if (localIntVariables.TryGetValue(id, out pos))
            return $"{localvariable}.ValuesInt[{pos}]";
        
        if (localStringVariables.TryGetValue(id, out pos))
            return $"{localvariable}.ValuesString[{pos}]";

        if(id.ToLower() == "result")
            return $"{stateVariable}.InteractionResult";
        
        return GetConstValue(id);
    }

    public void OutputVariable(string id)
    {
        if (!UseStateMachine)
        {
            lineBuilder.Append(id);
            return;
        }

        var str = GetStringForVariable(id);
        if(String.IsNullOrWhiteSpace(id))
            throw new Exception($"Attempting to use unknown variable {id}!");

        lineBuilder.Append(str);
    }

    public void OutputIdentifier(string varname)
    {
        lineBuilder.Append(varname);
    }

    public void OpenSwitch()
    {
        switchOption = lineBuilder.ToString();
        lineBuilder.Clear();

        //Console.WriteLine("Hoisting expression: " + switchOption);
    }

    public void OutputSwitchOption()
    {
        lineBuilder.Append(switchOption);
    }

    public void OpenScope()
    {
        if (lineBuilder.Length > 0)
            EndLine();

        lineBuilder.Append("{");
        EndLine();

        indentation++;
    }

    public void CloseScope()
    {
        if (lineBuilder.Length > 0)
            EndLine();

        indentation--;

        lineBuilder.Append("}");
        EndLine();
    }

    public void EndLine(int inputLineNumber = -1)
    {
        StartIndentedBlockLine();
        blockBuilder.Append(lineBuilder);
        if (inputLineNumber > 0)
            blockBuilder.Append($"; //line {inputLineNumber}");
        blockBuilder.Append(Environment.NewLine);

        lineBuilder.Clear();

        if (hasWait)
        {
            StartIndentedBlockLine().AppendLine($"{stateVariable}.Step = {curBlock + 1};");
            StartIndentedBlockLine().AppendLine($"return NpcInteractionResult.{Enum.GetName(waitType)};");
            AdvanceBlock(true);
            hasWait = false;
        }
    }

    public void ApplyBlockToScript()
    {
        if (lineBuilder.Length > 0)
            EndLine();
        if (blockBuilder.Length > 0)
            scriptBuilder.Append(blockBuilder);
        blockBuilder.Clear();
    }

    public void EndMethod()
    {

        if (UseStateMachine)
        {
            StartIndentedBlockLine().AppendLine("return NpcInteractionResult.EndInteraction;");
            indentation--;
            StartIndentedBlockLine().AppendLine("}").AppendLine("");
            StartIndentedBlockLine().AppendLine("return NpcInteractionResult.EndInteraction;");
        }

        indentation--;
        StartIndentedBlockLine().AppendLine("}").AppendLine("");

        scriptBuilder.Append(blockBuilder);
        ResetMethod();

        if (UseStateMachine)
        {
            foreach (var l in labels)
            {
                scriptBuilder.Replace($"###{l.Key}###", l.Value.ToString());
            }
            labels.Clear();
        }

        UseStateMachine = false;
    }

    public void EndClass()
    {
        indentation--;
        StartIndentedScriptLine().AppendLine("}");
    }

    public string OutputFinal()
    {
        //indentation--;
        //StartIndentedScriptLine().AppendLine("}");

        if (npcDefinitions.Count > 0)
        {
            StartIndentedScriptLine().AppendLine($"public class NpcLoader_{className} : INpcLoader");
            StartIndentedScriptLine().AppendLine("{");
            indentation++;
            StartIndentedScriptLine().AppendLine("public void Load()");
            StartIndentedScriptLine().AppendLine("{");
            indentation++;

            foreach (var npc in npcDefinitions)
                StartIndentedScriptLine().AppendLine(npc);

            indentation--;
            StartIndentedScriptLine().AppendLine("}");
            indentation--;
            StartIndentedScriptLine().AppendLine("}");
        }

        indentation--;
        StartIndentedScriptLine().AppendLine("}");

        if (indentation != 0)
            ServerLogger.LogWarning("Somehow script compilation output indentation is all messed up!");

        return scriptBuilder.ToString();
    }

}
