namespace RoRebuildServer.EntityComponents.Items;

public class ItemInteractionBase
{
    public virtual void Init(Player player, CombatEntity combatEntity) { }

    public virtual void OnUse(Player player, CombatEntity combatEntity)
    {
        return;
    }
    
}