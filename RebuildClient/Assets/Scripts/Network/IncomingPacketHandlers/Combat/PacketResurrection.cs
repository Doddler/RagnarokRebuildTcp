using Assets.Scripts.Effects.EffectHandlers.Skills.Priest;
using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.Resurrection)]
    public class PacketResurrection : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var pos = msg.ReadPosition();
            var hp = msg.ReadInt32();

            if (!NetworkManager.Instance.EntityList.TryGetValue(id, out var controllable))
            {
                //Debug.LogWarning("Trying to do hit entity " + id1 + ", but it does not exist in scene!");
                return;
            }

            Camera.AttachEffectToEntity("Resurrect", controllable.gameObject, id);
            ReviveEffect.Create(controllable);

            controllable.IsCharacterAlive = true;
            controllable.SpriteAnimator.State = SpriteState.Idle;
            controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Idle, true);
            controllable.SetHp(hp);
            if (controllable.IsMainCharacter)
                Camera.UpdatePlayerHP(hp, State.MaxHp);
        }
    }
}