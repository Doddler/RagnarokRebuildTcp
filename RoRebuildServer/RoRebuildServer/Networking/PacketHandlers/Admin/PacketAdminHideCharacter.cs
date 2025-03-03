using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Networking.PacketHandlers.Admin
{
    [AdminClientPacketHandler(PacketType.AdminHideCharacter)]
    public class PacketAdminHideCharacter : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (!connection.IsOnlineAdmin)
                return;

            var desiredStatus = msg.ReadBoolean();

            connection.Character!.AdminHidden = desiredStatus;

            CommandBuilder.SendAdminHideStatus(connection.Player!, desiredStatus);

            using var notifyList = EntityListPool.Get();
            if (desiredStatus)
            {
                var visible = connection.Character.GetVisiblePlayerList();
                if (visible == null)
                    return;
                foreach (var e in visible)
                {
                    if (e == connection.Entity)
                        continue;
                    notifyList.Add(e);
                }
                CommandBuilder.AddRecipients(notifyList);
                CommandBuilder.SendRemoveEntityMulti(connection.Character, CharacterRemovalReason.OutOfSight);
                CommandBuilder.ClearRecipients();
            }
            else
            {
                var visible = connection.Character.GetVisiblePlayerList();
                if (visible == null)
                    return;
                foreach (var e in visible)
                {
                    if (e == connection.Entity)
                        continue;
                    notifyList.Add(e);
                }
                CommandBuilder.AddRecipients(notifyList);
                CommandBuilder.SendCreateEntityMulti(connection.Character, CreateEntityEventType.Normal);
                CommandBuilder.ClearRecipients();
            }
        }
    }
}
