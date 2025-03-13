using System;
using System.Text;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.RefineItem;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
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
            var hasStatChange = false;
            foreach (var data in PlayerClientStatusDef.PlayerUpdateData)
            {
                var newVal = msg.ReadInt32();
                if(data >= PlayerStat.Str && data <= PlayerStat.Luk)
                    if (State.CharacterData[(int)data] != newVal)
                        hasStatChange = true;
                State.CharacterData[(int)data] = newVal;
            }
            
            foreach (var stats in PlayerClientStatusDef.PlayerUpdateStats)
                State.CharacterStats[(int)stats] = msg.ReadInt32();

            State.Level = State.GetData(PlayerStat.Level);
            State.AttackSpeed = msg.ReadFloat();
            State.CurrentWeight = msg.ReadInt32();

            var hp = State.GetStat(CharacterStat.Hp); //we don't assign these to player state directly so we can do the health bar animation thing
            var maxHp = State.GetStat(CharacterStat.MaxHp);
            var sp = State.GetStat(CharacterStat.Sp);
            var maxSp = State.GetStat(CharacterStat.MaxSp);
            
            State.MaxWeight = State.GetStat(CharacterStat.WeightCapacity);
            State.SkillPoints = State.GetData(PlayerStat.SkillPoints);
            State.Zeny = State.GetData(PlayerStat.Zeny);

            var hasSkills = msg.ReadBoolean();

            if (hasSkills)
            {
                var skills = msg.ReadInt32();

                State.KnownSkills.Clear();
                for (var i = 0; i < skills; i++)
                    State.KnownSkills.Add((CharacterSkill)msg.ReadByte(), msg.ReadByte());

                UiManager.SkillManager.UpdateAvailableSkills();
            }

            var hasInventory = msg.ReadBoolean();

            if (hasInventory)
            {
                State.Inventory.Deserialize(msg);
                State.Cart.Deserialize(msg);
                State.EquippedBagIdHashes.Clear();
                for (var i = 0; i < 10; i++)
                {
                    var bagId = msg.ReadInt32();
                    State.EquippedItems[i] = bagId;
                    State.EquippedBagIdHashes.Add(bagId);
                }

                State.AmmoId = msg.ReadInt32();
                
                UiManager.EquipmentWindow.RefreshEquipmentWindow();

                if(Application.isEditor)
                    Debug.Log($"Equipped items: " + string.Join(", ", State.EquippedItems));
            }

            var jobMax = ClientDataLoader.Instance.GetJobExpRequired(State.JobId, State.GetData(PlayerStat.JobLevel));
            
            CameraFollower.Instance.UpdatePlayerHP(hp, maxHp);
            CameraFollower.Instance.UpdatePlayerSP(sp, maxSp);
            CameraFollower.Instance.CharacterDetailBox.JobLvlDisplay.text = $"Job Lv. {State.GetData(PlayerStat.JobLevel)}";
            CameraFollower.Instance.UpdatePlayerJobExp(State.GetData(PlayerStat.JobExp), jobMax);
            CameraFollower.Instance.CharacterDetailBox.UpdateWeightAndZeny();

            if (CameraFollower.Instance.TargetControllable != null)
            {
                CameraFollower.Instance.TargetControllable.SetHp(hp, maxHp, false);
                CameraFollower.Instance.TargetControllable.SetSp(sp, maxSp);
            }

            UiManager.Instance.SkillHotbar.UpdateItemCounts();
            UiManager.Instance.InventoryWindow.UpdateActiveVisibleBag();
            if(hasStatChange)
                UiManager.Instance.StatusWindow.ResetStatChanges();
            else
                UiManager.Instance.StatusWindow.UpdateCharacterStats();
            UiManager.Instance.SkillManager.RefreshSkillAvailability();
            
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