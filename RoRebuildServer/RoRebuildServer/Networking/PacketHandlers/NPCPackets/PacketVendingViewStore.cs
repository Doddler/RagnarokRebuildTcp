using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation;
using System.Numerics;

namespace RoRebuildServer.Networking.PacketHandlers.NPCPackets;

[ClientPacketHandler(PacketType.VendingViewStore)]
public class PacketVendingViewStore : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var id = msg.ReadInt32();

        var character = connection.Character;

        if (character == null || connection.Player == null
                              || character.State == CharacterState.Sitting
                              || character.State == CharacterState.Dead
                              || character.Player.IsInNpcInteraction)
            return;

        if (!character.Player.CanPerformCharacterActions() && !character.CombatEntity.HasBodyState(BodyStateFlags.Hidden))
            return;

        connection.Player.AddInputActionDelay(InputActionCooldownType.Click);

        var targetNpc = World.Instance.GetEntityById(id);

        if (targetNpc.Type != EntityType.Npc)
            return;

        if (targetNpc.IsNull() || !targetNpc.IsAlive())
            return;

        var player = connection.Player;
        var npc = targetNpc.Get<Npc>();

        if (!npc.Owner.TryGet<Player>(out var vendor) || vendor.VendingState == null)
            return;

        if (player.Character.Map != npc.Character.Map || !npc.Character.IsPlayerVisible(player.Entity))
            return;

        npc.OnInteract(connection.Player);
        //player.NpcInteractionState.OpenShop();
        
        CommandBuilder.SendVendOpenShop(player, vendor, npc.Name);
        
    }
}