using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.Utility;
using Assets.Scripts.Utility;
using JetBrains.Annotations;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayerControl
{
    public static class ClientCommandHandler
    {
        private static Dictionary<string, int> emoteList = new();

        public static void RegisterEmoteCommand(string command, int id)
        {
            emoteList.TryAdd(command, id);
        }


        [CanBeNull]
        private static string[] SplitStringCommand(string input)
        {
            var outList = new List<string>();
            var inQuote = false;
            var sb = new StringBuilder();

            foreach (var c in input)
            {
                if ((c == ' ' && !inQuote) || (c == ',' && !inQuote))
                {
                    if (sb.Length == 0)
                        continue;
                    outList.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                if (c == '"')
                {
                    inQuote = !inQuote;
                    continue;
                }

                sb.Append(c);
            }

            if (inQuote)
                return null;

            outList.Add(sb.ToString());
            return outList.ToArray();
        }

        public static void HandleClientCommand(CameraFollower cameraFollower, ServerControllable controllable, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (text.StartsWith("/"))
            {
                if (text.StartsWith("/sh "))
                {
                    var msg = text.Substring(4);
                    NetworkManager.Instance.SendSay(msg, PlayerChatType.Shout);
                    return;
                }

                if (text.StartsWith("/s "))
                {
                    var msg = text.Substring(3);
                    NetworkManager.Instance.SendSay(msg, PlayerChatType.Shout);
                    return;
                }

                if (text.StartsWith("/shout "))
                {
                    var msg = text.Substring(7);
                    NetworkManager.Instance.SendSay(msg, PlayerChatType.Shout);
                    return;
                }
                
                if (text.StartsWith("/p "))
                {
                    var msg = text.Substring(3);
                    NetworkManager.Instance.SendSay(msg, PlayerChatType.Party);
                    return;
                }

                if (text.StartsWith("/party "))
                {
                    var msg = text.Substring(7);
                    NetworkManager.Instance.SendSay(msg, PlayerChatType.Party);
                    return;
                }

                var s = SplitStringCommand(text);
                if (s == null)
                {
                    cameraFollower.AppendError($"Malformed slash command, could not execute.");
                    return;
                }

                Debug.Log($"string command: " + string.Join('|', s));

                if (s[0] == "/memo")
                {
                    WarpPortalWindow.RunMemoCommand();
                    return;
                }

                if (s[0] == "/warp" && s.Length > 1)
                {
                    if (s.Length == 4)
                    {
                        if (int.TryParse(s[2], out var x) && int.TryParse(s[3], out var y))
                            NetworkManager.Instance.SendMoveRequest(s[1], x, y);
                        else
                            NetworkManager.Instance.SendMoveRequest(s[1]);
                    }
                    else
                        NetworkManager.Instance.SendMoveRequest(s[1]);
                }

                if (s[0] == "/where")
                {
                    var mapname = NetworkManager.Instance.CurrentMap;
                    var srcPos = cameraFollower.WalkProvider.GetMapPositionForWorldPosition(cameraFollower.Target.transform.position, out var srcPosition);

                    cameraFollower.AppendChatText($"Client location: {mapname} {srcPosition.x},{srcPosition.y}");
                    NetworkManager.Instance.SendClientTextCommand(ClientTextCommand.Where);
                }

                if (s[0] == "/info")
                {
                    NetworkManager.Instance.SendClientTextCommand(ClientTextCommand.Info);
                }

                if (s[0] == "/adminify")
                {
                    if (s.Length > 1)
                    {
                        var cmdText = text.Substring(10);
                        NetworkManager.Instance.SendClientTextCommand(ClientTextCommand.Adminify, cmdText);
                    }
                    else
                        NetworkManager.Instance.SendClientTextCommand(ClientTextCommand.Adminify);
                }

                if (s[0] == "/name" || s[0] == "/changename")
                {
                    var newName = text.Substring(s[0].Length + 1);
                    NetworkManager.Instance.SendChangeName(newName);
                }

                if (s[0] == "/find")
                {
                    var target = text.Substring(s[0].Length + 1);
                    NetworkManager.Instance.SendAdminFind(target);
                }

                if (s[0] == "/level")
                {
                    if (s.Length == 1 || !int.TryParse(s[1], out var level))
                        NetworkManager.Instance.SendAdminLevelUpRequest(0, false);
                    else
                        NetworkManager.Instance.SendAdminLevelUpRequest(level, false);
                }

                if (s[0] == "/joblevel")
                {
                    if (s.Length == 1 || !int.TryParse(s[1], out var level))
                        NetworkManager.Instance.SendAdminLevelUpRequest(0, true);
                    else
                        NetworkManager.Instance.SendAdminLevelUpRequest(level, true);
                }

                if (s[0] == "/skillreset")
                {
                    NetworkManager.Instance.SendAdminResetSkillPoints();
                }

                if (s[0] == "/statreset")
                {
                    NetworkManager.Instance.SendAdminResetStatPoints();
                }

                if (s[0] == "/hide")
                {
                    NetworkManager.Instance.SendAdminHideCharacter(!controllable.IsHidden);
                }

                if (s[0] == "/return")
                {
                    NetworkManager.Instance.SendRespawn(false);
                }

                if (s[0] == "/logout")
                {
                    GameConfig.SaveConfig();
                    NetworkManager.Instance.Disconnect();
                    SceneManager.LoadScene(0);
                }

                if (s[0] == "/item")
                {
                    if (s.Length < 2)
                    {
                        cameraFollower.AppendError("Invalid item request.");
                    }

                    var count = 1;
                    var name = s[1];
                    var nameMax = s.Length;

                    if (s.Length >= 3 && int.TryParse(s[s.Length - 1], out var newCount))
                    {
                        count = newCount;
                        nameMax--;
                    }

                    Debug.Log($"Item '{name}' {count} {nameMax}");

                    if (s.Length > 2)
                        name = String.Join(" ", s.Skip(1).Take(nameMax - 1));

                    if (!ClientDataLoader.Instance.TryGetItemByName(name, out var item))
                        cameraFollower.AppendError($"The item name '{name}' is not valid.");
                    else
                        NetworkManager.Instance.SendAdminCreateItem(item.Id, count);
                }

                if (s[0] == "/refine")
                {
                    if (s.Length < 2)
                    {
                        cameraFollower.AppendError("Invalid command format.");
                        return;
                    }

                    var itemPosition = s[1].ToLower() switch
                    {
                        "head" => EquipSlot.HeadTop,
                        "head1" => EquipSlot.HeadTop,
                        "head2" => EquipSlot.HeadMid,
                        "head3" => EquipSlot.HeadBottom,
                        "armor" => EquipSlot.Body,
                        "body" => EquipSlot.Body,
                        "weapon" => EquipSlot.Weapon,
                        "shield" => EquipSlot.Shield,
                        "garment" => EquipSlot.Garment,
                        "footgear" => EquipSlot.Footgear,
                        "boot" => EquipSlot.Footgear,
                        "boots" => EquipSlot.Footgear,
                        "shoes" => EquipSlot.Footgear,
                        _ => EquipSlot.None
                    };

                    if (itemPosition == EquipSlot.None)
                    {
                        cameraFollower.AppendError("Invalid item position.");
                        return;
                    }

                    var itemId = PlayerState.Instance.EquippedItems[(int)itemPosition];
                    if (itemId <= 0)
                    {
                        cameraFollower.AppendError($"Item not equipped in slot {itemPosition}.");
                        return;
                    }

                    var refine = 0;
                    if (s.Length >= 3)
                    {
                        if (!int.TryParse(s[2], out refine))
                        {
                            cameraFollower.AppendError($"Invalid refine value {s[2]}.");
                            return;
                        }
                    }
                    else
                    {
                        var item = PlayerState.Instance.Inventory.GetInventoryItem(itemId);
                        refine = item.UniqueItem.Refine + 1;
                    }

                    NetworkManager.Instance.SendAdminRefineAttempt(itemId, refine);
                }

                if (s[0] == "/summon" || s[0] == "/boss" || s[0] == "/monster")
                {
                    if (s.Length < 2)
                    {
                        cameraFollower.AppendError("Invalid summon monster request.");
                        return;
                    }

                    //var failed = false;
                    var count = 1;
                    var name = s[1];
                    var nameMax = s.Length;

                    if (s.Length >= 3 && int.TryParse(s[s.Length - 1], out var newCount))
                    {
                        count = newCount;
                        nameMax--;
                    }

                    Debug.Log($"Summon '{name}' {count} {nameMax}");

                    var isBoss = s[0] == "/boss";

                    if (s.Length > 2)
                        name = String.Join(" ", s.Skip(1).Take(nameMax - 1));

                    if (!ClientDataLoader.Instance.IsValidMonsterName(name) && !ClientDataLoader.Instance.IsValidMonsterCode(name))
                        cameraFollower.AppendError($"The monster name '{name}' is not valid.");
                    else
                        NetworkManager.Instance.SendAdminSummonMonster(name, count, isBoss);
                }

                if (s[0] == "/bgm")
                    AudioManager.Instance.ToggleMute();

                if (s[0] == "/emote" && s.Length == 2)
                    cameraFollower.Emote(int.Parse(s[1]));

                if (s[0] == "/change")
                {
                    if (s.Length == 1)
                        NetworkManager.Instance.SendChangeAppearance(0);

                    if (s.Length == 2)
                    {
                        if (s[1].ToLower() == "hair")
                            NetworkManager.Instance.SendChangeAppearance(1);
                        if (s[1].ToLower() == "gender")
                            NetworkManager.Instance.SendChangeAppearance(2, controllable.IsMale ? 1 : 0);
                        if (s[1].ToLower() == "job" || s[1].ToLower() == "class")
                            NetworkManager.Instance.SendChangeAppearance(3);
                        if (s[1].ToLower() == "weapon")
                            NetworkManager.Instance.SendChangeAppearance(4);
                    }

                    if (s.Length == 3)
                    {
                        if (int.TryParse(s[2], out var id))
                        {
                            if (s[1].ToLower() == "hair")
                                NetworkManager.Instance.SendChangeAppearance(1, id);
                            if (s[1].ToLower() == "gender")
                                NetworkManager.Instance.SendChangeAppearance(2, id);
                            if (s[1].ToLower() == "job" || s[1].ToLower() == "class")
                                NetworkManager.Instance.SendChangeAppearance(3, id);
                            if (s[1].ToLower() == "weapon")
                                NetworkManager.Instance.SendChangeAppearance(4, id);
                        }
                    }

                    if (s.Length == 4)
                    {
                        if (int.TryParse(s[2], out var id) && int.TryParse(s[3], out var subId))
                            if (s[1].ToLower() == "hair")
                                NetworkManager.Instance.SendChangeAppearance(1, id, subId);
                    }
                }

                if (s[0] == "/speed")
                {
                    if (s.Length > 1 && int.TryParse(s[1], out var speed))
                        NetworkManager.Instance.SendAdminChangeSpeed(speed);
                    else
                        cameraFollower.AppendChatText("<color=yellow>Error</color>: Incorrect parameters.");
                }

                if (s[0] == "/admin")
                {
                    NetworkManager.Instance.SendAdminChangeSpeed(50);
                    NetworkManager.Instance.SendAdminHideCharacter(true);
                }

                if (s[0] == "/ailog" || s[0] == "/ailogging")
                {
                    NetworkManager.Instance.SendAiLogging();
                }

                if (s[0] == "/kill")
                {
                    NetworkManager.Instance.SendAdminKillMobAction(false);
                }

                if (s[0] == "/killall")
                {
                    NetworkManager.Instance.SendAdminKillMobAction(true);
                }

                if (s[0] == "/die" || s[0] == "/suicide")
                {
                    NetworkManager.Instance.SendAdminDieAction();
                }

                if (s[0] == "/showexp") {
                    GameConfig.Data.ShowExpGainInChat = !GameConfig.Data.ShowExpGainInChat;
                    UiManager.Instance.ConfigManager.Refresh();
                    cameraFollower.AppendChatText($"showexp turned {(GameConfig.Data.ShowExpGainInChat == true ? "on" : "off")}",TextColor.System);
                }

                if (s[0] == "/sit")
                {
                    if (controllable.SpriteAnimator.State == SpriteState.Idle || controllable.SpriteAnimator.State == SpriteState.Standby)
                        NetworkManager.Instance.ChangePlayerSitStand(true);
                    if (controllable.SpriteAnimator.State == SpriteState.Sit)
                        NetworkManager.Instance.ChangePlayerSitStand(false);
                }

                if (s[0] == "/randomize" || s[0] == "/random")
                    NetworkManager.Instance.SendChangeAppearance(0);

                if (s[0] == "/effect" && s.Length > 1)
                {
                    if (int.TryParse(s[1], out var id))
                    {
                        if (Application.isEditor && s.Length > 2 && int.TryParse(s[2], out var count))
                        {
                            for (var i = 0; i < count; i++)
                                cameraFollower.AttachEffectToEntity(id, controllable.gameObject);
                        }
                        else
                            cameraFollower.AttachEffectToEntity(id, controllable.gameObject);
                    }
                    else
                        cameraFollower.AttachEffectToEntity(s[1], controllable.gameObject);
                }

                if (s[0] == "/status" && s.Length > 2)
                {
                    if (!Enum.TryParse<CharacterStatusEffect>(s[1], out var status))
                    {
                        cameraFollower.AppendChatText($"<color=yellow>Error</color>: Could not find status effect {s[1]}.");
                        return;
                    }

                    var len = s.Length >= 3 && float.TryParse(s[2], out var f) ? f : 15f;
                    var v1 = s.Length >= 4 && int.TryParse(s[3], out var v) ? v : 0;
                    var v2 = s.Length >= 5 && int.TryParse(s[4], out v) ? v : 0;
                    var v3 = s.Length >= 6 && int.TryParse(s[5], out v) ? v : 0;
                    var v4 = s.Length >= 7 && int.TryParse(s[6], out v) ? v : 0;

                    NetworkManager.Instance.SendAdminStatusAdd(status, len, v1, v2, v3, v4);
                }
                
                if (s[0] == "/event" && s.Length > 2)
                {
                    var name = s[1];
                    var v1 = s.Length >= 3 && int.TryParse(s[2], out var v) ? v : 0;
                    var v2 = s.Length >= 4 && int.TryParse(s[3], out v) ? v : 0;
                    var v3 = s.Length >= 5 && int.TryParse(s[4], out v) ? v : 0;
                    var v4 = s.Length >= 6 && int.TryParse(s[5], out v) ? v : 0;

                    NetworkManager.Instance.SendAdminCreateEvent(name, v1, v2, v3, v4);
                }

                if (s[0] == "/reloadscript" || s[0] == "/scriptreload")
                {
                    NetworkManager.Instance.SendAdminAction(AdminAction.ReloadScripts);
                }

                if (s[0] == "/grantskill" && s.Length >= 2)
                {
                    if (Enum.TryParse<CharacterSkill>(s[1], out var skill))
                    {
                        if (s.Length > 2 && int.TryParse(s[2], out var lvl))
                            NetworkManager.Instance.AdminGrantSkill(skill, lvl);
                        else
                            NetworkManager.Instance.AdminGrantSkill(skill, 1);
                    }
                    else
                        cameraFollower.AppendChatText($"Could not parse skill name {s[1]}", TextColor.Error);
                }

                if (s[0] == "/god" || s[0] == "/godmode")
                {
                    if (s.Length == 1)
                    {
                        NetworkManager.Instance.SendAdminGodModeSelf(true);
                        return;
                    }

                    if (s[1].ToLower() == "off")
                    {
                        NetworkManager.Instance.SendAdminGodModeSelf(false);
                        return;
                    }

                    var chopStart = s[0].Length + 1;

                    if (s[^1].ToLower() == "off")
                    {
                        var name = text.Substring(chopStart, text.Length - (chopStart + 4));
                        NetworkManager.Instance.SendAdminGodModeOther(name, false);
                    }
                    else
                    {
                        var name = text.Substring(chopStart);
                        NetworkManager.Instance.SendAdminGodModeOther(name, true);
                    }
                }

                if (s[0] == "/servergc")
                {
                    NetworkManager.Instance.SendAdminAction(AdminAction.ForceGC);
                }

                if (s[0] == "/clear" || s[0] == "/cls" || s[0] == "/clearchat")
                    cameraFollower.ResetChat();

                if (s[0] == "/debug")
                {
                    if (s.Length < 3)
                    {
                        cameraFollower.AppendChatText("<color=yellow>Incorrect parameters. Usage:</color>/debug valueName value");
                        return;
                    }

                    if (float.TryParse(s[2], out var f))
                        DebugValueHolder.Set(s[1], f);
                    else
                        cameraFollower.AppendChatText("<color=yellow>Incorrect parameters. Usage:</color>/debug valueName float");
                }

                if ((s[0] == "/organize" || s[0] == "/party") && s.Length > 1)
                {
                    if (PlayerState.Instance.IsInParty)
                    {
                        cameraFollower.AppendChatText($"<color=yellow>You are already in a party. You'll need to /leave to form a new party.</color>");
                        return;
                    }

                    var msg = s[1];
                    if (!msg.Contains("\""))
                        text.Substring(s[0].Length + 1);
                    NetworkManager.Instance.OrganizeParty(msg);
                }

                if (s[0] == "/invite" && s.Length > 1)
                {
                    var name = s[1];
                    if (!name.Contains("\""))
                        text.Substring(s[0].Length + 1);
                    if (!PlayerState.Instance.IsInParty)
                        cameraFollower.AppendChatText($"<color=yellow>You must first create a party with /organize before you can invite a player.</color>");
                    else
                        NetworkManager.Instance.PartyInviteByName(name);
                }

                if (s[0] == "/accept")
                {
                    if (s.Length > 1 && int.TryParse(s[1], out var id))
                    {
                        NetworkManager.Instance.PartyAcceptInvite(id);
                    }
                    else
                    {
                        if (PlayerState.Instance.InvitedPartyId < 0)
                            cameraFollower.AppendChatText($"<color=yellow>You do not have a pending party invite.</color>");
                        else
                            NetworkManager.Instance.PartyAcceptInvite(PlayerState.Instance.InvitedPartyId);
                    }
                }

                if (s[0] == "/leave" || s[0] == "/leaveparty")
                {
                    if (!PlayerState.Instance.IsInParty)
                        cameraFollower.AppendChatText($"<color=yellow>You are not currently in a party.</color>");
                    else
                        NetworkManager.Instance.LeaveParty();
                }

                if (s[0] == "/partyinfo")
                {
                    var state = PlayerState.Instance;
                    if (!state.IsInParty || state.PartyMembers == null || state.PartyMembers.Count == 0)
                    {
                        cameraFollower.AppendChatText("You are not currently in a party.");
                        return;
                    }

                    var sb = new StringBuilder();
                    sb.Append("Party members: ");

                    var count = 0;
                    foreach (var (_, member) in state.PartyMembers)
                    {
                        if (count > 0)
                            sb.Append(", ");
                        count++;
                        sb.Append($"{member.PlayerName}");
                        if (member.EntityId == 0)
                            sb.Append(" (offline)");
                    }

                    cameraFollower.AppendChatText(sb.ToString());
                }

                if (s[0] == "/signalnpc" && s.Length > 1)
                {
                    var npcName = s[1];
                    var signalVal = s.Length > 2 ? s[2] : "";
                    var v1 = s.Length > 3 && int.TryParse(s[3], out var i) ? i : 0;
                    var v2 = s.Length > 4 && int.TryParse(s[4], out i) ? i : 0;
                    var v3 = s.Length > 5 && int.TryParse(s[5], out i) ? i : 0;
                    var v4 = s.Length > 6 && int.TryParse(s[6], out i) ? i : 0;

                    NetworkManager.Instance.SendAdminSignalNpc(npcName, signalVal, v1, v2, v3, v4);
                }

                if (s[0] == "/shutdown")
                {
                    if (s.Length == 1)
                        NetworkManager.Instance.SendServerShutdown(60, "");
                    else
                    {
                        if (int.TryParse(s[1], out var seconds))
                        {
                            if (s.Length > 3)
                                cameraFollower.AppendChatText("Incorrectly formated command", TextColor.Error);
                            else
                                NetworkManager.Instance.SendServerShutdown(seconds, s.Length > 2 ? s[2] : "");
                        }
                        else
                        {
                            if (s.Length > 2)
                                cameraFollower.AppendChatText("Incorrectly formated command", TextColor.Error);
                            else
                                NetworkManager.Instance.SendServerShutdown(60, s[1]);
                        }
                    }
                }

                if (s[0] == "/changecart" || s[0] == "/changeriding")
                {
                    if (s.Length != 2 || !int.TryParse(s[1], out var cartId))
                    {
                        cameraFollower.AppendChatText($"Incorrectly formatted command. Command should look like: /{s[0]} #");
                        return;
                    }

                    NetworkManager.Instance.SendChangeCart(cartId - 1);
                    return;
                }

                if (emoteList.TryGetValue(s[0], out var emote))
                    NetworkManager.Instance.SendEmote(emote);
            }
            else
            {
                if (text.Length > 255)
                {
                    cameraFollower.AppendChatText("<color=yellow>Error</color>: Text too long.");
                }
                else
                    NetworkManager.Instance.SendSay(text, PlayerChatType.Say);
                //AppendChatText(text);
            }
        }
    }
}