using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Objects;
using Assets.Scripts.UI.ConfigWindow;
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
                    Camera.AppendChatText($"{name}: {text}");
                else
                    Camera.AppendChatText(text);
                return;
            }

            if (type == PlayerChatType.Party)
            {
                if (Network.EntityList.TryGetValue(id, out var partyMember))
                    partyMember.DialogBox($"{name}: <i><color=#2FCE2C>{text}</color></i>");
                Camera.AppendChatText($"{name} to party: <i><color=#2FCE2C>{text}</color></i>");
                return;
            }

            if (Network.EntityList.TryGetValue(id, out var controllable))
            {
                if (type == PlayerChatType.Shout)
                {
                    controllable.DialogBox($"{name}: <i><color=#FFB051>{text}</color></i>");
                    Camera.AppendChatText($"{name} shouts: <i><color=#FFB051>{text}</color></i>");
                }
                else
                {
                    controllable.DialogBox($"{name}: {text}");
                    Camera.AppendChatText($"{name}: {text}");
                }
            }
            else
            {
                if (type == PlayerChatType.Shout)
                    Camera.AppendChatText($"{name} shouts: <i><color=#FFB051>{text}</color></i>");
                else
                    Camera.AppendChatText($"{name} nearby: <i><color=#FFFF6A>{text}</color></i>");
            }
        }
    }
}