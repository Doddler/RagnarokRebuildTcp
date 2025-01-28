using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.SkillHandlers;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.StartCast)]
    public class PacketStartCasting : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var srcId = msg.ReadInt32();
            var targetId = msg.ReadInt32();
            var skill = (CharacterSkill)msg.ReadByte();
            var lvl = (int)msg.ReadByte();
            var dir = (Direction)msg.ReadByte();
            var casterPos = new Vector2Int(msg.ReadInt16(), msg.ReadInt16());
            var castTime = msg.ReadFloat();
            var hideName = msg.ReadBoolean();
            
            Network.EntityList.TryGetValue(targetId, out var target);

            if (Network.EntityList.TryGetValue(srcId, out var controllable))
            {
                if (controllable.SpriteAnimator.State == SpriteState.Walking)
                    controllable.StopImmediate(casterPos, false);
                
                controllable.LookAtOrDefault(target, dir);
                
                if (controllable.SpriteAnimator.State != SpriteState.Dead && controllable.SpriteAnimator.State != SpriteState.Walking)
                {
                    controllable.SpriteAnimator.State = SpriteState.Standby;
                    controllable.SpriteAnimator.ChangeMotion(SpriteMotion.Casting);
                    controllable.SpriteAnimator.PauseAnimation();
                }
                
                #if UNITY_EDITOR
                Debug.Log($"Character {controllable.Name} starts casting {skill} lv{lvl}, cast time {castTime}, hideName:{hideName}");
                #endif

                controllable.HideCastName = hideName;
                if (hideName)
                    skill = CharacterSkill.NoCast;
                ClientSkillHandler.StartCastingSkill(controllable, target, skill, lvl, castTime);
                controllable.StartCastBar(skill, castTime);
            }
        }
    }
}



