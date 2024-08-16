using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Utility;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Network
{
    [ClientPacketHandler(PacketType.ChangeMaps)]
    public class PacketOnChangeMaps : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var mapName = msg.ReadString();
            
            Network.EntityList.Clear();
            Network.CurrentMap = mapName;
            
            SceneTransitioner.Instance.DoTransitionToScene(Network.CurrentScene, Network.CurrentMap, Network.OnMapLoad);
        }
    }
}