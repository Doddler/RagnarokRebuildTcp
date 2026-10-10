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

            var follower = (CharacterFollowerState)msg.ReadByte();

            if (controllable.IsMainCharacter)
            {
                PlayerState.Instance.HasCart = (follower & CharacterFollowerState.AnyCart) > 0;
                PlayerState.Instance.HasBird = (follower & CharacterFollowerState.Falcon) > 0;
                UiManager.Instance.RefreshCartVisibility();
            }

            if (controllable.FollowerObject != null)
            {
                Object.Destroy(controllable.FollowerObject);
            }

            if (follower == CharacterFollowerState.None)
            {
                controllable.FollowerObject = null;
                UiManager.Instance.EquipmentWindow.RefreshEquipmentWindow();
                return;
            }

            if ((follower & CharacterFollowerState.AnyCart) > 0)
            {
                var cartStyle = follower switch
                {
                    CharacterFollowerState.Cart0 => 0,
                    CharacterFollowerState.Cart1 => 1,
                    CharacterFollowerState.Cart2 => 2,
                    CharacterFollowerState.Cart3 => 3,
                    CharacterFollowerState.Cart4 => 4,
                    _ => 0
                };
                var cartObj = new GameObject();
                var cart = cartObj.AddComponent<CartFollower>();
                cart.AttachCart(controllable, cartStyle);
                
                if(controllable.FollowerObject != null)
                    GameObject.Destroy(controllable.FollowerObject);
                controllable.FollowerObject = cartObj;

            }

            if ((follower & CharacterFollowerState.Falcon) > 0)
            {
                var birdObj = new GameObject();
                var bird = birdObj.AddComponent<BirdFollower>();
                bird.AttachBird(controllable, 0);
                
                if(controllable.FollowerObject != null)
                    GameObject.Destroy(controllable.FollowerObject);
                controllable.FollowerObject = birdObj;

            }

            UiManager.Instance.EquipmentWindow.RefreshEquipmentWindow();
        }
    }
}
