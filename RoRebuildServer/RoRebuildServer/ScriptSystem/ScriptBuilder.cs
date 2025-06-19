using System.Reflection;
using System.Text;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.ServerConfigScript;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using static System.Collections.Specialized.BitVector32;
using static System.Globalization.CultureInfo;

namespace RoRebuildServer.ScriptSystem;

public struct TimerFunction
{
    public int EventTime { get; set; }
    public string FuncName { get; set; }
}

public class ScriptBuilder
{
    private StringBuilder scriptBuilder = new StringBuilder(5000);
    private StringBuilder blockBuilder = new StringBuilder(500);
    private StringBuilder lineBuilder = new StringBuilder(200);

    private List<string> blocks = new();
    private Dictionary<string, string> functionBaseClasses = new();
    private List<string> functionSources = new();

    private List<string> npcDefinitions = new();
    private List<string> itemDefinitions = new();
    private List<string> monsterSkillDefinitions = new();

    //state machine stuff
    private Dictionary<string, int> localIntVariables = new();
    private Dictionary<string, int> localStringVariables = new();
    private Dictionary<string, int> stateIntVariables = new();
    private Dictionary<string, int> stateStringVariables = new();
    private Dictionary<int, int> labels = new();
    private List<int> timerValues = new();
    private List<string> remoteCommands = new();
    private Dictionary<string, NpcInteractionResult> waitingFunctions = new();
    private Dictionary<string, NpcPathUpdateResult> pathingFunctions = new();
    private Dictionary<string, string> additionalVariables { get; set; } = new();
    private Dictionary<Type, List<string>> functionValidMethods = new();

    private List<TimerFunction> timerFunctions = new();
    private List<MonsterAiState> monsterStatesWithHandlers = new();

    //private Dictionary<string, ScriptMethodHandler> methodHandlers = new();

    private string className = "";
    private string methodName = "";
    //private string npcName = "";
    private string stateVariable = "";
    private string localVariable = "";
    private int pointerCount = 0;
    private int lineNumber = 1;
    private int curBlock;
    private string switchOption = "";
    private bool hasWait = false;
    private bool hasTouch = false;
    private bool hasInteract = false;
    private string eventHandlerTarget = "";
    private NpcInteractionResult waitType = NpcInteractionResult.WaitForContinue;

    private bool hasPathWait = false;
    public NpcPathUpdateResult pathWaitType;

    public bool UseStateMachine;
    public bool UseStateStorage;
    public bool UseLocalStorage;
    public int StateStorageLimit = 0;
    public bool IsEvent;
    public string? defaultReturn = null;
    public string RequiresServerEvent = "";
    private StateMachineType stateMachineType;

    private int indentation = 1;
    private int uniqueVal = 0;

    private HashSet<string> terminalFunctions = new();

    public Stack<int> breakPointerStack = new();

    private Stack<ScriptMacro> macroStack = new();
    public ScriptMacro? ActiveMacro;
    public string ActiveScript;

    private enum StateMachineType
    {
        None,
        NpcInteraction,
        NpcPath
    }

    public bool IsTerminalFunction(string name) => terminalFunctions.Contains(name);

    public ScriptBuilder(string className, params string[] namespaceList)
    {
        this.className = className;
        ActiveScript = string.Empty;

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

    public int GetFutureBlockPointer()
    {
        pointerCount++;
        return pointerCount - 1;
    }

    public int GotoFutureBlock(bool extraIndent = false)
    {
        var line = StartIndentedBlockLine();

        if (extraIndent)
            line.Append("\t");

        line.AppendLine($"goto case ###{pointerCount}###;");
        pointerCount++;
        return pointerCount - 1;
    }

    public void GotoFutureBlock(int ptr)
    {
        StartIndentedBlockLine().AppendLine($"goto case ###{ptr}###;");
    }
    public void GotoBlock(int blockId)
    {
        StartIndentedBlockLine().AppendLine($"goto case {blockId};");
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
        return pointerCount - 1;
    }

    public int AdvanceBlock(bool skipGoto = false)
    {
        curBlock++;
        if (!skipGoto)
            StartIndentedBlockLine().AppendLine($"goto case {curBlock};");

        indentation--;
        StartIndentedBlockLine().AppendLine($"case {curBlock}:");
        indentation++;

        return curBlock;
    }

    public void LoadObjectProperties(Type source, string varName)
    {
        var name = source.Name;
        var properties = source.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
        foreach (var prop in properties)
        {
            var pName = prop.Name;
            additionalVariables.Add(pName, $"{varName}.{pName}");
        }
    }

    public void LoadFunctionSource(Type source, string varName, bool requireScriptUseableAttribute = false)
    {
        var name = source.Name;
        if (functionSources.Contains(name))
            return;

        if (functionValidMethods.TryGetValue(source, out var existingList))
        {
            foreach (var m in existingList)
            {
                if (!functionBaseClasses.ContainsKey(m))
                    functionBaseClasses.Add(m, varName);
            }

            return;
        }

        var cachedValues = new List<string>();

        var methods = source.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).ToList();
        foreach (var method in methods)
        {
            var m = method.Name;
            if (m == "GetType" || m == "ToString" || m == "Equals" || m == "GetHashCode" || m.StartsWith("set_"))
                continue;

            if (requireScriptUseableAttribute)
            {
                if (m.StartsWith("get_"))
                {
                    var prop = source.GetProperty(m.Substring(4));
                    if (prop == null)
                        continue;
                    var attr = prop.GetCustomAttribute<ScriptUseableAttribute>();
                    if (attr == null)
                        continue;
                }
                else
                {
                    var attr = method.GetCustomAttribute<ScriptUseableAttribute>();
                    if (attr == null)
                        continue;
                }
            }

            if (m.StartsWith("get_"))
            {
                var m2 = m.Substring(4);
                if (!functionBaseClasses.ContainsKey(m2))
                    functionBaseClasses.Add(m2, varName);
                if (!cachedValues.Contains(m2))
                    cachedValues.Add(m2);

                continue;
            }

            if (method.IsStatic)
            {
                if (!functionBaseClasses.ContainsKey(m))
                    functionBaseClasses.Add(m, varName);
                if (!cachedValues.Contains(m))
                    cachedValues.Add(m);
            }
            else
            {
                if (!functionBaseClasses.ContainsKey(m))
                    functionBaseClasses.Add(m, varName);
                if (!cachedValues.Contains(m))
                    cachedValues.Add(m);
            }
        }

        functionValidMethods.Add(source, cachedValues);
    }

    public void StartMap(string name)
    {
        methodName = name.Replace(" ", "").Replace("-", "_");
        UseStateMachine = false;
        UseStateStorage = false;
        UseLocalStorage = false;
        stateMachineType = StateMachineType.None;

        defaultReturn = null;
        blockBuilder.Clear();
        terminalFunctions.Clear();

        functionSources.Clear();
        functionBaseClasses.Clear();
        additionalVariables.Clear();
        terminalFunctions.Clear();

        StartIndentedScriptLine().AppendLine($"public static partial class RoRebuildGenMapData_{className}");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        StartIndentedBlockLine().AppendLine($"[ServerMapConfigAttribute(\"{name}\")]");
        StartIndentedBlockLine().AppendLine($"public static void Map_{methodName}(IServerMapConfig map)");
        StartIndentedBlockLine().AppendLine("{");

        LoadFunctionSource(typeof(ServerMapConfig), "map");

        foreach (var i in Enum.GetValues<SpawnCreateFlags>())
            additionalVariables.Add(i.ToString(), $"SpawnCreateFlags.{i}");

        indentation++;
    }


    public void StartServerConfigHandler(string name)
    {
        UseStateMachine = false;
        UseStateStorage = false;
        UseLocalStorage = false;
        stateMachineType = StateMachineType.None;
        defaultReturn = null;
        blockBuilder.Clear();
        terminalFunctions.Clear();

        StartIndentedScriptLine().AppendLine($"public class RoRebuildServerConfig_{name} : ServerConfigScriptHandlerBase");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        StartIndentedBlockLine().AppendLine($"public override void Init()");
        StartIndentedBlockLine().AppendLine("{");
        indentation++;

        methodName = "Init";
    }

    public void StartItem(string name)
    {
        UseStateMachine = false;
        UseStateStorage = false;
        UseLocalStorage = false;
        stateMachineType = StateMachineType.None;
        defaultReturn = null;
        blockBuilder.Clear();
        terminalFunctions.Clear();

        StartIndentedScriptLine().AppendLine($"public class RoRebuildItemGen_{name} : ItemInteractionBase");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        StartIndentedBlockLine().AppendLine($"public override void Init(Player player, CombatEntity combatEntity)");
        StartIndentedBlockLine().AppendLine("{");
        indentation++;

        methodName = "Init";

        LoadFunctionSource(typeof(Player), "player", true);
        LoadFunctionSource(typeof(CombatEntity), "combatEntity", true);
    }

    public void StartMonsterSkillHandler(string name)
    {
        methodName = name.Replace(" ", "").Replace("-", "_");
        UseStateMachine = false;
        UseStateStorage = false;
        UseLocalStorage = false;
        stateMachineType = StateMachineType.None;
        defaultReturn = null;
        blockBuilder.Clear();
        monsterStatesWithHandlers.Clear();
        terminalFunctions.Clear();

        StartIndentedScriptLine().AppendLine($"public class RoRebuildSkillAI_{name} : MonsterSkillAiBase");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        StartIndentedBlockLine().AppendLine($"public void Init(MonsterSkillAiState state)");
        StartIndentedBlockLine().AppendLine("{");
        indentation++;

        methodName = "Init";
    }

    public void EndMonsterSkillHandler(string name, bool isAltType)
    {
        var behaviorName = $"RoRebuildSkillAI_{name}";
        monsterSkillDefinitions.Add($"DataManager.RegisterMonsterSkillHandler(\"{name}\", new {behaviorName}(), {(isAltType ? "true" : "false")});");
    }

    public void EndItem(string name, string className)
    {
        var behaviorName = $"RoRebuildItemGen_{className}";
        itemDefinitions.Add($"DataManager.RegisterItem(\"{name}\", new {behaviorName}());");
    }

    public void EndComboItem(string name, string className, List<string> comboItems)
    {
        var behaviorName = $"RoRebuildItemGen_{className}";
        var items = new StringBuilder();
        foreach (var item in comboItems)
        {
            if (items.Length > 0)
                items.Append(", ");
            items.Append($"\"{item}\"");
        }
        itemDefinitions.Add($"DataManager.RegisterComboItem(\"{name}\", new {behaviorName}(), new List<string>() {{ {items} }});");
    }


    private void ClearVariables()
    {
        functionSources.Clear();
        functionBaseClasses.Clear();
        pathingFunctions.Clear();
        additionalVariables.Clear();
        terminalFunctions.Clear();
        stateIntVariables.Clear();
        stateStringVariables.Clear();
        defaultReturn = null;
    }

    private void SetupMonsterHandlerVariables()
    {
        UseStateMachine = false;
        UseStateStorage = false;
        UseLocalStorage = false;

        LoadObjectProperties(typeof(MonsterSkillAiState), "state");
        LoadFunctionSource(typeof(MonsterSkillAiState), "state");
        LoadFunctionSource(typeof(MonsterSkillAiBase), "this"); //probably don't need this but may as well...

        foreach (var i in Enum.GetValues<CharacterSkill>())
            additionalVariables.Add(i.ToString(), $"CharacterSkill.{i}");

        foreach (var i in Enum.GetValues<MonsterSkillAiFlags>())
            additionalVariables.TryAdd(i.ToString(), $"MonsterSkillAiFlags.{i}");


        foreach (var i in Enum.GetValues<MonsterAiState>())
            additionalVariables.TryAdd(i.ToString(), $"MonsterAiState.{i}");

        foreach (var i in Enum.GetValues<MonsterAiType>())
            additionalVariables.TryAdd(i.ToString(), $"MonsterAiType.{i}");
    }

    public void StartMonsterEventSection(string section)
    {
        if (methodName == section)
            return;

        ClearVariables();

        CloseScope();
        StartIndentedBlockLine().AppendLine($"public override void {section}(MonsterSkillAiState state)");
        OpenScope();

        SetupMonsterHandlerVariables();
    }

    public void StartMonsterSkillAiSection(string section)
    {
        if (methodName == section)
            return;

        if (!Enum.TryParse<MonsterAiState>(section, out var state))
            throw new Exception($"Error in {className} line {lineNumber} : the section '{section}' does not match a valid MonsterAiState.");
        if (monsterStatesWithHandlers.Contains(state))
            throw new Exception($"Error in {className} line {lineNumber} : the section '{section}' is defined twice.");

        ClearVariables();

        monsterStatesWithHandlers.Add(state);
        methodName = section;
        eventHandlerTarget = "state.CastSuccessEvent";
        terminalFunctions.Add("TryCast");
        terminalFunctions.Add("Cast");

        CloseScope();
        StartIndentedBlockLine().AppendLine($"public void {section}(MonsterSkillAiState state)");
        OpenScope();

        SetupMonsterHandlerVariables();
    }

    public void StartSkillEventMethod(string eventName)
    {
        ClearVariables();

        methodName = eventName;
        StartIndentedScriptLine().AppendLine($"private void {eventName}(MonsterSkillAiState state)");
        OpenScope();

        SetupMonsterHandlerVariables();
    }

    public void CreateFinalSkillHandler()
    {
        StartIndentedScriptLine().AppendLine($"public override void RunAiSkillUpdate(MonsterAiState aiState, MonsterSkillAiState skillState)");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        StartIndentedScriptLine().AppendLine("skillState.FinishedProcessing = false;");


        if (monsterStatesWithHandlers.Contains(MonsterAiState.StateAny))
        {
            //don't call stateAny if we're disabled. We might want skill handler to run while disabled for some boss mechanics down the line.
            StartIndentedScriptLine().AppendLine($"if(!skillState.IsDisabled())");
            StartIndentedScriptLine().AppendLine($"\tStateAny(skillState);");
            StartIndentedScriptLine().AppendLine($"if(skillState.FinishedProcessing) return;");

            //
        }


        StartIndentedScriptLine().AppendLine($"switch(aiState)");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        foreach (var state in monsterStatesWithHandlers)
        {
            if (state == MonsterAiState.StateAny)
                continue;
            StartIndentedScriptLine().AppendLine($"case MonsterAiState.{state}:");
            indentation++;
            StartIndentedScriptLine().AppendLine($"{state}(skillState);");
            StartIndentedScriptLine().AppendLine($"break;");
            indentation--;
        }

        StartIndentedScriptLine().AppendLine("}");
        indentation--;
        //StartIndentedScriptLine().AppendLine("return false;");
        StartIndentedScriptLine().AppendLine("}");
        indentation--;
        EndLine();
    }


    public void StartItemSection(string section)
    {
        if (methodName == section)
            return;

        functionSources.Clear();
        functionBaseClasses.Clear();
        additionalVariables.Clear();
        terminalFunctions.Clear();
        defaultReturn = null;

        methodName = section;

        if (section == "OnEquip" || section == "OnUnequip")
        {
            CloseScope();
            StartIndentedBlockLine().AppendLine($"public override void {section}(Player player, CombatEntity combatEntity, ItemEquipState state, UniqueItem item, EquipSlot position)");
            OpenScope();

            stateVariable = "state";
            UseStateMachine = false;
            UseStateStorage = true;
            UseLocalStorage = true;
            StateStorageLimit = 4;

            LoadFunctionSource(typeof(ItemEquipState), "state");
            LoadFunctionSource(typeof(Player), "player", true);
            //LoadFunctionSource(typeof(CombatEntity), "combatEntity"); //we don't want them using the addstat/substat directly, ti should come from ItemEquipState
            LoadFunctionSource(typeof(ScriptUtilityFunctions), "ScriptUtilityFunctions");


            foreach (var i in Enum.GetValues<CharacterElement>())
                additionalVariables.Add(i.ToString(), $"CharacterElement.{i}");


            foreach (var i in Enum.GetValues<AttackElement>())
                additionalVariables.Add($"{i}Element", $"AttackElement.{i}");

            additionalVariables.Add("position", "position");

            foreach (var i in Enum.GetNames<EquipPosition>())
                additionalVariables.Add(i.ToString(), $"EquipPosition.{i}");

            foreach (var i in Enum.GetValues<CharacterSkill>())
                additionalVariables.TryAdd(i.ToString(), $"CharacterSkill.{i}");


            foreach (var i in Enum.GetValues<SkillPreferredTarget>())
                additionalVariables.TryAdd($"Target{i}", $"SkillPreferredTarget.{i}");
        }
        else
        {

            CloseScope();
            if (section == "OnValidate")
            {
                defaultReturn = "true";
                StartIndentedBlockLine().AppendLine($"public override bool {section}(Player player, CombatEntity combatEntity)");
                additionalVariables.Add("target", "combatEntity");
            }
            else if (section == "OnUseTargeted")
            {
                defaultReturn = "true";
                StartIndentedBlockLine().AppendLine($"public override bool {section}(int itemId, Player player, CombatEntity combatEntity, CombatEntity target)");
                additionalVariables.Add("target", "target");
                additionalVariables.Add("item", "itemId");
                additionalVariables.Add("id", "itemId");
            }
            else
                StartIndentedBlockLine().AppendLine($"public override void {section}(Player player, CombatEntity combatEntity)");

            OpenScope();

            UseStateMachine = false;
            UseStateStorage = false;
            UseLocalStorage = false;

            LoadFunctionSource(typeof(Player), "player", true);
            LoadFunctionSource(typeof(CombatEntity), "combatEntity", true);
            LoadFunctionSource(typeof(ScriptUtilityFunctions), "ScriptUtilityFunctions");

            foreach (var i in Enum.GetValues<CharacterSkill>())
                additionalVariables.Add(i.ToString(), $"CharacterSkill.{i}");

            foreach (var i in Enum.GetValues<StatusCleanseTarget>())
                additionalVariables.Add("Status" + i.ToString(), $"StatusCleanseTarget.{i}");

        }

        foreach (var i in Enum.GetNames<CharacterStat>())
        {
            var str = i.ToString();
            if (!additionalVariables.ContainsKey(str))
                additionalVariables.Add(str, $"CharacterStat.{i}");
        }

        foreach (var i in Enum.GetValues<CharacterStatusEffect>())
        {
            var str = "Status" + i.ToString();
            if (!additionalVariables.ContainsKey(str))
                additionalVariables.Add(str, $"CharacterStatusEffect.{i}");
        }
    }


    public void StartServerConfigSection(string section)
    {
        if (methodName == section)
            return;

        OutputReturn(defaultReturn);
        CloseScope();

        functionSources.Clear();
        functionBaseClasses.Clear();
        additionalVariables.Clear();
        terminalFunctions.Clear();
        defaultReturn = null;

        methodName = section;

        stateVariable = "this";
        UseStateMachine = false;
        UseStateStorage = false;
        UseLocalStorage = false;
        StateStorageLimit = 0;

        if (section == "OnLoadDropData" || section == "OnSetItemPurchasePrice" || section == "OnSetItemSaleValue")
        {

            if (section == "OnLoadDropData")
                StartIndentedBlockLine().AppendLine($"public override int {section}(ItemClass type, string code, string subType, int rate)");
            else
                StartIndentedBlockLine().AppendLine($"public override int {section}(ItemClass type, string code, string subType, int price)");
            OpenScope();

            additionalVariables.Add("type", "type");
            additionalVariables.Add("code", "code");
            additionalVariables.Add("subType", "subType");
            if (section == "OnLoadDropData")
            {
                additionalVariables.Add("rate", "rate");
                defaultReturn = "rate";
            }
            else
            {
                additionalVariables.Add("price", "price");
                defaultReturn = "price";
            }

            LoadFunctionSource(typeof(ScriptUtilityFunctions), "ScriptUtilityFunctions");

            foreach (var i in Enum.GetValues<ItemClass>())
                additionalVariables.Add(i.ToString(), $"ItemClass.{i}");
        }
        else if (section == "OnSetMonsterSpawnTime")
        {
            StartIndentedBlockLine().AppendLine($"public override void OnSetMonsterSpawnTime(MonsterDatabaseInfo monster, string mapName, ref int minTime, ref int maxTime)");
            OpenScope();

            additionalVariables.Add("minTime", "minTime");
            additionalVariables.Add("maxTime", "maxTime");
            additionalVariables.Add("mapName", "mapName");

            LoadFunctionSource(typeof(MonsterDatabaseInfo), "monster");
        }
        else
            throw new Exception($"Invalid ServerConfig section type {section}!");

    }


    public string StartNpc(string name, bool isTrader)
    {
        methodName = name.Replace(" ", "");
        UseStateMachine = false;
        UseStateStorage = false;
        UseLocalStorage = true;
        stateVariable = "state";
        localVariable = "npc";
        blockBuilder.Clear();
        timerValues.Clear();
        remoteCommands.Clear();
        terminalFunctions.Clear();
        functionSources.Clear();
        ClearVariables();
        hasTouch = false;
        hasInteract = false;
        defaultReturn = null;


        name += "_" + Guid.NewGuid().ToString().Replace("-", "_");

        var baseClass = "NpcBehaviorBase";
        if (isTrader)
        {
            baseClass = "NpcTraderBase";
            hasInteract = true;
        }

        //if (UniqueNames.Contains(name))
        //{
        //    if (name.Contains("#"))
        //        throw new Exception(
        //            $"The NPC with the unique name {name} was attempted to be reused in the script {className}");
        //    else
        //        name += "_" + GameRandom.Next(0, 999_999_999);
        //}

        //UniqueNames.Add(name);

        StartIndentedScriptLine().AppendLine($"public class RoRebuildNpcGen_{name} : {baseClass}");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        //StartIndentedBlockLine().AppendLine($"[ServerMapConfigAttribute(\"{methodName}\")]");
        StartIndentedBlockLine().AppendLine($"public override void Init(Npc npc)");
        StartIndentedBlockLine().AppendLine("{");
        indentation++;

        methodName = "Init";

        waitingFunctions.Clear();
        waitingFunctions.Add("Dialog", NpcInteractionResult.WaitForContinue);
        waitingFunctions.Add("DialogBig", NpcInteractionResult.WaitForContinue);
        waitingFunctions.Add("Option", NpcInteractionResult.WaitForInput);
        waitingFunctions.Add("PromptForCount", NpcInteractionResult.WaitForItemPrompt);
        waitingFunctions.Add("OpenShop", NpcInteractionResult.WaitForShop);
        waitingFunctions.Add("StartSellToNpc", NpcInteractionResult.WaitForShop);
        waitingFunctions.Add("StartItemTrade", NpcInteractionResult.WaitForTrade);
        waitingFunctions.Add("OpenStorage", NpcInteractionResult.WaitForStorageAccess);
        waitingFunctions.Add("OpenRefineDialog", NpcInteractionResult.WaitForRefine);
        waitingFunctions.Add("MoveTo", NpcInteractionResult.EndInteraction);

        LoadFunctionSource(typeof(Npc), "npc");

        return name;
    }

    public string StartEvent(string name)
    {
        methodName = name.Replace(" ", "");
        UseStateMachine = false;
        UseStateStorage = false;
        UseLocalStorage = true;
        stateVariable = "state";
        localVariable = "npc";
        blockBuilder.Clear();
        timerValues.Clear();
        remoteCommands.Clear();
        terminalFunctions.Clear();
        ClearVariables();
        hasTouch = false;
        hasInteract = false;
        defaultReturn = null;

        name += "_" + Guid.NewGuid().ToString().Replace("-", "_").Replace("'", "").Replace("[", "").Replace("]", "");

        StartIndentedScriptLine().AppendLine($"public class RoRebuildNpcGen_{name} : NpcBehaviorBase");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        //StartIndentedBlockLine().AppendLine($"[ServerMapConfigAttribute(\"{methodName}\")]");
        StartIndentedBlockLine().AppendLine($"public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)");
        StartIndentedBlockLine().AppendLine("{");
        indentation++;

        StartIndentedBlockLine().AppendLine("npc.ParamsInt = new int[4];");
        StartIndentedBlockLine().AppendLine("npc.ParamsInt[0] = param1; npc.ParamsInt[1] = param2; npc.ParamsInt[2] = param3; npc.ParamsInt[3] = param4;");
        StartIndentedBlockLine().AppendLine("npc.ParamString = paramString;");

        methodName = "InitEvent";

        waitingFunctions.Clear();
        waitingFunctions.Add("Dialog", NpcInteractionResult.WaitForContinue);
        waitingFunctions.Add("Option", NpcInteractionResult.WaitForInput);
        waitingFunctions.Add("OpenShop", NpcInteractionResult.WaitForShop);
        waitingFunctions.Add("MoveTo", NpcInteractionResult.EndInteraction);

        additionalVariables.Clear();
        LoadFunctionSource(typeof(Npc), "npc");
        LoadFunctionSource(typeof(ScriptUtilityFunctions), "ScriptUtilityFunctions");
        additionalVariables.Add("Param1", "npc.ParamsInt[0]");
        additionalVariables.Add("Param2", "npc.ParamsInt[1]");
        additionalVariables.Add("Param3", "npc.ParamsInt[2]");
        additionalVariables.Add("Param4", "npc.ParamsInt[3]");
        additionalVariables.Add("ParamString", "npc.ParamString");

        foreach (var i in Enum.GetValues<CharacterSkill>())
            additionalVariables.Add(i.ToString(), $"CharacterSkill.{i}");


        foreach (var i in Enum.GetValues<NpcEffectType>())
            additionalVariables.Add("Effect" + i.ToString(), $"NpcEffectType.{i}");

        IsEvent = true;

        return name;
    }

    public void EndNpc(string name, string npcTag, string map, string signalName, string sprite, string facing, int x, int y, int w, int h)
    {
        var behaviorName = $"RoRebuildNpcGen_{name}";
        npcDefinitions.Add($"DataManager.RegisterNpc({npcTag}, {map}, {signalName}, {sprite}, {x}, {y}, {facing}, {w}, {h}, {(hasInteract ? "true" : "false")}, {(hasTouch ? "true" : "false")}, new {behaviorName}());");
    }


    public void EndEvent(string name, string className)
    {
        var behaviorName = $"RoRebuildNpcGen_{className}";
        npcDefinitions.Add($"DataManager.RegisterEvent(\"{name}\", new {behaviorName}());");
        IsEvent = false;
    }

    public void StartNpcSection(string section, int timer = -1)
    {
        if (timer >= 0)
        {
            section = section.TrimEnd() + timer;
        }

        if (methodName == section)
            return;

        EndMethod();

        ClearVariables();

        methodName = section;
        stateVariable = "state";

        if (IsEvent)
        {
            additionalVariables.Add("Param1", "npc.ParamsInt[0]");
            additionalVariables.Add("Param2", "npc.ParamsInt[1]");
            additionalVariables.Add("Param3", "npc.ParamsInt[2]");
            additionalVariables.Add("Param4", "npc.ParamsInt[3]");
            additionalVariables.Add("ParamString", "npc.ParamString");

            foreach (var i in Enum.GetValues<CharacterSkill>())
                additionalVariables.Add(i.ToString(), $"CharacterSkill.{i}");
        }

        if (section == "OnPath")
        {
            stateVariable = "path";
            StartIndentedBlockLine().AppendLine($"public override NpcPathUpdateResult {section}(Npc npc, NpcPathHandler path)");
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
            UseStateStorage = true;
            UseLocalStorage = true;
            stateMachineType = StateMachineType.NpcPath;
            StateStorageLimit = NpcInteractionState.StorageCount;

            LoadFunctionSource(typeof(Npc), "npc");
            LoadFunctionSource(typeof(NpcPathHandler), "path");
            LoadFunctionSource(typeof(ScriptUtilityFunctions), "ScriptUtilityFunctions");

            pathingFunctions.Clear();
            pathingFunctions.Add("WaitTime", NpcPathUpdateResult.WaitForTime);
            pathingFunctions.Add("Wait", NpcPathUpdateResult.WaitForTime);
            pathingFunctions.Add("PathTo", NpcPathUpdateResult.WaitForMove);
            pathingFunctions.Add("PathToTarget", NpcPathUpdateResult.WaitForMove);

            return;
        }

        if (section == "OnClick" || section == "OnTouch")
        {
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
            UseStateStorage = true;
            UseLocalStorage = true;
            stateMachineType = StateMachineType.NpcInteraction;
            StateStorageLimit = NpcInteractionState.StorageCount;

            LoadFunctionSource(typeof(Npc), "npc");
            LoadFunctionSource(typeof(Player), "player", true);
            LoadFunctionSource(typeof(NpcInteractionState), "state");
            LoadFunctionSource(typeof(ScriptUtilityFunctions), "ScriptUtilityFunctions");

            foreach (var i in Enum.GetValues<CharacterSkill>())
                additionalVariables.Add(i.ToString(), $"CharacterSkill.{i}");

            if (section == "OnClick")
                hasInteract = true;
            if (section == "OnTouch")
                hasTouch = true;

            return;
        }

        if (section == "OnSignal")
        {

            StartIndentedBlockLine().AppendLine($"public override void {section}(Npc npc, Npc srcNpc, string signal, int value1, int value2, int value3, int value4)");
            OpenScope();

            UseStateMachine = false;
            UseLocalStorage = true;

            LoadFunctionSource(typeof(Npc), "npc");
            additionalVariables.Add("Src", "SrcNpc");
            additionalVariables.Add("Signal", "signal");
            additionalVariables.Add("Value1", "value1");
            additionalVariables.Add("Value2", "value2");
            additionalVariables.Add("Value3", "value3");
            additionalVariables.Add("Value4", "value4");
            LoadFunctionSource(typeof(ScriptUtilityFunctions), "ScriptUtilityFunctions");
            return;
        }

        var strOverride = "";
        if (section == "OnMobKill")
            strOverride = " override";

        StartIndentedBlockLine().AppendLine($"public{strOverride} void {section}(Npc npc)");
        OpenScope();

        UseStateMachine = false;
        UseLocalStorage = true;

        LoadFunctionSource(typeof(Npc), "npc");
        LoadFunctionSource(typeof(ScriptUtilityFunctions), "ScriptUtilityFunctions");


        if (section.StartsWith("OnTimer"))
        {
            timerFunctions.Add(new TimerFunction() { EventTime = timer, FuncName = methodName });
        }
    }

    public void PushMacro(ScriptMacro macro)
    {
        if (ActiveMacro != null)
            macroStack.Push(ActiveMacro);

        ActiveMacro = macro;
    }

    public void PopMacro()
    {
        if (ActiveMacro == null)
            throw new Exception("Unable to pop macro, no macro is currently on the stack!");

        ActiveMacro = null;
        if (macroStack.TryPop(out var macro))
            ActiveMacro = macro;
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
            {
                var file = ActiveScript;
                if (ActiveMacro != null)
                    file = ActiveMacro.CurrentScript;
                throw new Exception($"Error in {file} line {lineNumber}: Function name {name} could not be found.");
            }
        }

        if (UseStateMachine && waitingFunctions.TryGetValue(name, out var res))
        {
            hasWait = true;
            waitType = res;
        }

        if (UseStateMachine && pathingFunctions.TryGetValue(name, out var res2))
        {
            hasPathWait = true;
            pathWaitType = res2;
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

    public void OutputReturn(string? expr = null)
    {
        if (lineBuilder.Length > 0)
            EndLine();

        if (UseStateMachine)
        {
            if (stateMachineType == StateMachineType.NpcInteraction)
                lineBuilder.Append("return NpcInteractionResult.EndInteraction;");
            else
                lineBuilder.Append("return NpcPathUpdateResult.EndPath;");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(defaultReturn))
                lineBuilder.Append("return;");
            else if (!string.IsNullOrWhiteSpace(expr))
                lineBuilder.Append($"return {expr};");
            else
                lineBuilder.Append($"return {defaultReturn};");
        }

        EndLine();
    }

    public void StartReturn()
    {
        lineBuilder.Append("return ");
    }

    public void EndReturn()
    {
        lineBuilder.Append(";");
        EndLine();
    }


    public void OutputBreak()
    {
        if (lineBuilder.Length > 0)
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
                if (UseStateStorage)
                    return $"{stateVariable}.OptionResult";
                else
                    return id;
            case "left":
                return "0";
            case "right":
                return "1";
            case "center":
                return "2";
            case "true":
                return "true";
            case "false":
                return "false";
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

        if (functionBaseClasses.TryGetValue(id, out var src))
            return $"{src}.{id}";

        if (additionalVariables.ContainsKey(id))
            return additionalVariables[id];

        var file = ActiveScript;
        if (ActiveMacro != null)
            file = ActiveMacro.CurrentScript;
        throw new Exception($"Error in {file} line {lineNumber} : Unable to parse parse unidentified constant '{id}'");

        //return id;
    }

    public string GetStringForVariable(string id)
    {
        int pos = 0;

        if (UseStateStorage)
        {
            if (stateIntVariables.TryGetValue(id, out pos))
                return $"{stateVariable}.ValuesInt[{pos}]";

            if (stateStringVariables.TryGetValue(id, out pos))
                return $"{stateVariable}.ValuesString[{pos}]";
        }

        if (UseLocalStorage)
        {
            if (localIntVariables.TryGetValue(id, out pos))
                return $"{localVariable}.ValuesInt[{pos}]";

            if (localStringVariables.TryGetValue(id, out pos))
                return $"{localVariable}.ValuesString[{pos}]";
        }

        if (id.ToLower() == "result")
            return $"{stateVariable}.OptionResult";

        return GetConstValue(id);
    }

    public string OutputEventCall()
    {
        if (string.IsNullOrWhiteSpace(eventHandlerTarget))
            throw new Exception($"Attempting to add event handler to function with -> when no event handler type is defined.");

        var eventName = $"Event{uniqueVal}";
        lineBuilder.Append($"{eventHandlerTarget} = {eventName};");
        uniqueVal++;
        EndLine();

        return eventName;
    }

    public void OutputVariable(string id)
    {
        //if (!UseStateStorage)
        //{
        //    lineBuilder.Append(id);
        //    return;
        //}

        var str = GetStringForVariable(id);
        if (String.IsNullOrWhiteSpace(id))
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
            blockBuilder.Append($"; //{ActiveScript} line {inputLineNumber}");
        blockBuilder.Append(Environment.NewLine);

        lineBuilder.Clear();

        if (stateMachineType == StateMachineType.None)
            return;

        if (hasWait)
        {
            StartIndentedBlockLine().AppendLine($"{stateVariable}.Step = {curBlock + 1};");
            StartIndentedBlockLine().AppendLine($"return NpcInteractionResult.{Enum.GetName(waitType)};");
            AdvanceBlock(true);
            hasWait = false;
        }

        if (hasPathWait)
        {
            StartIndentedBlockLine().AppendLine($"{stateVariable}.Step = {curBlock + 1};");
            StartIndentedBlockLine().AppendLine($"return NpcPathUpdateResult.{Enum.GetName(pathWaitType)};");
            AdvanceBlock(true);
            hasPathWait = false;
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
            if (stateMachineType == StateMachineType.NpcInteraction)
            {
                StartIndentedBlockLine().AppendLine("return NpcInteractionResult.EndInteraction;");
                indentation--;
                StartIndentedBlockLine().AppendLine("}").AppendLine("");
                StartIndentedBlockLine().AppendLine("return NpcInteractionResult.EndInteraction;");
            }
            else
            {
                StartIndentedBlockLine().AppendLine("return NpcPathUpdateResult.EndPath;");
                indentation--;
                StartIndentedBlockLine().AppendLine("}").AppendLine("");
                StartIndentedBlockLine().AppendLine("return NpcPathUpdateResult.EndPath;");
            }
        }

        if (!string.IsNullOrWhiteSpace(defaultReturn))
            StartIndentedBlockLine().AppendLine($"return {defaultReturn};");

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

    public void OutputTimerMethods()
    {
        timerFunctions.Sort((a, b) => a.EventTime.CompareTo(b.EventTime));

        StartIndentedBlockLine().AppendLine($"public override void OnTimer(Npc npc, float lastTime, float newTime)");
        OpenScope();

        for (var i = 0; i < timerFunctions.Count; i++)
        {
            var timer = timerFunctions[i];
            var f = (float)timer.EventTime / 1000f;

            //force invariant culture here otherwise we might output invalid floats in certain locales
            var str = string.Create(InvariantCulture, $"if (lastTime < {f}f && newTime >= {f}f)");

            StartIndentedBlockLine().AppendLine(str);
            OpenScope();
            StartIndentedBlockLine().AppendLine($"this.OnTimer{timer.EventTime}(npc);");
            CloseScope();
        }

        CloseScope();
        ApplyBlockToScript();

        timerFunctions.Clear();
    }

    public void EndClass()
    {
        if (timerFunctions.Count > 0)
            OutputTimerMethods();

        indentation--;
        StartIndentedScriptLine().AppendLine("}");

        localIntVariables.Clear();
        localStringVariables.Clear();
        additionalVariables.Clear();
    }

    public bool HasUserVariable(string varName) => additionalVariables.ContainsKey(varName);

    public void AddUserVariable(string varName)
    {
        if (!additionalVariables.ContainsKey(varName))
            additionalVariables.Add(varName, varName);
    }

    private void OutputLoader(string loaderName, string loaderInterface, List<string>? lines)
    {
        if (lines == null || lines.Count == 0)
            return;

        StartIndentedScriptLine().AppendLine($"public class {loaderName}_{className} : {loaderInterface}");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;
        StartIndentedScriptLine().AppendLine("public void Load()");
        StartIndentedScriptLine().AppendLine("{");
        indentation++;

        if (!string.IsNullOrWhiteSpace(RequiresServerEvent))
            StartIndentedScriptLine().AppendLine($"if (!ServerConfig.OperationConfig.ActiveEvents?.Contains(\"{RequiresServerEvent}\")?? false) return;");

        foreach (var line in lines)
            StartIndentedScriptLine().AppendLine(line);

        indentation--;
        StartIndentedScriptLine().AppendLine("}");
        indentation--;
        StartIndentedScriptLine().AppendLine("}");
    }

    public string OutputFinal()
    {
        //indentation--;
        //StartIndentedScriptLine().AppendLine("}");

        OutputLoader("NpcLoader", "INpcLoader", npcDefinitions);
        OutputLoader("ItemLoader", "IItemLoader", itemDefinitions);
        OutputLoader("MonsterSkillLoader", "IMonsterLoader", monsterSkillDefinitions);

        //if (npcDefinitions.Count > 0)
        //{
        //    StartIndentedScriptLine().AppendLine($"public class NpcLoader_{className} : INpcLoader");
        //    StartIndentedScriptLine().AppendLine("{");
        //    indentation++;
        //    StartIndentedScriptLine().AppendLine("public void Load()");
        //    StartIndentedScriptLine().AppendLine("{");
        //    indentation++;

        //    foreach (var npc in npcDefinitions)
        //        StartIndentedScriptLine().AppendLine(npc);

        //    indentation--;
        //    StartIndentedScriptLine().AppendLine("}");
        //    indentation--;
        //    StartIndentedScriptLine().AppendLine("}");
        //}

        indentation--;
        StartIndentedScriptLine().AppendLine("}");

        if (indentation != 0)
            ServerLogger.LogWarning("Somehow script compilation output indentation is all messed up!");

        return scriptBuilder.ToString();
    }

}
