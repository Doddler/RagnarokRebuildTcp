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
        if (isNewCharacter)
        {
            return; //fixme
        }

        var chName = msg.ReadString();

        //ServerLogger.Log($"Running Enter for accountId {accountId}");

        var req = new LoadCharacterRequest(accountId, chName);
        RoDatabase.EnqueueDbRequest(req);

        if (req.HasCharacter)
        {
            ServerLogger.Log($"Client has an existing character! Character name {req.Name}.");
            return;
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
}