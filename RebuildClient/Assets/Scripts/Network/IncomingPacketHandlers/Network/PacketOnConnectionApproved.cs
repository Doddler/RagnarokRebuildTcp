using System;
using System.Collections.Generic;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.UI.TitleScreen;
using RebuildSharedData.Networking;
using RebuildSharedData.Util;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Network
{
    [ClientPacketHandler(PacketType.ConnectionApproved)]
    public class PacketOnConnectionApproved : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            if (msg.ReadBoolean()) //hasToken
                GameConfig.Data.SavedLoginToken = Convert.ToBase64String(msg.ReadBytes(msg.ReadInt32()));
            else
                GameConfig.Data.SavedLoginToken = null;

            var characterCount = msg.ReadInt32();
            Debug.Log($"Characters: {characterCount}");

            var characters = new List<ClientCharacterSummary>();
            
            for(var i = 0; i < 3; i++)
                characters.Add(null);
            
            for (var i = 0; i < characterCount; i++)
            {
                var ch = new ClientCharacterSummary();
                ch.Name = msg.ReadString();
                ch.CharacterSlot = msg.ReadInt32();
                ch.Map = msg.ReadString();
                var summaryLen = msg.ReadInt32();
                if (summaryLen > 0)
                {
                    ch.SummaryData = new int[(int)PlayerSummaryData.SummaryDataMax];
                    var readLen =  summaryLen / 4;
                    for (var j = 0; j < readLen; j++)
                        ch.SummaryData[j] = msg.ReadInt32();
                }
                characters[ch.CharacterSlot] = ch;
            }
            
            Network.TitleScreen.OpenCharacterSelect(characters);
        }
    }
}