using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.UI.Inventory;
using Assets.Scripts.UI.Utility;
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
                    Camera.AppendChatText("<color=#FF7777>Skill failed: You must have water nearby to use this skill.</color>");
                    break;
                case SkillValidationResult.MissingRequiredItem:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: You are missing a required item or catalyst.</color>");
                    break;
                case SkillValidationResult.SkillNotKnown:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: Skill not learned or available.</color>");
                    break;
                case SkillValidationResult.TrapTooClose:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: Too close to another trap, player, or monster.</color>");
                    break;
                case SkillValidationResult.TargetImmuneToEffect:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: The target's equipment blocks this skill.</color>");
                    break;
                case SkillValidationResult.TargetAreaOccupied:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: Target area is currently occupied.</color>");
                    break;
                case SkillValidationResult.TargetStateIgnoresEffect:
                    Camera.AppendChatText("<color=#FF7777>Skill failed: A status effect on the target prevents them from being targeted by this skill.</color>");
                    break;
                case SkillValidationResult.CannotTeleportHere:
                    Camera.AppendChatText("<color=#FF7777>You're unable to teleport in this location.</color>");
                    break;
                case SkillValidationResult.VendFailedGenericError:
                    VendingSetupManager.Instance?.ResumeVendWindow();
                    Camera.AppendChatText("Vending failed.", TextColor.Error);
                    break;
                case SkillValidationResult.VendFailedInvalidPrice:
                    VendingSetupManager.Instance?.ResumeVendWindow();
                    Camera.AppendChatText("Vending failed: One or more of the prices provided were not valid (max 9,999,999z).", TextColor.Error);
                    break;
                case SkillValidationResult.VendFailedItemsNotPreset:
                    VendingSetupManager.Instance?.ResumeVendWindow();
                    Camera.AppendChatText("Vending failed: One or more of the items listed could not be found in your cart.", TextColor.Error);
                    break;
                case SkillValidationResult.VendFailedNameNotValid:
                    VendingSetupManager.Instance?.ResumeVendWindow();
                    Camera.AppendChatText("Vending failed: Store name was not valid.", TextColor.Error);
                    break;
                case SkillValidationResult.VendFailedTooCloseToNpc:
                    VendingSetupManager.Instance?.ResumeVendWindow();
                    Camera.AppendChatText("Vending failed: Your shop cannot be within 4 tiles of an NPC.", TextColor.Error);
                    break;
                case SkillValidationResult.VendFailedTooManyItems:
                    VendingSetupManager.Instance?.ResumeVendWindow();
                    Camera.AppendChatText("Vending failed: The number of items exceeds the amount allowed by your learned level of vending.", TextColor.Error);
                    break;
                default:
                    Debug.Log($"Skill failure (not shown to user): {result}");
                    break;
            }
        }
    }
}