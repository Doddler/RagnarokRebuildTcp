using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Environment
{
    [ClientPacketHandler(PacketType.PickUpItem)]
    public class PacketPickUpItem : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var pickupId = msg.ReadInt32();
            var itemId = msg.ReadInt32();
            var hasPickerUpper = Network.EntityList.TryGetValue(pickupId, out var character);
            
            if (Network.GroundItemList.TryGetValue(itemId, out var item))
            {
                Debug.Log($"Pick up item {itemId}");
                if (hasPickerUpper)
                    character.LookAt(item.transform.position);

                GameObject.Destroy(item.gameObject);
                Network.GroundItemList.Remove(itemId);
            }

            if (hasPickerUpper && character.CharacterType == CharacterType.Player && character.SpriteAnimator != null)
            {
                character.StopImmediate(Vector2Int.zero, false);
                character.SpriteAnimator.ChangeMotion(SpriteMotion.PickUp);
            }
        }
    }
}