using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation;
using System.Numerics;
using RoRebuildServer.EntityComponents.Character;

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

        if (!character.Player.CanPerformCharacterActions() && !character.CombatEntity.HasBodyState(BodyStateFlags.Hidden))
            return;

        connection.Player.AddActionDelay(CooldownActionType.Click);

        var targetNpc = World.Instance.GetEntityById(id);
        
        if (targetNpc.Type != EntityType.Npc)
            return;

        if (targetNpc.IsNull() || !targetNpc.IsAlive())
            return;
        
        var npc = targetNpc.Get<Npc>();

        if (!npc.HasInteract)
            return;

        character.ShortenMovePath(); //we might be walking for 1 more tile after we start our interaction 
        character.Player.ClearTarget();

        npc.OnInteract(character.Player);
        
        if(!character.IsMoving)
            character.LookAtEntity(ref targetNpc);
            //character.ChangeLookDirection(ref connection.Entity, (npcChar.Position - character.Position).Normalize().GetDirectionForOffset(), HeadFacing.Center);
    }
}