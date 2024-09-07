using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Util;

namespace RoRebuildServer.EntityComponents.Npcs;

public abstract class NpcBehaviorBase
{
    public virtual void Init(Npc npc) {}

    public virtual void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString) { }

    public virtual NpcInteractionResult OnClick(Npc npc, Player player, NpcInteractionState state)
    {
        return NpcInteractionResult.EndInteraction;
    }


    public virtual NpcInteractionResult OnTouch(Npc npc, Player player, NpcInteractionState state)
    {
        return NpcInteractionResult.EndInteraction;
    }


    public virtual void OnCancel(Npc npc, Player player, NpcInteractionState state)
    {
        
    }

    public virtual void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe) { }

    public virtual void OnTimer(Npc npc, float lastTime, float newTime)
    {

    }

    public virtual void OnSignal(Npc npc, Npc srcNpc, string signal, int value1, int value2, int value3, int value4)
    {

    }

    public virtual void OnMobKill(Npc npc)
    {

    }
}