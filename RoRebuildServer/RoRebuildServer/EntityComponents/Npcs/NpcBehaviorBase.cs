using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

namespace RoRebuildServer.EntityComponents.Npcs;

public abstract class NpcBehaviorBase
{
    public virtual void Init(Npc npc) { }

    public virtual void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString) { }

    public virtual NpcInteractionResult OnClick(Npc npc, Player player, NpcInteractionState state)
    {
        return NpcInteractionResult.EndInteraction;
    }

    public virtual NpcInteractionResult OnTouch(Npc npc, Player player, NpcInteractionState state)
    {
        return NpcInteractionResult.EndInteraction;
    }

    public virtual void OnCancel(Npc npc, Player player, NpcInteractionState state) { }

    public virtual void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe) { }
    public virtual void OnAoEEvent(Npc npc, CombatEntity target, AreaOfEffect aoe, Object? eventData) { }

    public virtual void OnLeaveAoE(Npc npc, CombatEntity target, AreaOfEffect aoe) { }

    public virtual void OnTimer(Npc npc, float lastTime, float newTime) { }

    public virtual bool CanBeAttacked(Npc npc, BattleNpc battleNpc, CombatEntity attacker, CharacterSkill skill = CharacterSkill.None) => false;
    public virtual void OnCalculateDamage(Npc npc, BattleNpc battleNpc, CombatEntity attacker, ref DamageInfo di) { }
    public virtual void OnApplyDamage(Npc npc, BattleNpc battleNpc, ref DamageInfo di) { }
    public virtual void CombatUpdate(Npc npc, BattleNpc battleNpc) { }

    public virtual NpcPathUpdateResult OnPath(Npc npc, NpcPathHandler path) => NpcPathUpdateResult.EndPath;

    public virtual void OnSignal(Npc npc, WorldObject src, string signal, int value1, int value2, int value3, int value4) { }
    public virtual int OnQuery(Npc npc, Npc srcNpc, string signal, int value1, int value2, int value3, int value4) => 0;

    public virtual void OnMobKill(Npc npc) { }

    public virtual EventOwnerDeathResult OnOwnerDeath(Npc npc, CombatEntity owner) => EventOwnerDeathResult.DetachEvent;

    public enum EventOwnerDeathResult
    {
        NoAction,
        RemoveEvent,
        DetachEvent
    }
}