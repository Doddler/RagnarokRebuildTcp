using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.SkillHandlers;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.StartAreaCast)]
    public class PacketStartAreaCast : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var srcId = msg.ReadInt32();
            var target = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var skill = (CharacterSkill)msg.ReadByte();
            var lvl = (int)msg.ReadByte();
            var size = (int)msg.ReadByte();
            var dir = (Direction)msg.ReadByte();
            var casterPos = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var castTime = msg.ReadFloat();
            var flags = (SkillCastFlags)msg.ReadByte();

            if (!Network.EntityList.TryGetValue(srcId, out var controllable))
                return;

            if (controllable.SpriteAnimator.State == SpriteState.Walking)
                controllable.StopImmediate(casterPos, false);
            if (target == controllable.CellPosition)
                controllable.LookInDirection(dir);
            else
                controllable.LookAt(target.ToWorldPosition());
            if (controllable.SpriteAnimator.State != SpriteState.Dead && controllable.SpriteAnimator.State != SpriteState.Walking)
            {
                controllable.SpriteAnimator.State = SpriteState.Standby;
                controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Casting);
                controllable.SpriteAnimator.PauseAnimation();
            }

            controllable.HideCastName = flags.HasFlag(SkillCastFlags.HideSkillName);
            ClientSkillHandler.StartCastingSkill(controllable, target, skill, lvl, castTime);
            controllable.StartCastBar(skill, castTime, flags);
        }
    }
}
