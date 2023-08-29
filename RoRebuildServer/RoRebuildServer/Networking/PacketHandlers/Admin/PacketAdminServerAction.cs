using RebuildSharedData.Networking;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Networking.PacketHandlers.Admin;

[AdminClientPacketHandler(PacketType.AdminServerAction)]
public class PacketAdminServerAction : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
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