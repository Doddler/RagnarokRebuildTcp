using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.UI;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.OpenStorage)]
    public class PacketOpenStorage : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            Camera.DialogPanel.GetComponent<DialogWindow>().HideUI();
            State.Storage.Deserialize(msg);
            var storage = StorageUI.InitializeStorageUI(UiManager.StorageWindowPrefab, UiManager.PrimaryUserWindowContainer);
            storage.SetupStorageItems(State.Storage);
        }
    }
}