using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.StorageInteraction)]
    public class PacketStorageInteraction : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var type = (ItemType)msg.ReadByte();
            var bagId = msg.ReadInt32();
            var change = (int)msg.ReadInt16();
            var item = InventoryItem.Deserialize(msg, type, bagId);
            State.CurrentWeight = msg.ReadInt32();
            var storageCount = msg.ReadInt32();
            var isMoveToStorage = msg.ReadBoolean();

            if (isMoveToStorage)
            {
                if(StorageUI.Instance != null)
                    StorageUI.Instance.UpdateStorageItemCount(item, change);
            }
            else
            {
                if(StorageUI.Instance != null)
                    StorageUI.Instance.UpdateStorageItemCount(item, -change);
            }
            
            UiManager.SkillHotbar.UpdateItemCounts();
            UiManager.InventoryWindow.UpdateActiveVisibleBag();
            UiManager.RefreshTooltip();
        }
    }
}