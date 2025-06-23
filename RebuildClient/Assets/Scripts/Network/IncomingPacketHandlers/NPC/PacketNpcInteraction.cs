using System.Collections.Generic;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.UI;
using Assets.Scripts.UI.RefineItem;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers
{
    [ClientPacketHandler(PacketType.NpcInteraction)]
    public class PacketNpcInteraction : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var type = (NpcInteractionType)msg.ReadByte();

            //Debug.Log($"Received NPC interaction of type: {type}");

            switch (type)
            {
                case NpcInteractionType.NpcDialog:
                {
                    var name = msg.ReadString();
                    var text = msg.ReadString();
                     var isBig = msg.ReadBoolean();

                    Camera.IsInNPCInteraction = true;
                    var window = Camera.DialogPanel.GetComponent<DialogWindow>();
                    if(isBig)
                         window.MakeBig();
                    else
                     window.MakeNormalSize();
                    
                    window.SetDialog(name, text);
                    break;
                }
                case NpcInteractionType.NpcFocusNpc:
                {
                    var id = msg.ReadInt32();
                    if (!Network.EntityList.TryGetValue(id, out var controllable))
                        return;
                    var isFocus = msg.ReadBoolean();

                    Camera.OverrideTarget = isFocus ? controllable.gameObject : null;
                    break;
                }
                case NpcInteractionType.NpcShowSprite:
                {
                    var sprite = msg.ReadString();
                    Debug.Log($"Show npc sprite {sprite}");
                    Camera.DialogPanel.GetComponent<DialogWindow>().ShowImage(sprite);
                    break;
                }
                case NpcInteractionType.NpcOption:
                {
                    var options = new List<string>();
                    var len = msg.ReadInt32();
                    for (var i = 0; i < len; i++)
                        options.Add(msg.ReadString());

                    Camera.NpcOptionPanel.GetComponent<NpcOptionWindow>().ShowOptionWindow(options);
                    break;
                }
                case NpcInteractionType.NpcEndInteraction:
                    Camera.OverrideTarget = null;
                    Camera.IsInNPCInteraction = false;
                    Camera.DialogPanel.GetComponent<DialogWindow>().HideUI();
                    if(RefineItemWindow.Instance != null)
                        GameObject.Destroy(RefineItemWindow.Instance);
                    if(StorageUI.Instance != null)
                        GameObject.Destroy(StorageUI.Instance);
                    break;
                case NpcInteractionType.NpcOpenRefineWindow:
                    Camera.DialogPanel.GetComponent<DialogWindow>().HideUI();
                    RefineItemWindow.OpenRefineItemWindow();
                    break;
                default:
                    Debug.LogError($"Unknown Npc Interaction type: {type}");
                    break;
            }
        }
    }
}