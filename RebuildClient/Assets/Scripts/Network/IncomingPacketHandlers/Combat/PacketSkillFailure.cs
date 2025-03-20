using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Combat
{
    [ClientPacketHandler(PacketType.SkillError)]
    public class PacketSkillFailure : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var result = (SkillValidationResult)msg.ReadByte();

            switch (result)
            {
                case SkillValidationResult.IncorrectAmmunition:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: You don't have the correct ammunition type equipped.</color>");
                    break;
                case SkillValidationResult.IncorrectWeapon:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: Skill cannot be used with this weapon.</color>");
                    break;
                case SkillValidationResult.InsufficientSp:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: Insufficient SP.</color>");
                    break;
                case SkillValidationResult.InsufficientZeny:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: Insufficient Zeny.</color>");
                    break;
                case SkillValidationResult.InsufficientItemCount:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: Missing a required item.</color>");
                    break;
                case SkillValidationResult.CannotTargetBossMonster:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: The monster is immune to this skill effect.</color>");
                    break;
                case SkillValidationResult.ItemAlreadyStolen:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: An item has already been stolen from this target.</color>");
                    break;
                case SkillValidationResult.Failure:
                    Camera.AppendChatText("<color=#FF7777>Skill failed.</color>");
                    break;
                case SkillValidationResult.MemoLocationInvalid:
                    Camera.AppendChatText("<color=#FF7777>This location is unavailable for use as a warp portal destination.</color>");
                    break;
                case SkillValidationResult.MemoLocationUnwalkable:
                    Camera.AppendChatText("<color=#FF7777>Cannot memo current location while standing on un-walkable ground.</color>");
                    break;
                case SkillValidationResult.MustBeStandingInWater:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: You need to be standing in water to use this skill.</color>");
                    break;
                case SkillValidationResult.MissingRequiredItem:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: You are missing a required item or catalyst.</color>");
                    break;
                case SkillValidationResult.SkillNotKnown:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: Skill not learned or available.</color>");
                    break;
                default:
                    Debug.Log($"Skill failure (not shown to user): {result}");
                    break;
            }
        }
    }
}