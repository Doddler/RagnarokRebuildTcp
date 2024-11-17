using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Network
{
    [ClientPacketHandler(PacketType.RemoveEntity)]
    public class PacketRemoveEntity : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var reason = (CharacterRemovalReason)msg.ReadByte();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to remove entity " + id + ", but it does not exist in scene!");
                return;
            }

            if (id == State.EntityId)
            {
                //Debug.Log("We're removing the player object! Hopefully the server knows what it's doing. We're just going to pretend we didn't see it.");
                //return;

                Debug.LogWarning("Whoa! Trying to delete player object. Is that right...?");
                Camera.Target = null;
                Network.ClearGroundItemList();
            }

            Network.EntityList.Remove(id);
            
            if (Camera.SelectedTarget == controllable)
                Camera.ClearSelected();

            if (reason == CharacterRemovalReason.Dead)
            {
                if (controllable.SpriteAnimator.Type != SpriteType.Player)
                    controllable.MonsterDie();
                else
                    controllable.FadeOutAndVanish(0.1f);
            }
            else
            {
                controllable.FadeOutAndVanish(0.1f);
            }
        }
    }
}