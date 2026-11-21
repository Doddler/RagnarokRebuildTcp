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
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RebuildSharedData.Util;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayerControl
{
    public static class ClientCommandHandler
    {
        private static Dictionary<string, int> emoteList = new();

        private static readonly (string Prefix, PlayerChatType Type)[] ChatPrefixes =
        {
            ("/sh", PlayerChatType.Shout), ("/s", PlayerChatType.Shout), ("/shout", PlayerChatType.Shout),
            ("/p", PlayerChatType.Party), ("/party", PlayerChatType.Party)
        };

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

        private static void ShowUsage(CameraFollower cameraFollower, string usage) =>
            cameraFollower.AppendError($"Incorrect parameters. Usage: {usage}");

        //false when given but not a number
        private static bool TryGetOptionalInt(string[] s, int index, out int value)
        {
            value = 0;
            return index >= s.Length || int.TryParse(s[index], out value);
        }

        private static bool TryGetOptionalInts(string[] s, int start, out int v1, out int v2, out int v3, out int v4) =>
            TryGetOptionalInt(s, start, out v1) & TryGetOptionalInt(s, start + 1, out v2)
                                                & TryGetOptionalInt(s, start + 2, out v3) & TryGetOptionalInt(s, start + 3, out v4);

        //Every command ends in an action or a message, never silently.
        public static void HandleClientCommand(CameraFollower cameraFollower, ServerControllable controllable, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!text.StartsWith("/"))
            {
                if (text.Length > 255)
                    cameraFollower.AppendError("Text too long.");
                else
                    NetworkManager.Instance.SendSay(text, PlayerChatType.Say);
                return;
            }

            foreach (var (prefix, type) in ChatPrefixes)
            {
                if (text.Length > prefix.Length && text[prefix.Length] == ' ' && text.StartsWith(prefix))
                {
                    NetworkManager.Instance.SendSay(text.Substring(prefix.Length + 1), type);
                    return;
                }
            }

            var s = SplitStringCommand(text);
            if (s == null)
            {
                cameraFollower.AppendError($"Malformed slash command, could not execute.");
                return;
            }

            Debug.Log($"string command: " + string.Join('|', s));

            foreach (var (prefix, type) in ChatPrefixes)
            {
                if (s[0] == prefix)
                {
                    ShowUsage(cameraFollower, type == PlayerChatType.Party ? $"{prefix} <message> (use /organize <party name> to form a party)" : $"{prefix} <message>");
                    return;
                }
            }

            switch (s[0])
            {
                case "/memo":
                    WarpPortalWindow.RunMemoCommand();
                    return;

                case "/warp":
                {
                    if (s.Length == 2)
                        NetworkManager.Instance.SendMoveRequest(s[1]);
                    else if (s.Length == 4 && int.TryParse(s[2], out var x) && int.TryParse(s[3], out var y))
                        NetworkManager.Instance.SendMoveRequest(s[1], x, y);
                    else
                        ShowUsage(cameraFollower, "/warp <map> [x y]");
                    return;
                }

                case "/where":
                {
                    var mapname = NetworkManager.Instance.CurrentMap;
                    cameraFollower.WalkProvider.GetMapPositionForWorldPosition(cameraFollower.Target.transform.position, out var srcPosition);

                    cameraFollower.AppendChatText($"Client location: {mapname} {srcPosition.x},{srcPosition.y}");
                    NetworkManager.Instance.SendClientTextCommand(ClientTextCommand.Where);
                    return;
                }

                case "/info":
                    NetworkManager.Instance.SendClientTextCommand(ClientTextCommand.Info);
                    return;

                case "/adminify":
                    if (s.Length > 1)
                        NetworkManager.Instance.SendClientTextCommand(ClientTextCommand.Adminify, text.Substring(10));
                    else
                        NetworkManager.Instance.SendClientTextCommand(ClientTextCommand.Adminify);
                    return;

                case "/name":
                case "/changename":
                {
                    if (s.Length < 2)
                    {
                        ShowUsage(cameraFollower, $"{s[0]} <new name>");
                        return;
                    }

                    var newName = text.Substring(s[0].Length + 1).Trim();
                    var error = NameValidator.GetCharacterNameError(newName);
                    if (error != null)
                        cameraFollower.AppendError(error);
                    else
                        NetworkManager.Instance.SendChangeName(newName);
                    return;
                }

                case "/find":
                    if (s.Length < 2)
                        ShowUsage(cameraFollower, "/find <name>");
                    else
                        NetworkManager.Instance.SendAdminFind(text.Substring(s[0].Length + 1));
                    return;

                case "/level":
                case "/joblevel":
                {
                    //no number raises the level by one
                    if (s.Length > 2 || !TryGetOptionalInt(s, 1, out var level))
                        ShowUsage(cameraFollower, $"{s[0]} [levels to add]");
                    else
                        NetworkManager.Instance.SendAdminLevelUpRequest(level, s[0] == "/joblevel");
                    return;
                }

                case "/skillreset":
                    NetworkManager.Instance.SendAdminResetSkillPoints();
                    return;

                case "/statreset":
                    NetworkManager.Instance.SendAdminResetStatPoints();
                    return;

                case "/hide":
                    NetworkManager.Instance.SendAdminHideCharacter(!controllable.IsHidden);
                    return;

                case "/return":
                    NetworkManager.Instance.SendRespawn(false);
                    return;

                case "/logout":
                    GameConfig.SaveConfig();
                    NetworkManager.Instance.Disconnect();
                    SceneManager.LoadScene(0);
                    return;

                case "/item":
                {
                    if (s.Length < 2)
                    {
                        ShowUsage(cameraFollower, "/item <item name> [count]");
                        return;
                    }

                    var count = 1;
                    var nameMax = s.Length;

                    if (s.Length >= 3 && int.TryParse(s[s.Length - 1], out var newCount))
                    {
                        count = newCount;
                        nameMax--;
                    }

                    var name = string.Join(" ", s.Skip(1).Take(nameMax - 1));

                    if (!ClientDataLoader.Instance.TryGetItemByName(name, out var item))
                        cameraFollower.AppendError($"The item name '{name}' is not valid.");
                    else
                        NetworkManager.Instance.SendAdminCreateItem(item.Id, count);
                    return;
                }

                case "/refine":
                {
                    if (s.Length < 2)
                    {
                        ShowUsage(cameraFollower, "/refine <slot> [refine level]");
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
                    return;
                }

                case "/summon":
                case "/boss":
                case "/monster":
                {
                    if (s.Length < 2)
                    {
                        ShowUsage(cameraFollower, $"{s[0]} <monster name> [count]");
                        return;
                    }

                    var count = 1;
                    var nameMax = s.Length;

                    if (s.Length >= 3 && int.TryParse(s[s.Length - 1], out var newCount))
                    {
                        count = newCount;
                        nameMax--;
                    }

                    var name = string.Join(" ", s.Skip(1).Take(nameMax - 1));

                    if (!ClientDataLoader.Instance.IsValidMonsterName(name) && !ClientDataLoader.Instance.IsValidMonsterCode(name))
                        cameraFollower.AppendError($"The monster name '{name}' is not valid.");
                    else
                        NetworkManager.Instance.SendAdminSummonMonster(name, count, s[0] == "/boss");
                    return;
                }

                case "/bgm":
                    AudioManager.Instance.ToggleMute();
                    return;

                case "/emote":
                    if (s.Length == 2 && int.TryParse(s[1], out var emoteId))
                        cameraFollower.Emote(emoteId);
                    else
                        ShowUsage(cameraFollower, "/emote <emote id>");
                    return;

                case "/change":
                {
                    if (s.Length == 1)
                    {
                        NetworkManager.Instance.SendChangeAppearance(0);
                        return;
                    }

                    var mode = s[1].ToLower() switch
                    {
                        "hair" => 1,
                        "gender" => 2,
                        "job" or "class" => 3,
                        "weapon" => 4,
                        _ => -1
                    };

                    //only hair takes a second value, its colour
                    var maxLength = mode == 1 ? 4 : 3;
                    if (mode < 0 || s.Length > maxLength
                                 || !TryGetOptionalInt(s, 2, out var id) || !TryGetOptionalInt(s, 3, out var subId))
                    {
                        ShowUsage(cameraFollower, "/change [hair [style [colour]] | gender [id] | job [id] | weapon [id]]");
                        return;
                    }

                    if (s.Length == 2)
                    {
                        if (mode == 2)
                            NetworkManager.Instance.SendChangeAppearance(2, controllable.IsMale ? 1 : 0);
                        else
                            NetworkManager.Instance.SendChangeAppearance(mode);
                    }
                    else if (s.Length == 3)
                        NetworkManager.Instance.SendChangeAppearance(mode, id);
                    else
                        NetworkManager.Instance.SendChangeAppearance(mode, id, subId);
                    return;
                }

                case "/speed":
                    if (s.Length == 2 && int.TryParse(s[1], out var speed))
                        NetworkManager.Instance.SendAdminChangeSpeed(speed);
                    else
                        ShowUsage(cameraFollower, "/speed <move speed>");
                    return;

                case "/admin":
                    NetworkManager.Instance.SendAdminChangeSpeed(50);
                    NetworkManager.Instance.SendAdminHideCharacter(true);
                    return;

                case "/ailog":
                case "/ailogging":
                    NetworkManager.Instance.SendAiLogging();
                    return;

                case "/kill":
                    NetworkManager.Instance.SendAdminKillMobAction(false);
                    return;

                case "/killall":
                    NetworkManager.Instance.SendAdminKillMobAction(true);
                    return;

                case "/die":
                case "/suicide":
                    NetworkManager.Instance.SendAdminDieAction();
                    return;

                case "/showexp":
                    GameConfig.Data.ShowExpGainInChat = !GameConfig.Data.ShowExpGainInChat;
                    UiManager.Instance.ConfigManager.Refresh();
                    cameraFollower.AppendChatText($"showexp turned {(GameConfig.Data.ShowExpGainInChat == true ? "on" : "off")}",TextColor.System);
                    return;

                case "/sit":
                {
                    var state = controllable.SpriteAnimator.State;
                    if (state == SpriteState.Idle || state == SpriteState.Standby)
                        NetworkManager.Instance.ChangePlayerSitStand(true);
                    else if (state == SpriteState.Sit)
                        NetworkManager.Instance.ChangePlayerSitStand(false);
                    else
                        cameraFollower.AppendError("You can't sit down right now.");
                    return;
                }

                case "/randomize":
                case "/random":
                    NetworkManager.Instance.SendChangeAppearance(0);
                    return;

                case "/effect":
                {
                    if (s.Length < 2)
                    {
                        ShowUsage(cameraFollower, "/effect <effect id or name> [count]");
                        return;
                    }

                    if (int.TryParse(s[1], out var effectId))
                    {
                        if (Application.isEditor && s.Length > 2 && int.TryParse(s[2], out var count))
                        {
                            for (var i = 0; i < count; i++)
                                cameraFollower.AttachEffectToEntity(effectId, controllable.gameObject);
                        }
                        else
                            cameraFollower.AttachEffectToEntity(effectId, controllable.gameObject);
                    }
                    else
                        cameraFollower.AttachEffectToEntity(s[1], controllable.gameObject);
                    return;
                }

                case "/status":
                {
                    const string usage = "/status <status name> [seconds] [value1] [value2] [value3] [value4]";
                    if (s.Length < 2 || s.Length > 7)
                    {
                        ShowUsage(cameraFollower, usage);
                        return;
                    }

                    if (!Enum.TryParse<CharacterStatusEffect>(s[1], out var status))
                    {
                        cameraFollower.AppendError($"Could not find status effect {s[1]}.");
                        return;
                    }

                    var len = 15f;
                    if ((s.Length >= 3 && !float.TryParse(s[2], out len))
                        || !TryGetOptionalInts(s, 3, out var v1, out var v2, out var v3, out var v4))
                    {
                        ShowUsage(cameraFollower, usage);
                        return;
                    }

                    NetworkManager.Instance.SendAdminStatusAdd(status, len, v1, v2, v3, v4);
                    return;
                }

                case "/event":
                {
                    if (s.Length < 2 || s.Length > 6
                        || !TryGetOptionalInts(s, 2, out var v1, out var v2, out var v3, out var v4))
                    {
                        ShowUsage(cameraFollower, "/event <event name> [value1] [value2] [value3] [value4]");
                        return;
                    }

                    NetworkManager.Instance.SendAdminCreateEvent(s[1], v1, v2, v3, v4);
                    return;
                }

                case "/reloadscript":
                case "/scriptreload":
                    NetworkManager.Instance.SendAdminAction(AdminAction.ReloadScripts);
                    return;

                case "/grantskill":
                {
                    if (s.Length < 2 || s.Length > 3)
                    {
                        ShowUsage(cameraFollower, "/grantskill <skill name> [level]");
                        return;
                    }

                    if (!Enum.TryParse<CharacterSkill>(s[1], out var skill))
                    {
                        cameraFollower.AppendError($"Could not parse skill name {s[1]}.");
                        return;
                    }

                    var lvl = 1;
                    if (s.Length == 3 && !int.TryParse(s[2], out lvl))
                    {
                        ShowUsage(cameraFollower, "/grantskill <skill name> [level]");
                        return;
                    }

                    NetworkManager.Instance.AdminGrantSkill(skill, lvl);
                    return;
                }

                case "/god":
                case "/godmode":
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
                    return;
                }

                case "/servergc":
                    NetworkManager.Instance.SendAdminAction(AdminAction.ForceGC);
                    return;

                case "/clear":
                case "/cls":
                case "/clearchat":
                    cameraFollower.ResetChat();
                    return;

                case "/debug":
                    if (s.Length != 3 || !float.TryParse(s[2], out var debugValue))
                        ShowUsage(cameraFollower, "/debug <value name> <number>");
                    else
                        DebugValueHolder.Set(s[1], debugValue);
                    return;

                case "/organize":
                {
                    if (s.Length < 2)
                    {
                        ShowUsage(cameraFollower, "/organize <party name>");
                        return;
                    }

                    if (PlayerState.Instance.Party.IsInParty)
                    {
                        cameraFollower.AppendError("You are already in a party. You'll need to /leave to form a new party.");
                        return;
                    }

                    NetworkManager.Instance.OrganizeParty(string.Join(" ", s.Skip(1)));
                    return;
                }

                case "/invite":
                {
                    if (s.Length < 2)
                    {
                        ShowUsage(cameraFollower, "/invite <player name>");
                        return;
                    }

                    if (!PlayerState.Instance.Party.IsInParty)
                        cameraFollower.AppendError("You must first create a party with /organize before you can invite a player.");
                    else
                        NetworkManager.Instance.PartyInviteByName(string.Join(" ", s.Skip(1)));
                    return;
                }

                case "/accept":
                {
                    if (s.Length > 1)
                    {
                        if (s.Length == 2 && int.TryParse(s[1], out var partyId))
                            NetworkManager.Instance.PartyAcceptInvite(partyId);
                        else
                            ShowUsage(cameraFollower, "/accept [party id]");
                    }
                    else if (PlayerState.Instance.Party.InvitedPartyId < 0)
                        cameraFollower.AppendError("You do not have a pending party invite.");
                    else
                        NetworkManager.Instance.PartyAcceptInvite(PlayerState.Instance.Party.InvitedPartyId);
                    return;
                }

                case "/leave":
                case "/leaveparty":
                    if (!PlayerState.Instance.Party.IsInParty)
                        cameraFollower.AppendError("You are not currently in a party.");
                    else
                        NetworkManager.Instance.LeaveParty();
                    return;

                case "/partyinfo":
                {
                    var state = PlayerState.Instance;
                    if (!state.Party.IsInParty || state.Party.Members == null || state.Party.Members.Count == 0)
                    {
                        cameraFollower.AppendChatText("You are not currently in a party.");
                        return;
                    }

                    var sb = new StringBuilder();
                    sb.Append("Party members: ");

                    var count = 0;
                    foreach (var (_, member) in state.Party.Members)
                    {
                        if (count > 0)
                            sb.Append(", ");
                        count++;
                        sb.Append($"{member.PlayerName}");
                        if (member.EntityId == 0)
                            sb.Append(" (offline)");
                    }

                    cameraFollower.AppendChatText(sb.ToString());
                    return;
                }

                case "/signalnpc":
                {
                    if (s.Length < 2 || s.Length > 7
                        || !TryGetOptionalInts(s, 3, out var v1, out var v2, out var v3, out var v4))
                    {
                        ShowUsage(cameraFollower, "/signalnpc <npc name> [signal] [value1] [value2] [value3] [value4]");
                        return;
                    }

                    NetworkManager.Instance.SendAdminSignalNpc(s[1], s.Length > 2 ? s[2] : "", v1, v2, v3, v4);
                    return;
                }

                case "/shutdown":
                {
                    const string usage = "/shutdown [seconds] [message]";
                    if (s.Length == 1)
                        NetworkManager.Instance.SendServerShutdown(60, "");
                    else if (int.TryParse(s[1], out var seconds))
                    {
                        if (s.Length > 3)
                            ShowUsage(cameraFollower, usage);
                        else
                            NetworkManager.Instance.SendServerShutdown(seconds, s.Length > 2 ? s[2] : "");
                    }
                    else if (s.Length > 2)
                        ShowUsage(cameraFollower, usage);
                    else
                        NetworkManager.Instance.SendServerShutdown(60, s[1]);
                    return;
                }

                case "/changecart":
                    if (s.Length != 2 || !int.TryParse(s[1], out var cartId))
                        ShowUsage(cameraFollower, $"{s[0]} <number>");
                    else if (cartId <= 0)
                        NetworkManager.Instance.SendRemoveCart();
                    else
                        NetworkManager.Instance.SendChangeCartStyle(cartId - 1);
                    return;

                case "/disguise":
                {
                    if (s.Length < 2)
                    {
                        NetworkManager.Instance.SendAdminDisguise(-1);
                        return;
                    }

                    if (int.TryParse(s[1], out var disguiseId))
                    {
                        NetworkManager.Instance.SendAdminDisguise(disguiseId);
                        return;
                    }

                    //a trailing number is dropped, as with /summon
                    var nameMax = s.Length >= 3 && int.TryParse(s[^1], out _) ? s.Length - 1 : s.Length;
                    var name = string.Join(" ", s.Skip(1).Take(nameMax - 1));
                    var mon = ClientDataLoader.Instance.MonsterClassLookup.Values.FirstOrDefault(m => m.Name == name)
                              ?? ClientDataLoader.Instance.MonsterClassLookup.Values.FirstOrDefault(m => m.Code == name.ToUpperInvariant());
                    if (mon == null)
                        cameraFollower.AppendError($"The monster name '{name}' is not valid.");
                    else
                        NetworkManager.Instance.SendAdminDisguise(mon.Id);
                    return;
                }

                default:
                    if (emoteList.TryGetValue(s[0], out var emote))
                        NetworkManager.Instance.SendEmote(emote);
                    else
                        cameraFollower.AppendError($"Unknown command {s[0]}.");
                    return;
            }
        }
    }
}