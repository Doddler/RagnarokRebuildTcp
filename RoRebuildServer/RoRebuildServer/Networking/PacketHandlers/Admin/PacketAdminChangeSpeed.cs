using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Networking.PacketHandlers.Admin
{
    [AdminClientPacketHandler(PacketType.AdminChangeSpeed)]
    public class PacketAdminChangeSpeed : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (!connection.IsOnlineAdmin)
                return;

            var speed = msg.ReadInt16();
            if (speed < 30)
                speed = 30;
            if (speed > 2000)
                speed = 2000;

            connection.Character.MoveSpeed = speed / 1000f;
            connection.Character.CombatEntity.SetTiming(TimingStat.MoveSpeed, speed / 1000f);
        }
    }
}
