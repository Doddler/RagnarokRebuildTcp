using RebuildSharedData.Networking;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Networking.PacketHandlers.Admin;

[ClientPacketHandler(PacketType.AdminServerAction, true)]
public class PacketAdminServerAction : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (connection.Player?.IsAdmin != true)
        {
            NetworkManager.DisconnectPlayer(connection);
            return;
        }

        var type = (AdminAction)msg.ReadByte();

        if (type == AdminAction.ForceGC)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        if(type == AdminAction.ReloadScripts)
            World.Instance.TriggerReloadServerScripts();
    }
}