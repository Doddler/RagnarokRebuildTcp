using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Objects;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Utility;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.Say)]
    public class PacketSay : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var text = msg.ReadString();
            var name = msg.ReadString();
            var type = (PlayerChatType)msg.ReadByte();
            // var isShout = msg.ReadBoolean();
            // var hasNotice = msg.ReadBoolean();
            
            if(type == PlayerChatType.Notice)
                AudioManager.Instance.PlaySystemSound("버튼소리.ogg");

            if (type == PlayerChatType.Shout && GameConfig.Data.HideShoutChat)
            {
                Debug.Log($"Suppressed shout chat message: {name}: {text}");
                return;
            }

            if (id == -1)
            {
                if(!string.IsNullOrWhiteSpace(name))
                    Camera.AppendChatText(Line(name, TextColors.System, text));
                else
                    Camera.AppendChatText(text);
                return;
            }

            if (type == PlayerChatType.Party)
            {
                if (Network.EntityList.TryGetValue(id, out var partyMember))
                    partyMember.DialogBox(Line(name, TextColors.Party, text));
                Camera.AppendChatText(Line(name, TextColors.Party, text));
                return;
            }

            if (Network.EntityList.TryGetValue(id, out var controllable))
            {
                if (type == PlayerChatType.Shout)
                {
                    controllable.DialogBox(Line(name, TextColors.Shout, text));
                    Camera.AppendChatText(Line($"{name} shouts", TextColors.Shout, text));
                }
                else
                {
                    controllable.DialogBox(Line(name, TextColors.Say, text));
                    Camera.AppendChatText(Line(name, TextColors.Say, text));
                }
            }
            else
            {
                if (type == PlayerChatType.Shout)
                    Camera.AppendChatText(Line($"{name} shouts", TextColors.Shout, text));
                else
                    Camera.AppendChatText(Line($"{name} nearby", TextColors.Say, text));
            }
        }

        private static string Line(string speaker, string color, string text) => $"<color={color}>{speaker}: {text}</color>";
    }
}