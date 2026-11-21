using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.UpdateCharacterDisplayState)]
    public class PacketUpdateCharacterDisplayState : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            
            if (!Network.EntityList.TryGetValue(id, out var controllable))
                return;

            var headUpper = msg.ReadInt32();
            var headMid = msg.ReadInt32();
            var headLower = msg.ReadInt32();
            var weapon = msg.ReadInt32();
            var shield = msg.ReadInt32();
            controllable.SetWeaponClass(msg.ReadInt32());

            //a disguise is a single sprite without gear layers
            if (controllable.HeadAnimator != null)
                EntityBuilder.Instance.AttachGear(controllable, headUpper, headMid, headLower, weapon, shield);

            controllable.SetAppearance(controllable.Appearance.WithHeadgear(headUpper, headMid, headLower));

            Debug.Log($"Updating appearance data. New weapon class: {controllable.WeaponClass}");
        }
    }
}