using RebuildSharedData.Enum;

namespace RoRebuildServer.EntityComponents.Npcs;

public abstract class NpcBehaviorBase
{
    public virtual void Init(Npc npc) {}

    public virtual NpcInteractionResult OnClick(Npc npc, Player player, NpcInteractionState state)
    {
        return NpcInteractionResult.EndInteraction;
    }


    public virtual NpcInteractionResult OnTouch(Npc npc, Player player, NpcInteractionState state)
    {
        return NpcInteractionResult.EndInteraction;
    }
}