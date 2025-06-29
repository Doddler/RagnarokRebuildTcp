using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

namespace RoRebuildServer.EntityComponents.Items;

public class VendNpcProxy : NpcBehaviorBase
{
    public override NpcInteractionResult OnClick(Npc npc, Player player, NpcInteractionState state)
    {
        if (state.Step == 0)
        {
            state.Step = 1;

            if (npc.Owner.TryGet<Player>(out var owner) && owner == player)
                return NpcInteractionResult.CurrentlyVending;

            return NpcInteractionResult.WaitForVendShop;
        }

        return NpcInteractionResult.EndInteraction;
    }
}


public class VendingProxyEventLoader : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("VendNpcProxy", new VendNpcProxy());
    }
}
