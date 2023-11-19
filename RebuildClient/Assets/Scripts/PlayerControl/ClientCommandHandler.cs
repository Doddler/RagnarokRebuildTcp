using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using JetBrains.Annotations;
using RebuildSharedData.Networking;
using UnityEngine;

namespace PlayerControl
{
    public static class ClientCommandHandler
    {
        [CanBeNull]
        private static string[] SplitStringCommand(string input)
        {
            var outList = new List<string>();
            var inQuote = false;
            var sb = new StringBuilder();

            foreach (var c in input)
            {
                if (c == ' ' && !inQuote)
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
                var s = SplitStringCommand(text);
                if (s == null)
                {
                    cameraFollower.AppendError($"Malformed slash command, could not execute.");
                    return;
                }

                Debug.Log($"string command: " + string.Join('|', s));

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

                    cameraFollower.AppendChatText($"Location: {mapname} {srcPosition.x},{srcPosition.y}");
                }

                if (s[0] == "/name" || s[0] == "/changename")
                {
                    var newName = text.Substring(s[0].Length + 1);
                    NetworkManager.Instance.SendChangeName(newName);
                }

                if (s[0] == "/level")
                {
                    if (s.Length == 1 || !int.TryParse(s[1], out var level))
                        NetworkManager.Instance.SendAdminLevelUpRequest(0);
                    else
                        NetworkManager.Instance.SendAdminLevelUpRequest(level);
                }

                if (s[0] == "/hide")
                {
                    NetworkManager.Instance.SendAdminHideCharacter(!controllable.IsHidden);
                }

                if (s[0] == "/summon")
                {
                    if (s.Length < 2)
                    {
                        cameraFollower.AppendError("Invalid summon monster request.");
                    }
                    
                    //var failed = false;
                    var count = 1;
                    var name = s[1];
                    var nameMax = s.Length - 1;

                    if (s.Length >= 3 && int.TryParse(s[s.Length - 1], out var newCount))
                    {
                        count = newCount;
                        nameMax--;
                    }
                    
                    Debug.Log($"Summon '{name}' {count} {nameMax}");

                    if (s.Length > nameMax)
                        name = String.Join(" ", s.Skip(1).Take(s.Length - 2));

                    if (!SpriteDataLoader.Instance.IsValidMonsterName(name))
                        cameraFollower.AppendError($"The monster name '{name}' is not valid.");
                    else
                        NetworkManager.Instance.SendAdminSummonMonster(name, count);
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

                if (s[0] == "/randomize" || s[0] == "/random")
                    NetworkManager.Instance.SendChangeAppearance(0);

                if (s[0] == "/effect" && s.Length > 1)
                {
                    if (int.TryParse(s[1], out var id))
                        cameraFollower.AttachEffectToEntity(id, controllable.gameObject);
                    else
                        cameraFollower.AttachEffectToEntity(s[1], controllable.gameObject);
                }

                if (s[0] == "/reloadscript" || s[0] == "/scriptreload")
                {
                    NetworkManager.Instance.SendAdminAction(AdminAction.ReloadScripts);
                }


                if (s[0] == "/servergc")
                {
                    NetworkManager.Instance.SendAdminAction(AdminAction.ForceGC);
                }
                
                if(s[0] == "/clear" || s[0] == "/cls" || s[0] == "/clearchat")
                    cameraFollower.ResetChat();
            }
            else
            {
                if (text.Length > 255)
                {
                    cameraFollower.AppendChatText("<color=yellow>Error</color>: Text too long.");
                }
                else
                    NetworkManager.Instance.SendSay(text);
                //AppendChatText(text);
            }
        }
    }
}