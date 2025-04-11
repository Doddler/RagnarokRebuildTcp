using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using System.Diagnostics;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Networking.PacketHandlers.Party;

[ClientPacketHandler(PacketType.InvitePartyMember)]
public class PacketInvitePartyMember : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsConnectedAndInGame)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        //World.Instance.GetEntityById()

        var p = connection.Player;
        if (p.Party == null)
            return;

        if (!p.Party.IsPartyLeader(p))
        {
            CommandBuilder.ErrorMessage(p, "Only the party leader can invite other members.");
            return;
        }

        var isNameInvite = msg.ReadByte() == 1;
        if (isNameInvite)
        {
            var name = msg.ReadString();
            if (!World.Instance.TryFindPlayerByName(name, out var targetEntity) ||
                !targetEntity.TryGet<Player>(out var target))
            {
                CommandBuilder.ErrorMessage(p, $"Could not find player by the name of: {name}");
                return;
            }

            if (target.Party != null)
            {
                CommandBuilder.ErrorMessage(p, $"{name} is already in a party.");
                return;
            }

            if (target == p)
            {
                CommandBuilder.ErrorMessage(p, $"You cannot invite yourself.");
                return;
            }

            p.Party.SendInvite(p, target);
        }
        else
        {
            var targetId = msg.ReadInt32();

            if (targetId == p.Character.Id)
            {
                CommandBuilder.ErrorMessage(p, $"You cannot invite yourself.");
                return;
            }

            var targetEntity = World.Instance.GetEntityById(targetId);
            if (targetEntity.TryGet<Player>(out var target))
                p.Party.SendInvite(p, target);
        }
    }
}