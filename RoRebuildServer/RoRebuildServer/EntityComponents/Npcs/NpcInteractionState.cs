using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation;
using System;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RoRebuildServer.EntityComponents.Npcs;

public class NpcInteractionState
{
    public Entity NpcEntity;
    public Player? Player;
    public int Step;
    public int OptionResult = -1;
    
    public const int StorageCount = 10;

    public int[] ValuesInt = new int[StorageCount];
    public string?[] ValuesString = new string[StorageCount];

    public NpcInteractionResult InteractionResult { get; set; }
    public bool IsTouchEvent { get; set; }


    public void Reset()
    {
        NpcEntity = Entity.Null;
        Player = null;

        for (var i = 0; i < ValuesInt.Length; i++)
        {
            ValuesInt[i] = 0;
            ValuesString[i] = null;
        }
    }

    public void BeginInteraction(ref Entity npc, Player player)
    {
#if DEBUG
        if(NpcEntity != Entity.Null || Player != null)
            ServerLogger.LogWarning($"Attempting to begin npc interaction with {npc} but an interaction already appears to exist!");
#endif

        NpcEntity = npc;
        Player = player;
        Step = 0;
        InteractionResult = NpcInteractionResult.WaitForTime;
    }

    public void CancelInteraction()
    {
        if (Player == null)
            return;

        if (!NpcEntity.IsAlive())
        {
            Player.IsInNpcInteraction = false;
            Reset();
            return;
        }

        ServerLogger.Debug($"Player {Player} had an NPC interaction cancelled.");

        var npc = NpcEntity.Get<Npc>();
        npc.CancelInteraction(Player); //this will trigger a reset on this object
    }

    public void ContinueInteraction()
    {
        if (Player == null)
            return;

        var npc = NpcEntity.Get<Npc>();
        npc.Advance(Player);
    }
    
    public void OptionInteraction(int result)
    {
        if (Player == null)
            return;

        var npc = NpcEntity.Get<Npc>();
        npc.OptionAdvance(Player, result);
    }

    public void SetFlag(string name, int value)
    {
        if (name.Length > 16)
            throw new Exception($"Npc flag '{name}' is too long! It must be 16 or fewer characters in length.");
        Player?.SetNpcFlag(name, value);
    }
    public int GetFlag(string name) => Player?.GetNpcFlag(name) ?? 0;
    public int Level => Player?.GetStat(CharacterStat.Level) ?? 0;
    public int UnusedSkillPoints => Player?.GetData(PlayerStat.SkillPoints) ?? 99;
    public int JobId => Player?.GetData(PlayerStat.Job) ?? 0;
    public void ChangePlayerJob(int jobId) => Player?.ChangeJob(jobId);
    public void SkillReset() => Player?.SkillReset();

    public void FocusNpc()
    {
        if (Player == null)
            return;

        CommandBuilder.SendFocusNpc(Player, NpcEntity.Get<Npc>(), true);
    }

    public void ReleaseFocus()
    {
        if (Player == null)
            return;

        CommandBuilder.SendFocusNpc(Player, NpcEntity.Get<Npc>(), false);
    }

    public void ShowEffectOnPlayer(string effectName)
    {
        if (Player == null || Player.Character.Map == null) return;
        if (!NpcEntity.TryGet<WorldObject>(out var npc)) return;

        var id = DataManager.EffectIdForName[effectName];

        Player.Character.Map.AddVisiblePlayersAsPacketRecipients(Player.Character);
        CommandBuilder.SendEffectOnCharacterMulti(Player.Character, id);
        CommandBuilder.ClearRecipients();
    }

    public void ChangePlayerGender()
    {
        if (Player == null || Player.Character.Map == null) return;
        var gender = Player.GetData(PlayerStat.Gender);
        Player.SetData(PlayerStat.Gender, gender == 0 ? 1 : 0);
        Player.Character.Map.RefreshEntity(Player.Character);
        Player.UpdateStats();
    }

    public void ChangePlayerHairToRandom()
    {
        if (Player == null || Player.Character.Map == null) return;
        Player.SetData(PlayerStat.Head, GameRandom.NextInclusive(0, 28));
        Player.Character.Map.RefreshEntity(Player.Character);
        Player.UpdateStats();
    }

    public void ChangePlayerAppearanceToRandom()
    {
        if (Player == null || Player.Character.Map == null) return;
        Player.SetData(PlayerStat.Gender, GameRandom.NextInclusive(0, 1));
        Player.SetData(PlayerStat.Head, GameRandom.NextInclusive(0, 28));
        Player.Character.Map.RefreshEntity(Player.Character);
        Player.UpdateStats();
    }

    public void ResetCharacterToInitialState()
    {
        if (Player == null || Player.Character.Map == null) return;
        Player.SetData(PlayerStat.Status, 0);
        Player.Init();
        Player.Character.Map.RefreshEntity(Player.Character);
        Player.UpdateStats();
    }


    public void ShowSprite(string spriteName, int pos)
    {
        //Console.WriteLine("ShowSprite " + spriteName);

        if (Player == null)
            return;
        
        CommandBuilder.SendNpcShowSprite(Player, spriteName, pos);
    }
    
    public void Option(params string[] options)
    {
        //Console.WriteLine("Option");
        //foreach(var s in options)
        //    Console.WriteLine(" - " + s);

        if (Player == null)
            return;
        
        CommandBuilder.SendNpcOption(Player, options);
    }

    public void OpenShop()
    {

    }

    public void OpenShop(string[] items)
    {

    }

    public void MoveTo(string mapName, int x, int y)
    {
        MoveTo(mapName, x, y, 1, 1);
    }
    
    public void MoveTo(string mapName, int x, int y, int width, int height)
    {
        //ServerLogger.Log("Warp to " + mapName);
        if (Player == null)
            return;

        Player.Character.StopMovingImmediately();

        var ch = Player.Character;
        var ce = Player.CombatEntity;

        if (ch.Map == null)
            return;

        ServerLogger.Log($"Moving player {Player.Name} via npc or warp to map {mapName} {x},{y}");

        if (!Player.WarpPlayer(mapName, x, y, 1, 1, false))
            ServerLogger.LogWarning($"Failed to move player to {mapName}!");
    }

    public void Dialog(string name, string text)
    {
        if (Player == null)
            return;

        //Console.WriteLine($"Dialog {name}: {text}");
        CommandBuilder.SendNpcDialog(Player, name, text);
    }

    public void OpenStorage()
    {
        Console.WriteLine("OpenStorage");
    }

    public int GetZeny()
    {
        return 99999;
    }

    public void DropZeny(int zeny)
    {

    }
}