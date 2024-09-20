using System.Text;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.UpdatePlayerData)]
    public class PacketUpdatePlayerData : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var hp = msg.ReadInt32();
            var maxHp = msg.ReadInt32();
            var sp = msg.ReadInt32();
            var maxSp = msg.ReadInt32();
            State.SkillPoints = msg.ReadInt32();
            var skills = msg.ReadInt32();
            
            State.KnownSkills.Clear();
            for(var i = 0; i < skills; i++)
                State.KnownSkills.Add((CharacterSkill)msg.ReadByte(), msg.ReadByte());
            
            UiManager.SkillManager.UpdateAvailableSkills();
            
            State.Inventory.Deserialize(msg);
            State.Cart.Deserialize(msg);
            State.Storage.Deserialize(msg);
            
            CameraFollower.Instance.UpdatePlayerSP(sp, maxSp);
            UiManager.Instance.SkillHotbar.UpdateItemCounts();

            var sb = new StringBuilder(); 
            if (State.Inventory != null)
            {
                foreach (var i in State.Inventory.GetInventoryData)
                {
                    var data = ClientDataLoader.Instance.GetItemById(i.Value.Id);
                    sb.AppendLine($"{data.Name}: {i.Value.Count} ea.");
                }
            }

            Debug.Log($"Loaded inventory with following data:\n{sb.ToString()}");
        }
    }
}