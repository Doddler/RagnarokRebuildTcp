using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.PlayerControl;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers
{
    [ClientPacketHandler(PacketType.ToggleActivatedState)]
    public class PacketToggleActivatedState : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var activated = msg.ReadBoolean();
            
            if (!Network.EntityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            if (controllable.EntityObject == null)
                return;
            
            var actionHandler = controllable.EntityObject.GetComponent<IEntityActionHandler>();
            if(actionHandler != null)
                actionHandler.ChangeCharacterState(activated ? CharacterState.Activated : CharacterState.Idle);
        }
    }
}