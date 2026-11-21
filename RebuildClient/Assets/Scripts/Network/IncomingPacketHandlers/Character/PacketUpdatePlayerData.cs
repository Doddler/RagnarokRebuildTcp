using System;
using System.Text;
using Assets.Scripts.Network.HandlerBase;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.UpdatePlayerData)]
    public class PacketUpdatePlayerData : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            Span<int> data = stackalloc int[PlayerClientStatusDef.PlayerUpdateData.Length];
            for (var i = 0; i < data.Length; i++)
                data[i] = msg.ReadInt32();

            Span<int> stats = stackalloc int[PlayerClientStatusDef.PlayerUpdateStats.Length];
            for (var i = 0; i < stats.Length; i++)
                stats[i] = msg.ReadInt32();

            var attackDelay = msg.ReadFloat();
            State.SetWeight(msg.ReadInt32());
            State.SetCartWeight(msg.ReadInt32());
            State.SetCharacterData(data, stats, attackDelay);

            var hp = State.GetStat(CharacterStat.Hp); //we don't assign these to player state directly so we can do the health bar animation thing
            var maxHp = State.GetStat(CharacterStat.MaxHp);
            var sp = State.GetStat(CharacterStat.Sp);
            var maxSp = State.GetStat(CharacterStat.MaxSp);

            var hasSkills = msg.ReadBoolean();

            if (hasSkills)
            {
                var known = PacketRefreshGrantedSkills.ReadSkills(msg);
                var granted = PacketRefreshGrantedSkills.ReadSkills(msg);
                State.SetSkills(known, granted);
            }

            var hasInventory = msg.ReadBoolean();

            if (hasInventory)
            {
                State.Inventory.Deserialize(msg);
                if(msg.ReadByte() == 1) //has cart
                    State.Cart.Deserialize(msg);

                Span<int> equipped = stackalloc int[State.EquippedItems.Count];
                for (var i = 0; i < equipped.Length; i++)
                    equipped[i] = msg.ReadInt32();
                State.SetEquipment(equipped, msg.ReadInt32());

                if(Application.isEditor)
                    Debug.Log($"Equipped items: " + string.Join(", ", State.EquippedItems));
            }

            //no character yet at login; the spawn brings the hp
            if (State.Character != null)
                State.Character.SetHp(hp, maxHp, false);
            State.SetSp(sp, maxSp);

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
                        var eq = State.IsEquipped(i.Key) ? " *Equipped*" : "";
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