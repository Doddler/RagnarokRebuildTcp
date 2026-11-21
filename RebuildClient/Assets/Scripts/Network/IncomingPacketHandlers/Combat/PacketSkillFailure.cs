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
                    Camera.AppendChatText("Skill failed: You don't have the correct ammunition type equipped.", TextColor.Error);
                    break;
                case SkillValidationResult.IncorrectWeapon:
                    Camera.AppendChatText("Skill failed: Skill cannot be used with this weapon.", TextColor.Error);
                    break;
                case SkillValidationResult.InsufficientSp:
                    Camera.AppendChatText("Skill failed: Insufficient SP.", TextColor.Error);
                    break;
                case SkillValidationResult.InsufficientZeny:
                    Camera.AppendChatText("Skill failed: Insufficient Zeny.", TextColor.Error);
                    break;
                case SkillValidationResult.InsufficientItemCount:
                    Camera.AppendChatText("Skill failed: Missing a required item.", TextColor.Error);
                    break;
                case SkillValidationResult.CannotTargetBossMonster:
                    Camera.AppendChatText("Skill failed: The monster is immune to this skill effect.", TextColor.Error);
                    break;
                case SkillValidationResult.ItemAlreadyStolen:
                    Camera.AppendChatText("Skill failed: An item has already been stolen from this target.", TextColor.Error);
                    break;
                case SkillValidationResult.Failure:
                    Camera.AppendChatText("Skill failed.", TextColor.Error);
                    break;
                case SkillValidationResult.MemoLocationInvalid:
                    Camera.AppendChatText("This location is unavailable for use as a warp portal destination.", TextColor.Error);
                    break;
                case SkillValidationResult.MemoLocationUnwalkable:
                    Camera.AppendChatText("Cannot memo current location while standing on un-walkable ground.", TextColor.Error);
                    break;
                case SkillValidationResult.MustBeStandingInWater:
                    Camera.AppendChatText("Skill failed: You must have water nearby to use this skill.", TextColor.Error);
                    break;
                case SkillValidationResult.MissingRequiredItem:
                    Camera.AppendChatText("Skill failed: You are missing a required item or catalyst.", TextColor.Error);
                    break;
                case SkillValidationResult.SkillNotKnown:
                    Camera.AppendChatText("Skill failed: Skill not learned or available.", TextColor.Error);
                    break;
                case SkillValidationResult.TrapTooClose:
                    Camera.AppendChatText("Skill failed: Too close to another trap, player, or monster.", TextColor.Error);
                    break;
                case SkillValidationResult.TargetImmuneToEffect:
                    Camera.AppendChatText("Skill failed: The target's equipment blocks this skill.", TextColor.Error);
                    break;
                case SkillValidationResult.TargetAreaOccupied:
                    Camera.AppendChatText("Skill failed: Target area is currently occupied.", TextColor.Error);
                    break;
                case SkillValidationResult.CannotTargetSelf:
                    Camera.AppendChatText("Skill failed: You can't target yourself with this skill.", TextColor.Error);
                    break;
                case SkillValidationResult.FalconRequired:
                    Camera.AppendChatText("Skill failed: You need a falcon to use this skill.", TextColor.Error);
                    break;
                case SkillValidationResult.TargetStateIgnoresEffect:
                    Camera.AppendChatText("Skill failed: A status effect on the target prevents them from being targeted by this skill.", TextColor.Error);
                    break;
                case SkillValidationResult.UnusableWhileHidden:
                    Camera.AppendChatText("Skill failed: This skill cannot be used while hidden.", TextColor.Error);
                    break;
                case SkillValidationResult.MustBeUsedWhileHidden:
                    Camera.AppendChatText("Skill failed: This skill can only be used while using hiding.", TextColor.Error);
                    break;
                case SkillValidationResult.CannotTeleportHere:
                    Camera.AppendChatText("You're unable to teleport in this location.", TextColor.Error);
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