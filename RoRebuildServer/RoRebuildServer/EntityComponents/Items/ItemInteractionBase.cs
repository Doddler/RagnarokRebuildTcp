using RebuildSharedData.Data;
using RebuildSharedData.Enum;

namespace RoRebuildServer.EntityComponents.Items;

public interface IItemLoader
{
    public void Load();
}

public class ItemInteractionBase
{
    public virtual void Init(Player player, CombatEntity combatEntity) { }

    public virtual bool OnUse(Player player, CombatEntity combatEntity)
    {
        return true;
    }

    public virtual void OnEquip(Player player, CombatEntity combatEntity, ItemEquipState state, UniqueItem item, EquipSlot position) { }

    public virtual void OnUnequip(Player player, CombatEntity combatEntity, ItemEquipState state, UniqueItem item, EquipSlot position) { }

}