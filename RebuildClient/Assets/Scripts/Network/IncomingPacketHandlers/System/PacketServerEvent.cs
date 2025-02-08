using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.System
{
    [ClientPacketHandler(PacketType.ServerEvent)]
    public class PacketServerEvent : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var type = (ServerEvent)msg.ReadByte();
            var val = msg.ReadInt32();
            // var text = msg.ReadString();

            switch (type)
            {
                case ServerEvent.TradeSuccess:
                    Camera.AppendChatText($"<color=#00fbfb>The trade completed successfully.</color>");
                    break;
                case ServerEvent.GetZeny:
                    if (val > 0)
                        Camera.AppendChatText($"<color=#00fbfb>Obtained {val} zeny.</color>");
                    if (val < 0)
                        Camera.AppendChatText($"<color=#00fbfb>Lost {-val} zeny.</color>");
                    break;
                case ServerEvent.NoAmmoEquipped:
                    if (Camera.TargetControllable.WeaponClass == 12)
                        Camera.AppendChatText($"<color=#ed0000>You don't have any arrows equipped.</color>");
                    else
                        Camera.AppendChatText($"<color=#ed0000>You don't have any ammunition equipped.</color>");
                    break;
                case ServerEvent.WrongAmmoEquipped:
                    Camera.AppendChatText($"<color=#ed0000>You don't have the right kind of ammunition equipped.</color>");
                    break;
                case ServerEvent.OutOfAmmo:
                    Camera.AppendChatText($"<color=#ed0000>You don't have enough ammunition left to fire.</color>");
                    break;
                case ServerEvent.EligibleForJobChange:
                    Camera.AppendChatText($"<color=#99CCFF><i>Congratulations, you've reached job 10! You are now eligible to change jobs. "
                                          + "Speak to the bard south of Prontera to get started.</i></color>");
                    break;
                case ServerEvent.MemoLocationSaved:
                    if(State.KnownSkills.TryGetValue(CharacterSkill.WarpPortal, out var level) && level > 1)
                        Camera.AppendChatText($"<color=#00fbfb>Current location has been recorded in slot {val + 1} as a warp portal destination.</color>");
                    else
                        Camera.AppendChatText($"<color=#00fbfb>Current location has been recorded as your warp portal destination.</color>");
                    break;
            }
        }
    }
}