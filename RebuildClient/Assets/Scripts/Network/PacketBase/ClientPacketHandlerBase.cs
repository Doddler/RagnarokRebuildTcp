using Assets.Scripts.PlayerControl;

namespace Assets.Scripts.Network.HandlerBase
{
    public abstract class ClientPacketHandlerBase
    {
        public NetworkManager Network;
        public CameraFollower Camera;
        public UiManager UiManager;
        public PlayerState State;
        
        public virtual void ReceivePacket(ClientInboundMessage msg) {}
    }
}