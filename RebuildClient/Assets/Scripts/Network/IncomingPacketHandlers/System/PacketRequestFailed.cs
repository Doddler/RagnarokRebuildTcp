using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using Assets.Scripts.UI.Utility;

namespace Assets.Scripts.Network.IncomingPacketHandlers.System
{
    [ClientPacketHandler(PacketType.RequestFailed)]
    public class PacketRequestFailed : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var error = (ClientErrorType)msg.ReadByte();

            switch (error)
            {
                case ClientErrorType.InvalidCoordinates:
                    Camera.AppendError("Coordinates were invalid.");
                    break;
                case ClientErrorType.TooManyRequests:
                    Camera.AppendChatText($"<color={TextColors.System}>Warning</color>: Too many actions or requests.");
                    break;
                case ClientErrorType.UnknownMap:
                    Camera.AppendError("Could not find map.");
                    break;
                case ClientErrorType.MalformedRequest:
                    Camera.AppendError("Request could not be completed due to malformed data.");
                    break;
                case ClientErrorType.RequestTooLong:
                    Camera.AppendError("Your text was too long.");
                    break;
                case ClientErrorType.InvalidInput:
                    Camera.AppendError("Server request could not be performed as the input was not valid.");
                    break;
            }
        }
    }
}