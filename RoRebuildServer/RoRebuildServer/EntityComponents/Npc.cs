using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.Npc)]
public class Npc
{
    public Entity Entity;
    public string Name = null!;
    public bool HasTouch;
    public bool HasInteract;
    
    public int[] ValuesInt = new int[NpcInteractionState.StorageCount];
    public string[] ValuesString = new string[NpcInteractionState.StorageCount];

    public NpcBehaviorBase Behavior = null!;

    public void Update()
    {

    }

    public void OnTouch(Player player)
    {
        player.IsInNpcInteraction = true;
        player.NpcInteractionState.BeginInteraction(ref Entity, player);
        var res = Behavior.OnTouch(this, player, player.NpcInteractionState);
        if (res == NpcInteractionResult.EndInteraction)
        {
            player.IsInNpcInteraction = false;
            player.NpcInteractionState.Reset();
        }
    }

    public void OnInteract(Player player)
    {

    }
}