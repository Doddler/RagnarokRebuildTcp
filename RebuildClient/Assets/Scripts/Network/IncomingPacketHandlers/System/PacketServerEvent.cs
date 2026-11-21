using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using Assets.Scripts.UI.Utility;

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
                    Camera.AppendChatText($"The trade completed successfully.", TextColor.Item);
                    break;
                case ServerEvent.GetZeny:
                    if (val > 0)
                        Camera.AppendChatText($"Obtained {val} zeny.", TextColor.Item);
                    if (val < 0)
                        Camera.AppendChatText($"Lost {-val} zeny.", TextColor.Item);
                    break;
                case ServerEvent.NoAmmoEquipped:
                    if (State.WeaponClass == 12)
                        Camera.AppendChatText($"You don't have any arrows equipped.", TextColor.Error);
                    else
                        Camera.AppendChatText($"You don't have any ammunition equipped.", TextColor.Error);
                    break;
                case ServerEvent.WrongAmmoEquipped:
                    Camera.AppendChatText($"You don't have the right kind of ammunition equipped.", TextColor.Error);
                    break;
                case ServerEvent.OutOfAmmo:
                    Camera.AppendChatText($"You don't have enough ammunition left to fire.", TextColor.Error);
                    break;
                case ServerEvent.EligibleForJobChange:
                    Camera.AppendChatText($"<i>Congratulations, you've reached job 10! You are now eligible to change jobs. "
                                          + "Speak to the bard south of Prontera to get started.</i>", TextColor.Job);
                    break;
                case ServerEvent.MemoLocationSaved:
                    if(State.KnownSkills.TryGetValue(CharacterSkill.WarpPortal, out var level) && level > 1)
                        Camera.AppendChatText($"Current location has been recorded in slot {val + 1} as a warp portal destination.", TextColor.Item);
                    else
                        Camera.AppendChatText($"Current location has been recorded as your warp portal destination.", TextColor.Item);
                    break;
                // case ServerEvent.PartyInviteSent:
                //     Camera.AppendChatText($"<color=#77FF77>A party invite has been sent.</color>");
                //     break;
            }
        }
    }
}