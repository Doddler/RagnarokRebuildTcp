using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation;
using System.Numerics;

namespace RoRebuildServer.Networking.PacketHandlers.NPC;

[ClientPacketHandler(PacketType.NpcClick)]
public class PacketNpcClick : IClientPacketHandler
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


        if (connection.Player.InActionCooldown())
            return;
        connection.Player.AddActionDelay(CooldownActionType.Click);

        var target = World.Instance.GetEntityById(id);

        if (target.IsNull() || !target.IsAlive())
            return;

        if (target.Type != EntityType.Npc)
            return;

        var npc = target.Get<Npc>();
        var npcChar = target.Get<WorldObject>();
        if (!npc.HasInteract)
            return;


        npc.OnInteract(character.Player);
        
        character.ChangeLookDirection(ref connection.Entity, (npcChar.Position - character.Position).Normalize().GetDirectionForOffset(), HeadFacing.Center);
    }
}