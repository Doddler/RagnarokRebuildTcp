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

        var targetNpc = World.Instance.GetEntityById(id);
        
        character.StopAction();
        character.Player.ClearTarget();

        if (targetNpc.Type != EntityType.Npc)
            return;

        if (targetNpc.IsNull() || !targetNpc.IsAlive())
            return;
        
        var npc = targetNpc.Get<Npc>();
        var npcChar = targetNpc.Get<WorldObject>();

        if (!npc.HasInteract)
            return;

        npc.OnInteract(character.Player);
        
        character.ChangeLookDirection(ref connection.Entity, (npcChar.Position - character.Position).Normalize().GetDirectionForOffset(), HeadFacing.Center);
    }
}