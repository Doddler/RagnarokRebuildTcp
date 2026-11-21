using Assets.Scripts.Effects;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Utility;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.GainExp)]
    public class PacketGainExp : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var total = msg.ReadInt32();
            var exp = msg.ReadInt32();
            var jobTotal = msg.ReadInt32();
            var job = msg.ReadInt32();

            // Show exp-gain in chat window if the given setting was activated
            if (GameConfig.Data.ShowExpGainInChat == true && (exp > 0 || job > 0)) {
                Camera.AppendChatText($"You gained {exp} base and {job} job exp.", TextColor.System);
            }

            State.SetExp(total, jobTotal);

            var controllable = State.Character;
            if (controllable == null)
                return;

            if (exp == 0)
                return;

            //pooled indicators update every frame, so one is only taken when shown
            if (!GameConfig.Data.ShowExpGainOnKill)
                return;

            var height = 72f / 50f;
            if (controllable.SpriteAnimator != null && controllable.SpriteMode != ClientSpriteType.Prefab)
                height = controllable.SpriteAnimator.SpriteData.Size / 50f;

            var di = RagnarokEffectPool.GetDamageIndicator();
            di.DoDamage(TextIndicatorType.Experience, $"+{exp} Exp", controllable.gameObject.transform.localPosition,
                height, Direction.None, "yellow", false);
        }
    }
}
