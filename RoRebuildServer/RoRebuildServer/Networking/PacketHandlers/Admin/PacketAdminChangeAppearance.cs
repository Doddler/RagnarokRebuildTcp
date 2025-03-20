using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Networking.PacketHandlers.Admin;

[ClientPacketHandler(PacketType.AdminChangeAppearance)]
public class PacketAdminChangeAppearance : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsPlayerAlive)
            return;

        var p = connection.Player;

        if (p == null) return;
        Debug.Assert(connection.Player != null && connection.Character?.Map != null);

        var id = msg.ReadInt32();
        var val = msg.ReadInt32();
        var subId = msg.ReadInt32();

        switch (id)
        {
            default:
            case 0:
                p.SetData(PlayerStat.Head, GameRandom.NextInclusive(0, 31));
                p.SetData(PlayerStat.Gender, GameRandom.NextInclusive(0, 1));
                break;
            case 1:
                if (subId == -1)
                    subId = GameRandom.NextInclusive(0, 8);
                if(val >= 0 && val <= 41)
                    p.SetData(PlayerStat.Head, val);
                else
                    p.SetData(PlayerStat.Head, GameRandom.NextInclusive(0, 31));
                p.SetData(PlayerStat.HairId, subId);
                break;
            case 2:
                if(val >= 0 && val <= 1)
                    p.SetData(PlayerStat.Gender, val);
                else
                    p.SetData(PlayerStat.Gender, GameRandom.NextInclusive(0, 1));
                break;
            case 3:
                if (!p.IsAdmin)
                    return;
                if (val >= 0 && val <= 6)
                    p.ChangeJob(val);
                else
                    p.ChangeJob(GameRandom.Next(0, 6));
                return; //return as we don't want to double refresh (change job will refresh)
            //case 4:
            //    if (!p.IsAdmin)
            //        return;

            //    if (val >= 0 && val <= 12)
            //        p.SetWeaponClassOverride(val);
            //    else
            //        p.SetWeaponClassOverride(0);
            //    break;
        }

        connection.Character.Map.RefreshEntity(p.Character);
        p.UpdateStats();
    }
}