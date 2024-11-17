using System;
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
            State.CurrentWeight = msg.ReadInt32();
            State.MaxWeight = msg.ReadInt32();
            State.SkillPoints = msg.ReadInt32();
            var skills = msg.ReadInt32();
            
            State.KnownSkills.Clear();
            for(var i = 0; i < skills; i++)
                State.KnownSkills.Add((CharacterSkill)msg.ReadByte(), msg.ReadByte());
            
            UiManager.SkillManager.UpdateAvailableSkills();

            var hasInventory = msg.ReadBoolean();

            if (hasInventory)
            {
                State.Inventory.Deserialize(msg);
                State.Cart.Deserialize(msg);
                State.Storage.Deserialize(msg);
                State.EquippedBagIdHashes.Clear();
                for (var i = 0; i < 10; i++)
                {
                    var bagId = msg.ReadInt32();
                    State.EquippedItems[i] = bagId;
                    State.EquippedBagIdHashes.Add(bagId);
                }
                
                UiManager.EquipmentWindow.RefreshEquipmentWindow();

                if(Application.isEditor)
                    Debug.Log($"Equipped items: " + string.Join(", ", State.EquippedItems));
            }

            CameraFollower.Instance.UpdatePlayerHP(hp, maxHp);
            CameraFollower.Instance.UpdatePlayerSP(sp, maxSp);
            UiManager.Instance.SkillHotbar.UpdateItemCounts();
            UiManager.Instance.InventoryWindow.UpdateActiveVisibleBag();
            
#if UNITY_EDITOR
            if (!hasInventory)
                return;
            
            var sb = new StringBuilder(); 
            if (State.Inventory != null)
            {
                foreach (var i in State.Inventory.GetInventoryData())
                {
                    // var data = ClientDataLoader.Instance.GetItemById(i.Value.Id);
                    if(i.Value.Type == ItemType.RegularItem)
                        sb.AppendLine($"{i.Key} - {i.Value}");
                    else
                    {
                        var eq = State.EquippedBagIdHashes.Contains(i.Key) ? " *Equipped*" : "";
                        sb.Append($"{i.Key} - {i.Value} <");
                        for (var j = 0; j < 4; j++)
                        {
                            if (j > 0) sb.Append(", ");
                            sb.Append(i.Value.UniqueItem.SlotData(j));
                        }

                        sb.AppendLine($"> : (Guid {i.Value.UniqueItem.UniqueId}) {eq}");
                    }
                }
            }

            
            Debug.Log($"Loaded inventory with following data:\n{sb}");
#endif
        }
    }
}