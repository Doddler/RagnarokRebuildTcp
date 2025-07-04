﻿using System.Diagnostics;
using System.Globalization;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Logging;
using RoServerScript;
using static RoServerScript.RoScriptParser;
using Microsoft.AspNetCore.Components.Forms;
using RoRebuildServer.Data;

namespace RoRebuildServer.ScriptSystem;

internal class ScriptTreeWalker
{
    private ScriptBuilder builder = null!;
    private string name = null!;
    private string path = "";
    private Action<StartSectionContext>? sectionHandler;
    private Dictionary<string, ScriptMacro> macroMap = new();
    private Dictionary<string, StatementblockContext> eventHandlers = new();
    private Dictionary<string, ScriptMacro> eventMacros = new();
    private HashSet<string> activeMacros = new();
    private ScriptMacro? ActiveEventMacro;
    private Stack<string> scriptNameStack = new();

    public ScriptTreeWalker(string inputName, string inputPath)
    {
        path = inputPath;
        //this is silly and I really should do something better
        builder = new ScriptBuilder(inputName.Replace(" ", "_"),
            "System",
            "System.Linq",
            "System.Collections.Generic",
            "RoRebuildServer.Data.Map",
            "RebuildSharedData.Data",
            "RoRebuildServer.Data",
            "RoRebuildServer.Data.ServerConfigScript",
            "RoRebuildServer.EntityComponents",
            "RoRebuildServer.ScriptSystem",
            "RebuildSharedData.Enum",
            "RebuildSharedData.Enum.EntityStats",
            "RoRebuildServer.EntityComponents.Npcs",
            "RoRebuildServer.Simulation.Util",
            "RoRebuildServer.EntityComponents.Items",
            "RoRebuildServer.EntityComponents.Monsters",
            "RoRebuildServer.Data.Monster",
            "RoRebuildServer.EntityComponents.Character",
            "RoRebuildServer.EntityComponents.Util",
            "RoRebuildServer.Simulation.StatusEffects",
            "RoRebuildServer.Simulation.StatusEffects._1stJob",
            "RoRebuildServer.Simulation.StatusEffects.ItemEffects",
            "RoRebuildServer.Simulation.StatusEffects.GenericDebuffs");
    }

    public string BuildClass(string inputName, RoScriptParser parser)
    {
        name = inputName;
        builder.ActiveScript = name;

        var ruleSet = parser.rule_set();

        foreach (var statement in ruleSet.toplevelstatement())
            VisitTopLevelStatement(statement);

        return builder.OutputFinal();
    }

    private void VisitTopLevelStatement(ToplevelstatementContext topLevelContext)
    {
        builder.SetLineNumber(topLevelContext.Start.Line);

        if (topLevelContext is MacroDefinitionContext macroContext)
        {
            EnterMacroStatement(macroContext);
        }

        if (topLevelContext is EventDefinitionContext eventDefinitionContext)
            EnterEventStatement(eventDefinitionContext);

        if (topLevelContext is TopLevelFunctionDefinitionContext context)
        {
            VisitFunctionDefinitionContext(context.functionDefinition());
        }

        if (topLevelContext is StandaloneFunctionContext standaloneContext)
        {
            var id = standaloneContext.IDENTIFIER().GetText();

            switch (id)
            {
                case "RecoveryItem":
                    EnterRecoveryItemStatement(standaloneContext);
                    break;
                case "HiddenWarp":
                    EnterWarpStatement(standaloneContext, true);
                    break;
                case "Warp":
                    EnterWarpStatement(standaloneContext, false);
                    break;
                default:
                    throw new Exception("Unexpected top level function call: " + id);
            }
        }

        if (topLevelContext is TopLevelMacroCallContext macroCallContext)
            VisitMacroContext(macroCallContext.macrocall());

        if (topLevelContext is IncludeFileContextContext includeContext)
            VisitIncludeFile(includeContext);

        if (topLevelContext is ServerEventContextContext eventContext)
            VisitServerEventContext(eventContext);
    }

    private void VisitServerEventContext(ServerEventContextContext eventContext)
    {
        var eventName = eventContext.eventName.Text.Unescape();
        builder.RequiresServerEvent = eventName;
    }

    private void VisitIncludeFile(IncludeFileContextContext includeContext)
    {
        var fileName = includeContext.file.Text.Unescape();
        var dir = Path.GetDirectoryName(path);
        if (dir == null)
            dir = "";

        var includeFile = Path.Combine(dir, fileName);

        using var fs = new StreamReader(includeFile);
        var input = new AntlrInputStream(fs);

        var lexer = new RoScriptLexer(input);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new RoScriptParser(tokenStream);

        parser.AddErrorListener(new QueryLanguageErrorListener());

        var str = String.Empty;

        try
        {
            scriptNameStack.Push(name);
            name = fileName;
            builder.ActiveScript = name;
            var ruleSet = parser.rule_set();

            foreach (var statement in ruleSet.toplevelstatement())
                VisitTopLevelStatement(statement);

            name = scriptNameStack.Pop();
            builder.ActiveScript = name;
        }
        catch (Exception)
        {
            ServerLogger.LogError($"Failed to compile script {includeFile}!");
            throw;
        }
    }

    private void VisitFunctionDefinitionContext(FunctionDefinitionContext context)
    {
        var id = context.IDENTIFIER().GetText();

        switch (id)
        {
            case "Npc":
                EnterNpcStatement(context);
                break;
            case "Trader":
                EnterNpcStatement(context, true);
                break;
            case "Item":
                EnterItemStatement(context);
                break;
            case "ComboItem":
                EnterComboItemStatement(context);
                break;
            case "MapConfig":
                EnterMapConfigStatement(context);
                break;
            case "SkillHandler":
                EnterSkillHandlerStatement(context, false);
                break;
            case "AltSkillHandler":
                EnterSkillHandlerStatement(context, true);
                break;
            case "ServerConfig":
                EnterServerConfigStatement(context);
                break;
            default:
                throw new Exception("Unexpected top level statement: " + id);
        }
    }

    private void EnterMacroStatement(MacroDefinitionContext macroContext)
    {
        var macroName = macroContext.IDENTIFIER().GetText();
        var mp = macroContext.functionparam();

        if (mp != null)
        {
            var vardef = mp.expression();
            var macro = new ScriptMacro($"{name}:{macroName}", vardef.Length, macroContext.statementblock());


            for (var i = 0; i < vardef.Length; i++)
            {
                var v = vardef[i];
                macro.DefineVariable(i, v.GetText());
            }
            macroMap.Add(macroName, macro);
        }
        else

        {
            var macro = new ScriptMacro(name, 0, macroContext.statementblock());
            macroMap.Add(macroName, macro);
        }
    }

    private void EnterItemStatement(FunctionDefinitionContext functionContext)
    {
        //only expect one param, the item name
        var param = functionContext.functionparam();
        if (param.expression().Length != 1)
            throw new Exception($"Incorrect number of parameters on Item expression on line {param.start.Line}");

        var itemName = param.expression()[0].GetText();
        if (itemName.StartsWith("\""))
            itemName = itemName.Substring(1, itemName.Length - 2);

        var className = itemName.Replace(" ", "_").Replace(".", "").Replace("'", "").Replace("-", "_");

        sectionHandler = ItemSectionHandler;

        builder.StartItem(className);

        var statements = functionContext.block1;
        VisitStatementBlock(statements);

        builder.EndMethod();
        builder.EndClass();

        builder.EndItem(itemName, className);
    }

    private void EnterComboItemStatement(FunctionDefinitionContext functionContext)
    {
        //only expect one param, the item name
        var param = functionContext.functionparam();
        if (param.expression().Length < 3)
            throw new Exception($"Incorrect number of parameters on ComboItem expression on line {param.start.Line}");

        var expr = param.expression();

        var itemName = expr[0].GetText();
        if (itemName.StartsWith("\""))
            itemName = itemName.Substring(1, itemName.Length - 2);

        var comboItems = new List<string>();
        for (var i = 1; i < expr.Length; i++)
        {
            var txt = expr[i].GetText();
            
            if(txt == null || txt.Length <= 0)
                ErrorResult(functionContext, $"Invalid combo item parameters, combo item value is empty.");

            txt = txt!.Substring(1, txt.Length - 2);

            if (txt.Contains(" ") || txt.Contains('\"'))
                ErrorResult(functionContext, $"Invalid combo item parameters, item name cannot contain spaces or quotes. Name: {txt}");

            comboItems.Add(txt);
        }

        var className = itemName.Replace(" ", "_").Replace(".", "").Replace("'", "").Replace("-", "_");

        sectionHandler = ItemSectionHandler;

        builder.StartItem(className);

        var statements = functionContext.block1;
        VisitStatementBlock(statements);

        builder.EndMethod();
        builder.EndClass();

        builder.EndComboItem(itemName, className, comboItems);
    }


    public void ItemSectionHandler(StartSectionContext context)
    {
        builder.StartItemSection(context.IDENTIFIER().GetText());
    }

    private void EnterMapConfigStatement(FunctionDefinitionContext functionContext)
    {
        //only expect one param, the map name
        var param = functionContext.functionparam();
        if (param.expression().Length != 1)
            throw new Exception($"Incorrect number of parameters on MapConfig expression on line {param.start.Line}");

        var itemName = param.expression()[0].GetText();
        if (itemName.StartsWith("\""))
            itemName = itemName.Substring(1, itemName.Length - 2);

        var className = itemName.Replace(" ", "_").Replace(".", "").Replace("'", "").Replace("-", "_");

        sectionHandler = WarningForInvalidSectionHandler;

        builder.StartMap(className);

        var statements = functionContext.block1;
        VisitStatementBlock(statements);


        builder.EndMethod();
        builder.EndClass();
    }

    public void ConfigSectionHandler(StartSectionContext context)
    {
        builder.StartServerConfigSection(context.IDENTIFIER().GetText());
    }

    private void EnterServerConfigStatement(FunctionDefinitionContext functionContext)
    {
        //only expect one param, the map name
        var param = functionContext.functionparam();
        if (param.expression().Length != 1)
            throw new Exception($"Incorrect number of parameters on ServerConfig expression on line {param.start.Line}");

        var str = param.expression()[0].GetText();
        if (str.StartsWith("\""))
            str = str.Substring(1, str.Length - 2);

        builder.StartServerConfigHandler(str.Replace(" ", "_"));

        sectionHandler = ConfigSectionHandler;

        //do stuff
        var statements = functionContext.block1;
        VisitStatementBlock(statements);

        //methodBuilder.WithBody(block);

        builder.EndMethod();
        builder.EndClass();
    }

    private void EnterSkillHandlerStatement(FunctionDefinitionContext functionContext, bool isAltType)
    {
        //only expect one param, the item name
        var param = functionContext.functionparam();
        if (param.expression().Length != 1)
            throw new Exception($"Incorrect number of parameters on SkillHandler expression on line {param.start.Line}");

        eventHandlers.Clear();
        eventMacros.Clear();

        var str = param.expression()[0].GetText();
        if (str.StartsWith("\""))
            str = str.Substring(1, str.Length - 2);

        str = str.Replace(" ", "_");
        builder.StartMonsterSkillHandler(str);

        sectionHandler = SkillSectionHandler;

        var statements = functionContext.block1;
        VisitStatementBlock(statements);

        builder.EndMethod();

        foreach (var e in eventHandlers)
        {
            ActiveEventMacro = eventMacros.TryGetValue(e.Key, out var macro) ? macro : null;
            builder.StartSkillEventMethod(e.Key);
            VisitStatementBlock(e.Value);
            builder.EndMethod();
        }

        ActiveEventMacro = null;

        builder.CreateFinalSkillHandler();
        builder.EndClass();

        builder.EndMonsterSkillHandler(str, isAltType);
    }

    public void SkillSectionHandler(StartSectionContext context)
    {
        var name = context.IDENTIFIER().GetText();
        if (name == "OnDie" || name == "OnInit")
            builder.StartMonsterEventSection(name);
        else
            builder.StartMonsterSkillAiSection(name);
    }

    private string CleanString(string str)
    {
        if (str.StartsWith("\""))
            str = str.Substring(1, str.Length - 2);

        return str;
    }

    private void EnterEventStatement(EventDefinitionContext eventContext)
    {
        var str = eventContext.IDENTIFIER().GetText();


        var name = builder.StartEvent(str);

        sectionHandler = NpcSectionHandler;

        var statements = eventContext.statementblock();
        VisitStatementBlock(statements);

        //methodBuilder.WithBody(block);

        builder.EndMethod();
        builder.EndClass();

        builder.EndEvent(str, name);
    }


    private void EnterNpcStatement(FunctionDefinitionContext functionContext, bool isTrader = false)
    {
        //only expect one param, the map name
        var param = functionContext.functionparam();
        var expr = param.expression();


        if (expr.Length != 6 && expr.Length != 8)
            throw new Exception($"Incorrect number of parameters on Npc expression on line {param.start.Line}");

        var str = ExpressionContextString(expr[1]).Unescape();
        str = ScriptUtilityFunctions.CleanCsString(str);

        var mapName = ExpressionContextString(expr[0]);
        var displayName = ExpressionContextString(expr[1]);
        var spriteName = ExpressionContextString(expr[2]);
        var x = int.Parse(ExpressionContextString(expr[3]));
        var y = int.Parse(ExpressionContextString(expr[4]));
        var facingTxt = ExpressionContextString(expr[5]);
        var w = 0;
        var h = 0;

        if (expr.Length == 8)
        {
            w = int.Parse(ExpressionContextString(expr[6]));
            h = int.Parse(ExpressionContextString(expr[7]));
        }

        if (!int.TryParse(facingTxt, out var _))
            facingTxt = builder.GetConstValue(facingTxt);

        var name = builder.StartNpc(str, isTrader);

        sectionHandler = NpcSectionHandler;

        //do stuff
        var statements = functionContext.block1;
        VisitStatementBlock(statements);

        //methodBuilder.WithBody(block);

        //if (isTrader)
        //{
        //    builder.StartNpcSection("OnClick");
        //    builder.FunctionCall("OpenShop", false);
        //    builder.FunctionCallEnd();
        //    builder.OutputRaw(";");
        //    builder.EndLine();
        //    builder.EndMethod();
        //}
        //else

        builder.EndMethod();
        builder.EndClass();

        builder.EndNpc(name, displayName, mapName, displayName, spriteName, facingTxt, x, y, w, h);
    }

    private void EnterRecoveryItemStatement(StandaloneFunctionContext functionContext)
    {
        var param = functionContext.functionparam();
        var expr = param.expression();

        if (expr.Length != 5)
            throw new Exception($"Incorrect number of parameters on RecoveryItem expression on line {param.start.Line}");

        var itemName = param.expression()[0].GetText();
        if (itemName.StartsWith("\""))
            itemName = itemName.Substring(1, itemName.Length - 2);

        var className = itemName.Replace(" ", "_").Replace(".", "").Replace("'", "").Replace("-", "_");

        var hp1 = int.Parse(expr[1].GetText());
        var hp2 = int.Parse(expr[2].GetText());
        var sp1 = int.Parse(expr[3].GetText());
        var sp2 = int.Parse(expr[4].GetText());

        builder.StartItem(className);
        builder.StartItemSection("OnUse");
        if (hp1 > 0 || hp2 > 0)
        {
            builder.OutputRaw($"combatEntity.HealRange({hp1}, {hp2})");
            builder.EndLine(functionContext.start.Line);
        }
        if (sp1 > 0 || sp2 > 0)
        {
            builder.OutputRaw($"combatEntity.RecoverSpRange({sp1}, {sp2})");
            builder.EndLine(functionContext.start.Line);
        }

        builder.EndMethod();
        builder.EndClass();
        builder.EndItem(itemName, className);
    }

    private void OutputWarpStatement(ExpressionContext expression)
    {

    }

    private void EnterWarpStatement(StandaloneFunctionContext functionContext, bool isHidden)
    {
        var param = functionContext.functionparam();
        var expr = param.expression();

        var v = new ScriptTopLevelParameters(expr);

        if (v.VerifySignature("ssiiiisiiii"))
            v.SetParameters(new[] { "mapName", "signalName", "x", "y", "w", "h", "destMap", "dx", "dy", "dw", "dh" });
        if (v.VerifySignature("ssiiiisii"))
            v.SetParameters(new[] { "mapName", "signalName", "x", "y", "w", "h", "destMap", "dx", "dy" });
        if (v.VerifySignature("sssiiiisiiii"))
            v.SetParameters(new[] { "mapName", "signalName", "displayName", "x", "y", "w", "h", "destMap", "dx", "dy", "dw", "dh" });
        if (v.VerifySignature("sssiiiisii"))
            v.SetParameters(new[] { "mapName", "signalName", "displayName", "x", "y", "w", "h", "destMap", "dx", "dy" });

        if (!v.HasParametersSet())
            throw new Exception($"The Warp expression on line {param.start.Line} could not match the parameters provided to any known function signature.");

        var str = v["signalName"].String.Unescape();
        if (string.IsNullOrWhiteSpace(str))
            str = "UnnamedWarp_" + v["mapName"].String.Unescape();
        str = ScriptUtilityFunctions.CleanCsString(str);

        var name = builder.StartNpc(str, false);
        //builder.OutputRaw($"npc.RevealAsEffect(NpcEffectType.MapWarp, {(v["displayName"].IsString ? v["displayName"].String : "")};");
        //builder.EndLine(functionContext.start.Line);
        builder.OutputRaw($"npc.RegisterLink({v["destMap"]}, {v["dx"]}, {v["dy"]})");
        builder.EndLine(functionContext.start.Line);
        builder.OutputRaw($"npc.RemoveIfLinksInvalid()");
        builder.EndLine(functionContext.start.Line);
        if(isHidden){
            builder.OutputRaw($"npc.HideFromView()");
            builder.EndLine(functionContext.start.Line);
        }
        builder.StartNpcSection("OnTouch");
        builder.OutputRaw($"state.MoveTo({v["destMap"]}, {v["dx"]}, {v["dy"]}, {v["dw"]}, {v["dh"]})");
        builder.EndLine(functionContext.start.Line);
        builder.EndMethod();
        builder.EndClass();
        builder.EndNpc(name, v["displayName"], v["mapName"], v["signalName"], "\"WARP\"", "4", v["x"], v["y"], v["w"], v["h"]);
    }

    public void NpcSectionHandler(StartSectionContext context)
    {
        var timer = ParseDecimal(context, context.DECIMAL(), -1);
        builder.StartNpcSection(context.IDENTIFIER().GetText(), timer);
    }

    private void WarningForInvalidSectionHandler(StartSectionContext context)
    {
        ServerLogger.LogWarning($"{name} line {context.start.Line}: Section definition '{context.IDENTIFIER()}' ignored as this type does not allow it.");
    }

    private void VisitStatementBlock(StatementblockContext blockContext)
    {
        builder.SetLineNumber(blockContext.Start.Line);

        switch (blockContext)
        {
            case SingleStatementContext context:
                VisitStatement(context.statement());
                break;
            case StatementGroupContext context:
                foreach (var s in context.statement())
                    VisitStatement(s);
                break;
            default:
                ErrorResult(blockContext);
                break;
        }

    }

    private void VisitStatement(StatementContext statementContext)
    {
        builder.SetLineNumber(statementContext.Start.Line);

        switch (statementContext)
        {
            case StatementExpressionContext context:
                VisitExpression(context.expression());
                builder.EndLine(context.start.Line);
                break;
            case ForLoopContext context:
                VisitForLoop(context);
                break;
            case WhileLoopContext context:
                VisitWhileLoop(context);
                break;
            case BreakLoopContext context:
                VisitBreak(context);
                break;
            case StartSectionContext context:
                if (sectionHandler != null)
                    sectionHandler(context);
                break;
            case ReturnStatementContext context:
                if (context.expression() != null)
                {
                    builder.StartReturn();
                    VisitExpression(context.expression());
                    builder.EndReturn();
                }
                else
                    builder.OutputReturn();
                break;
            case SwitchStatementContext context:
                VisitSwitchStatement(context);
                break;
            case IfStatementContext context:
                VisitIfStatement(context);
                break;
            default:
                ErrorResult(statementContext);
                break;
        }
    }

    private void VisitIfStatement(IfStatementContext context)
    {
        builder.OutputRaw("if (");
        VisitExpression(context.expression());
        builder.OutputRaw(")");
        builder.EndLine();

        if (!builder.UseStateMachine)
        {
            builder.OpenScope();
            VisitStatementBlock(context.block1);
            builder.CloseScope();

            if (context.block2 != null)
            {
                builder.OutputRaw("else");
                builder.EndLine();

                builder.OpenScope();
                VisitStatementBlock(context.block2);
                builder.CloseScope();
            }
        }
        else
        {
            builder.OpenStateIf();

            var falsePointer = 0;

            if (context.block2 == null)
            {
                falsePointer = builder.GotoFutureBlock();
            }
            else
            {
                builder.OutputRaw("else");
                builder.EndLine();

                falsePointer = builder.OpenStateElse();
            }

            builder.AdvanceBlock(true);

            VisitStatementBlock(context.block1);

            if (context.block2 == null)
            {
                builder.AdvanceBlock();
                builder.RegisterGotoDestination(falsePointer);
            }
            else
            {
                var ptr2 = builder.GotoFutureBlock();
                builder.AdvanceBlock(true);
                builder.RegisterGotoDestination(falsePointer);

                VisitStatementBlock(context.block2);

                builder.AdvanceBlock();
                builder.RegisterGotoDestination(ptr2);
            }
        }
    }

    private void VisitSwitchStatement(SwitchStatementContext context)
    {
        var items = context.switchblock();

        if (!builder.UseStateMachine)
        {
            builder.OutputRaw("switch (");
            VisitExpression(context.expr);
            builder.OutputRaw(")");
            builder.EndLine();

            for (var i = 0; i < items.Length; i++)
            {
                if (i > 0)
                {
                    builder.OutputBreak();
                }
            }
            builder.OpenScope();
        }
        else
        {
            VisitExpression(context.expr);
            builder.OpenSwitch();

            var breakPtr = builder.GetFutureBlockPointer();
            builder.breakPointerStack.Push(breakPtr);
            var statementPtr = -1;

            foreach (var t in items)
            {
                var item = t as SwitchCaseContext;
                if (item == null)
                    ErrorResult(context, "Expecting case in switch block.");

                builder.OutputRaw("if ((");
                builder.OutputSwitchOption();
                builder.OutputRaw(") == ");
                VisitEntity(item!.entity());
                builder.OutputRaw(")");
                builder.EndLine();

                builder.OpenStateIf();

                var ptr = builder.GotoFutureBlock();
                builder.AdvanceBlock(true);

                if (statementPtr != -1)
                {
                    builder.RegisterGotoDestination(statementPtr);
                    statementPtr = -1;
                }

                var hasStatements = false;

                foreach (var s in item.statement())
                {
                    VisitStatement(s);
                    hasStatements = true;
                }

                if (!hasStatements)
                {
                    if (statementPtr == -1)
                        statementPtr = builder.GotoFutureBlock();
                    else
                        builder.GotoFutureBlock(statementPtr);
                }

                builder.GotoFutureBlock(breakPtr);

                builder.AdvanceBlock(true);
                builder.RegisterGotoDestination(ptr);
                //builder.breakPointerStack.Pop();
            }

            builder.AdvanceBlock();
            builder.RegisterGotoDestination(breakPtr);
            builder.breakPointerStack.Pop();
        }


    }

    private void VisitSwitchCaseStatement(SwitchCaseContext context)
    {

    }

    private void VisitWhileLoop(WhileLoopContext whileContext)
    {
        if (!builder.UseStateMachine)
        {
            builder.OutputRaw("while (");
            VisitExpression(whileContext.comp);
            builder.OutputRaw(")");
            builder.EndLine();

            builder.OpenScope();
            VisitStatementBlock(whileContext.statementblock());
            builder.CloseScope();
        }
        else
        {
            var start = builder.AdvanceBlock();
            var end = builder.GetFutureBlockPointer();

            builder.OutputRaw("if (");
            VisitExpression(whileContext.expression());
            builder.OutputRaw(")");
            builder.EndLine();

            var loop = builder.GotoFutureBlock(true);
            builder.GotoFutureBlock(end);

            builder.AdvanceBlock(true);
            builder.RegisterGotoDestination(loop);
            builder.breakPointerStack.Push(end);

            VisitStatementBlock(whileContext.statementblock());

            builder.GotoBlock(start);

            builder.AdvanceBlock(true);
            builder.RegisterGotoDestination(end);
            builder.breakPointerStack.Pop();
        }
    }

    private void VisitForLoop(ForLoopContext forLoopContext)
    {
        builder.OutputRaw("for (");
        VisitExpression(forLoopContext.asgn);
        builder.OutputRaw("; ");
        VisitExpression(forLoopContext.comp);
        builder.OutputRaw("; ");
        VisitExpression(forLoopContext.inc);
        builder.OutputRaw(")");
        builder.EndLine();

        builder.OpenScope();
        VisitStatementBlock(forLoopContext.statementblock());
        builder.CloseScope();
    }

    private void VisitBreak(BreakLoopContext breakContext)
    {
        if (!builder.UseStateMachine)
        {
            if (breakContext.count != null)
                ErrorResult(breakContext, $"You cannot break a specific count outside of OnClick and OnTouch blocks.");
            builder.OutputRaw("break;");
            builder.EndLine();
        }
        else
        {
            if (breakContext.count != null)
            {
                var count = int.Parse(breakContext.count.Text);
                if (builder.breakPointerStack.Count < count)
                    ErrorResult(breakContext, $"You cannot break {count} times here, as we don't have enough places to break from.");

                var ptr = builder.breakPointerStack.Skip(count - 1).First();

                builder.GotoFutureBlock(ptr);
                builder.EndLine();
            }
            else
            {
                if (!builder.breakPointerStack.TryPeek(out var ptr))
                    ErrorResult(breakContext,
                        "You cannot use a break statement here, as there is nowhere you can break to.");

                builder.GotoFutureBlock(ptr);
                builder.EndLine();
            }
        }
    }

    private void VisitExpression(ExpressionContext expressionContext)
    {
        switch (expressionContext)
        {
            case ExpressionUnaryContext unaryContext: VisitUnaryExpression(unaryContext); break;
            case ArithmeticMultContext multiContext: VisitOperator(multiContext.left, multiContext.right, multiContext.type.Text); break;
            case ArithmeticPlusContext plusContext: VisitOperator(plusContext.left, plusContext.right, plusContext.type.Text); break;
            case BitwiseAndContext bitwiseAndContext: VisitOperator(bitwiseAndContext.left, bitwiseAndContext.right, bitwiseAndContext.type.Text); break;
            case LogicalAndContext logicalAndContext: VisitOperator(logicalAndContext.left, logicalAndContext.right, logicalAndContext.type.Text); break;
            case ComparisonContext comparisonContext: VisitOperator(comparisonContext.left, comparisonContext.right, comparisonContext.comparison_operator().GetText()); break;
            case ExpressionEntityContext entityContext: VisitEntity(entityContext.entity()); break;
            case FunctionCallExpressionContext functionCallContext: VisitFunctionCallExpression(functionCallContext); break;
            case ExpressionFunctionDefinitionContext functionDefinitionContext: VisitFunctionDefinitionContext(functionDefinitionContext.functionDefinition()); break;
            case AreaTypeContext areaTypeContext: VisitAreaType(areaTypeContext); break;
            case VarDeclarationContext varDeclarationContext: VisitVarDeclaration(varDeclarationContext); break;
            case LocalDeclarationContext context: VisitLocalDeclaration(context); break;
            case ArithmeticParensContext parensContext:
                builder.OutputRaw("(");
                VisitExpression(parensContext.expression());
                builder.OutputRaw(")");
                break;
            case NotOperationContext notContext:
                builder.OutputRaw("!(");
                VisitExpression(notContext.expression());
                builder.OutputRaw(")");
                break;
            case AssignmentExpressionContext assignmentContext:
                VisitAssignment(assignmentContext.assignment());
                break;
            default:
                ErrorResult(expressionContext);
                break;
        }
    }

    private void VisitOperator(ExpressionContext left, ExpressionContext right, string optext)
    {
        builder.OutputRaw("(");
        VisitExpression(left);
        builder.OutputRaw($" {optext} ");
        VisitExpression(right);
        builder.OutputRaw(")");
    }

    private void VisitUnaryExpression(ExpressionUnaryContext context)
    {
        var v = context.IDENTIFIER().GetText();
        var t = context.type;

        builder.OutputIdentifier(v);
        builder.OutputRaw(t.Text); //honestly we don't care if it's ++ or --, we are outputting as text
    }

    private void VisitAreaType(AreaTypeContext areaTypeContext)
    {
        builder.OutputRaw("Area.CreateAroundPoint(");

        var fparam = areaTypeContext.functionparam();

        if (fparam == null)
            ErrorResult(areaTypeContext, $"Area shorthand expression missing parameters!");

        var pos = 0;

        foreach (var t in fparam!.expression())
        {
            if (pos > 0)
                builder.AddComma();

            VisitExpression(t);

            pos++;
        }

        if (pos != 4)
            ErrorResult(areaTypeContext, $"Area shorthand expression has incorrect number of parameters (has {pos} expected 4)!");

        builder.OutputRaw(")");
    }

    private void VisitFunctionCallExpression(FunctionCallExpressionContext functionContext)
    {
        //VisitFunctionCall((FunctionCallContext)functionCallContext.function())

        var func = functionContext.function();

        switch (func)
        {
            case FunctionCallContext callContext: VisitFunctionCall(callContext); break;
            case RegularMacroCallContext macroContext: VisitMacroContext(macroContext.macrocall()); break;
        }
    }

    private void VisitMacroContext(MacrocallContext macroContext)
    {
        var id = macroContext.IDENTIFIER().GetText();
        var fparam = macroContext.functionparam();

        if (!macroMap.TryGetValue(id, out var macro))
        {
            var found = false;

            if (builder.ActiveMacro != null && builder.ActiveMacro.TryGetVariable(id, out var expression))
            {
                if (macroMap.TryGetValue(expression.GetText(), out macro))
                    found = true;
            }

            if (!found)
                ErrorResult(macroContext, $"Cannot find a macro defined with the name \"{id}\".");
        }

        if (activeMacros.Contains(id))
            ErrorResult(macroContext, $"You cannot call macro {id} as it is currently already active. A macro cannot call itself or be called by any siblings.");

        Debug.Assert(macro != null);

        activeMacros.Add(id);
        builder.PushMacro(macro);

        if (fparam != null)
        {
            var p = fparam.expression();

            var pos = 0;

            foreach (var t in p)
            {
                macro.SetValue(pos, t);

                pos++;
            }
        }

        VisitStatementBlock(macro.Context);

        builder.PopMacro();
        activeMacros.Remove(id);
    }

    private void VisitFunctionCall(FunctionCallContext functionContext, bool isChained = false)
    {
        while (true)
        {
            var id = functionContext.IDENTIFIER().GetText();
            var fparam = functionContext.functionparam();
            var isConditional = functionContext.condition != null;
            var isTerminal = builder.IsTerminalFunction(id);
            var hasEventBlock = functionContext.eventblock != null;

            if (isConditional)
            {
                builder.OutputRaw("if (");
                VisitExpression(functionContext.condition!);
                builder.OutputRaw(")");
                builder.EndLine();
                builder.OpenScope();
            }

            if (isTerminal || hasEventBlock)
                builder.OutputRaw("if (");

            builder.FunctionCall(id, isChained);

            if (fparam != null)
            {
                var p = fparam.expression();

                var pos = 0;

                foreach (var t in p)
                {
                    if (pos > 0) builder.AddComma();

                    VisitExpression(t);

                    pos++;
                }
            }

            builder.FunctionCallEnd();

            var dotFunction = functionContext.function();

            if (dotFunction != null)
            {
                functionContext = (FunctionCallContext)dotFunction;
                isChained = true;
                continue;
            }

            if (isTerminal || hasEventBlock)
            {
                builder.OutputRaw(")");
                builder.OpenScope();
                if (hasEventBlock)
                {
                    var eventName = builder.OutputEventCall();
                    if (builder.ActiveMacro != null)
                        eventMacros.Add(eventName, builder.ActiveMacro);
                    eventHandlers.Add(eventName, functionContext.eventblock!);
                }

                if (isTerminal)
                    builder.OutputReturn();
                builder.CloseScope();
            }

            if (isConditional)
            {
                if (!isTerminal)
                    builder.OutputRaw(";");
                builder.EndLine();
                builder.CloseScope();
            }

            break;
        }
    }

    public void VisitFunctionTerminalFunctionHandler(FunctionCallContext context)
    {

    }

    public void VisitLocalDeclaration(LocalDeclarationContext localDeclarationContext)
    {
        //if(!builder.UseStateMachine)
        //    ErrorResult(localDeclarationContext, "Cannot use local type for variable not used within an NPC OnClick or OnTouch command.");


        var type = localDeclarationContext.type.Text.ToLower();
        var isString = (type == "localstr");

        var assn = localDeclarationContext.assignment();
        if (assn is VarAssignmentContext varContext)
        {
            builder.DefineVariable(varContext.IDENTIFIER().GetText(), isString, true);
        }

        VisitAssignment(localDeclarationContext.assignment());
    }

    public void VisitVarDeclaration(VarDeclarationContext varDeclarationContext)
    {
        var assn = varDeclarationContext.assignment();
        var type = varDeclarationContext.type.Text.ToLower();
        var isString = (type == "string");

        if (builder.UseStateMachine && assn is VarAssignmentContext varContext)
        {
            builder.DefineVariable(varContext.IDENTIFIER().GetText(), isString, false);
        }
        else
        {
            var id = ((VarAssignmentContext)assn).IDENTIFIER().GetText();

            if (!builder.HasUserVariable(id))
            {
                builder.OutputRaw("var ");
                builder.AddUserVariable(id);
            }
        }

        VisitAssignment(varDeclarationContext.assignment());
    }

    public void VisitAssignment(AssignmentContext assignmentContext)
    {
        switch (assignmentContext)
        {
            case VarAssignmentContext context:
                builder.OutputVariable(context.IDENTIFIER().GetText());
                builder.OutputRaw(" = ");
                VisitExpression(context.expression());
                break;
            case SpecialAssignmentContext context:
                var left = context.IDENTIFIER().GetText();
                if (context.specialassignment_operator().DE() != null)
                    builder.OutputRaw($"{left} = {left} / (");
                if (context.specialassignment_operator().TE() != null)
                    builder.OutputRaw($"{left} = {left} * (");
                if (context.specialassignment_operator().PE() != null)
                    builder.OutputRaw($"{left} = {left} + (");
                if (context.specialassignment_operator().ME() != null)
                    builder.OutputRaw($"{left} = {left} - (");
                VisitExpression(context.expression());
                builder.OutputRaw(")");
                break;
            default:
                ErrorResult(assignmentContext);
                break;
        }
    }

    private void VisitEntity(EntityContext entityContext)
    {
        switch (entityContext)
        {
            case StringEntityContext context:
                builder.OutputRaw(context.GetText());
                break;
            case NumericConstContext context:
                builder.OutputRaw(ParseDecimal(context, context.DECIMAL()).ToString());
                break;
            case VariableContext context:
                if (builder.ActiveMacro != null && builder.ActiveMacro.HasVariable(context.GetText()))
                    VisitExpression(builder.ActiveMacro.GetVariable(context.GetText()));
                else if (ActiveEventMacro != null && ActiveEventMacro.HasVariable(context.GetText()))
                    VisitExpression(ActiveEventMacro.GetVariable(context.GetText()));
                else
                    builder.OutputVariable(context.GetText());
                break;
            default:
                ErrorResult(entityContext);
                break;
        }
    }

    private string ExpressionContextString(ExpressionContext expression)
    {
        var val = expression.GetText();

        if (builder.ActiveMacro != null && builder.ActiveMacro.TryGetVariable(val, out var expr))
            return expr.GetText();


        if (ActiveEventMacro != null && ActiveEventMacro.TryGetVariable(val, out var expr2))
            return expr2.GetText();

        return val;
    }

    private StringBuilder decStringBuilder = new(12);

    private int ParseDecimal(ParserRuleContext context, ITerminalNode? node, int defaultValue = -1)
    {
        if (node == null)
            return defaultValue;

        var value = node.GetText();
        if (value == null)
            return defaultValue;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return v;

        try
        {
            var sum = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '.')
                {
                    decStringBuilder.Append(c);
                    continue;
                }

                if (c == 's')
                    sum += (int)(float.Parse(decStringBuilder.ToString(), CultureInfo.InvariantCulture) * 1000);
                if (c == 'm')
                    sum += (int)(float.Parse(decStringBuilder.ToString(), CultureInfo.InvariantCulture) * 60 * 1000);
                if (c == 'h')
                    sum += (int)(float.Parse(decStringBuilder.ToString(), CultureInfo.InvariantCulture) * 60 * 60 * 1000);
                if (c == '%')
                {
                    if (i + 1 < value.Length)
                        ErrorResult(context, $"Could not parse string {value} into decimal, the '%' symbol was in an unexpected location.");
                    sum = (int)(float.Parse(decStringBuilder.ToString(), CultureInfo.InvariantCulture) * 10);
                }
                decStringBuilder.Clear();
            }

            if (decStringBuilder.Length > 0)
                sum += (int)float.Parse(decStringBuilder.ToString(), CultureInfo.InvariantCulture);

            return sum;
        }
        catch
        {
            ErrorResult(context, $"Could not parse decimal from string {value}");
            throw;
        }
    }

    private void ErrorResult(ParserRuleContext context, string? message = null)
    {
        var text = context.GetText();
        var script = name;
        if (builder.ActiveMacro != null)
            script = builder.ActiveMacro.CurrentScript;
        if (ActiveEventMacro != null)
            script = ActiveEventMacro.CurrentScript;

        if (string.IsNullOrWhiteSpace(message))
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException($"{script} line {context.start.Line}: Unable to parse statement.");
            else
                throw new InvalidOperationException($"{script} line {context.start.Line}: Unable to parse statement: {text}");
        }
        else
            throw new InvalidOperationException($"{script} line {context.start.Line}: {message}");



    }
}