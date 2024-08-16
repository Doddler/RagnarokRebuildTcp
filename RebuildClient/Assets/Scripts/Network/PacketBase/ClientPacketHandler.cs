using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Network.IncomingPacketHandlers;
using Assets.Scripts.Network.IncomingPacketHandlers.System;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.PacketBase
{
    public static partial class ClientPacketHandler
    {
        private static readonly ClientPacketHandlerBase[] handlers;

        public static bool HasValidHandler(PacketType type) => handlers[(int)type].GetType() != typeof(InvalidPacket);

        public static void Execute(PacketType type, ClientInboundMessage msg) => handlers[(int)type].ReceivePacket(msg);
        
        public static void Init(NetworkManager network, PlayerState state)
        {
            for (var i = 0; i < handlers.Length; i++)
            {
                handlers[i].Network = network;
                handlers[i].Camera = CameraFollower.Instance;
                handlers[i].UiManager = UiManager.Instance;
                handlers[i].State = state;
            }
        }
    }
}