using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;

namespace RoRebuildServer.EntityComponents.Npcs;

public class NpcInteractionState
{
    public Entity NpcEntity;
    public Player? Player;
    public int Step;
    public int InteractionResult;

    public int[] ValuesInt = new int[5];
    public string?[] ValuesString = new string[5];

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
        InteractionResult = 0;
    }


    public void ShowSprite(string spriteName, int pos)
    {
        Console.WriteLine("ShowSprite " + spriteName);
    }


    public void Option(params string[] options)
    {
        Console.WriteLine("Option");
        foreach(var s in options)
            Console.WriteLine(" - " + s);
    }

    public void MoveTo(string mapName, int x, int y)
    {
        MoveTo(mapName, x, y, 1, 1);
    }


    public void MoveTo(string mapName, int x, int y, int width, int height)
    {
        ServerLogger.Log("Warp to " + mapName);
        if (Player == null)
            return;

        var ch = Player.Character;
        var ce = Player.CombatEntity;

        if (ch.Map == null)
            return;

        if (!Player.WarpPlayer(mapName, x, y, 1, 1, false))
            ServerLogger.LogWarning($"Failed to move player to {mapName}!");
    }

    public void Dialog(string name, string text)
    {
        Console.WriteLine($"Dialog {name}: {text}");
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
        var calcBase = (DateTime.Now.ToFileTimeUtc() / 86400) * 100 + seed;

        var step1 = (calcBase << 11) ^ calcBase;
        var step2 = (step1.RotateRight(8)) ^ step1;

        return (int)(step2 % max);
    }
}