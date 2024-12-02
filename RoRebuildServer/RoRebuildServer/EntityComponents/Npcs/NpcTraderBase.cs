using System.Resources;
using RebuildSharedData.Enum;

namespace RoRebuildServer.EntityComponents.Npcs
{
    public class NpcTraderBase : NpcBehaviorBase
    {
        public override NpcInteractionResult OnClick(Npc npc, Player player, NpcInteractionState state)
        {
            switch (state.Step)
            {
                case 0:
                    state.FocusNpc();
                    state.Option("Buy", "Sell", "Cancel");
                    state.Step = 1;
                    return NpcInteractionResult.WaitForInput;
                case 1:
                    state.Step = 10;
                    if(state.OptionResult == 0)
                        state.OpenShop();
                    if (state.OptionResult == 1)
                        state.StartSellToNpc();
                    if (state.OptionResult > 1)
                        goto case 10;
                    return NpcInteractionResult.WaitForShop;
                case 10:
                    return NpcInteractionResult.EndInteraction;
            }

            return NpcInteractionResult.EndInteraction;
        }
    }
}
