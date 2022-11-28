using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace RoRebuildServer.EntityComponents.Npcs;

public class NpcInteractionState
{
    public Entity NpcEntity;
    public Player? Player;
    public int Step;
    public int OptionResult = -1;
    public NpcInteractionResult InteractionResult { get; set; }
    
    public const int StorageCount = 5;

    public int[] ValuesInt = new int[StorageCount];
    public string?[] ValuesString = new string[StorageCount];

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

    public void FocusNpc()
    {
        if (Player == null)
            return;

        CommandBuilder.SendFocusNpc(Player, NpcEntity.Get<Npc>());
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

    public void MoveTo(string mapName, int x, int y)
    {
        MoveTo(mapName, x, y, 1, 1);
    }
    
    public void MoveTo(string mapName, int x, int y, int width, int height)
    {
        //ServerLogger.Log("Warp to " + mapName);
        if (Player == null)
            return;

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
        return 999;
    }

    public void DropZeny(int zeny)
    {

    }

    public int Random(int max) => GameRandom.Next(max);
    public int Random(int min, int max) => GameRandom.Next(min, max);

    public int DeterministicRandom(int seed, int max)
    {
        var calcBase = (DateTime.Today.ToFileTimeUtc() / 86400) * 100 + seed;

        var step1 = (calcBase << 11) ^ calcBase;
        var step2 = (step1.RotateRight(8)) ^ step1;
        step2 = Math.Abs(step2);

        return (int)(step2 % max);
    }
}