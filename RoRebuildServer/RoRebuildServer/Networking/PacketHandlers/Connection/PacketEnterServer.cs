using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Config;
using RoRebuildServer.Database;
using RoRebuildServer.Database.Requests;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Util;
using System.Buffers;
using RebuildSharedData.Enum.EntityStats;

namespace RoRebuildServer.Networking.PacketHandlers.Connection;


[ClientPacketHandler(PacketType.EnterServer)]
public class PacketEnterServer : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Character != null)
            return;

        var accountId = connection.AccountId;
        var isNewCharacter = msg.ReadBoolean();
        var chName = msg.ReadString();

        if (!isNewCharacter)
        {

            var req = new LoadCharacterRequest(accountId, chName);
            RoDatabase.EnqueueDbRequest(req);
            return;
        }

        if (chName.Length > 30)
        {
            CommandBuilder.ErrorMessage(connection, $"The character name you entered is too long, it must be 30 or fewer characters in length.");
            return;
        }

        if (string.IsNullOrWhiteSpace(chName) || chName.Length < 2)
        {
            CommandBuilder.ErrorMessage(connection, $"The character name is invalid. It must be at least 2 characters long.");
            return;
        }

        var t = chName.Trim();
        if (t != chName)
        {
            CommandBuilder.ErrorMessage(connection, $"The character name is invalid. Names cannot contain leading or trailing whitespace.");
        }
        
        var charData = ArrayPool<int>.Shared.Rent((int)PlayerStat.PlayerStatsMax);
        for(var i = 0; i < charData.Length; i++)
            charData[i] = 0;

        var head = msg.ReadInt32();
        var hair = msg.ReadInt32();
        var slot = (int)msg.ReadByte();
        
        var total = 0;
        var error = false;
        for (var i = (int)PlayerStat.Str; i <= (int)PlayerStat.Luck; i++)
        {
            var stat = (int)msg.ReadByte();
            total += stat;
            charData[i] = stat;
            if (stat < 1 || stat > 9)
                error = true;
        }
        var isMale = msg.ReadBoolean();

        if (total != 33) error = true;
        if (head < 0 || head > 19) error = true;
        if (hair < 0 || hair > 8) error = true;
        if (slot < 0 || slot > 2) error = true;

        if (error)
        {
            CommandBuilder.ErrorMessage(connection, $"Cannot create character, the server rejected the request for incorrectly submitted data.");
            ArrayPool<int>.Shared.Return(charData);
            return;
        }

        charData[(int)PlayerStat.Level] = 1;
        charData[(int)PlayerStat.Head] = head;
        charData[(int)PlayerStat.HairId] = hair;
        charData[(int)PlayerStat.Gender] = isMale ? 0 : 1;

        var newReq = new CreateCharacterRequest(connection, accountId, slot, chName, charData);
        RoDatabase.EnqueueDbRequest(newReq);
    }

    ////var name = "Player " + GameRandom.NextInclusive(0, 999);
    //var name = accountName;

    //var charData = ArrayPool<int>.Shared.Rent((int)PlayerStat.PlayerStatsMax);

    //var newReq = new SaveCharacterRequest(name, accountId);
    //await RoDatabase.ExecuteDbRequestAsync(newReq);

    //ArrayPool<int>.Shared.Return(charData, true);

    //var loadReq = new LoadCharacterRequest(accountId); //database will assign us a guid, use that to load back the character
    //await RoDatabase.ExecuteDbRequestAsync(loadReq);

    //return loadReq;

    //RoDatabase.EnqueueDbRequest();

    ////CommandBuilder.InformEnterServer(connection.Character, networkPlayer);
}
