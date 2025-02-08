using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.UI;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.ChangePlayerSpecialActionState)]
    public class PacketChangePlayerSpecialActionState : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var specialState = (SpecialPlayerActionState)msg.ReadByte();

            switch (specialState)
            {
                case SpecialPlayerActionState.None:
                    if(WarpPortalWindow.Instance != null)
                        WarpPortalWindow.Instance.HideWindow();
                    break;
                case SpecialPlayerActionState.WaitingOnPortalDestination:
                    WarpPortalWindow.StartCastWarpPortal();
                    break;
            }
        }
    }
}