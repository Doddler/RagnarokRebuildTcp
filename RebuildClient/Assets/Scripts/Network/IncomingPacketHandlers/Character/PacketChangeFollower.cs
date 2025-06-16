using Assets.Scripts.Misc;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.ChangeFollower)]
    public class PacketChangeFollower : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            
            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;
            
            var follower = (PlayerFollower)msg.ReadByte();

            if (controllable.FollowerObject != null)
            {
                Object.Destroy(controllable.FollowerObject);
            }

            if (follower == PlayerFollower.None)
            {
                controllable.FollowerObject = null;
                PlayerState.Instance.HasCart = false;
                UiManager.Instance.EquipmentWindow.RefreshEquipmentWindow();
                return;
            }

            if ((follower & PlayerFollower.AnyCart) > 0)
            {
                var cartStyle = follower switch
                {
                    PlayerFollower.Cart0 => 0,
                    PlayerFollower.Cart1 => 1,
                    PlayerFollower.Cart2 => 2,
                    PlayerFollower.Cart3 => 3,
                    PlayerFollower.Cart4 => 4,
                    _ => 0
                };
                var cartObj = new GameObject();
                var cart = cartObj.AddComponent<CartFollower>();
                cart.AttachCart(controllable, cartStyle);
                controllable.FollowerObject = cartObj;
            }

            if (controllable.IsMainCharacter)
                PlayerState.Instance.HasCart = true;
            
            UiManager.Instance.EquipmentWindow.RefreshEquipmentWindow();
        }
    }
}