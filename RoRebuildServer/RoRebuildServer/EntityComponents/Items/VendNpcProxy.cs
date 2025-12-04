using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Simulation.Pathfinding;

namespace RoRebuildServer.EntityComponents.Items;

public class VendNpcProxy : NpcBehaviorBase
{
    public override void Init(Npc npc)
    {
        npc.StartTimer(1000);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (!npc.Owner.TryGet<Player>(out var owner))
        {
            npc.EndEvent();
            return;
        }

        if (!npc.Character.IsPlayerVisible(npc.Owner) || npc.Character.Position.DistanceTo(owner.Character.Position) > 1)
        {
            npc.EndEvent();
            return;
        }

        if (owner.Character.State == CharacterState.Dead && !npc.Character.AdminHidden)
            npc.HideNpc();

        if (owner.Character.State != CharacterState.Dead && npc.Character.AdminHidden)
            npc.ShowNpc();
    }

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