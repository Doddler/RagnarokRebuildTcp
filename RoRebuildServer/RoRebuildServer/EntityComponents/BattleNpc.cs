using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.BattleNpc)]
public class BattleNpc : IEntityAutoReset
{
    public Entity Entity;
    public WorldObject Character { get; set; }
    public Npc Npc { get; set; }
    public CombatEntity CombatEntity { get; set; }

    public void Reset()
    {
        Entity = Entity.Invalid;
        Character = null!;
        Npc = null!;
        CombatEntity = null!;
    }

    public bool CanBeTargeted(CombatEntity attacker, CharacterSkill skill)
    {
        return Npc.Behavior.CanBeAttacked(Npc, this, attacker, skill);
    }

    public void OnNotifyAttack()
    {

    }

    public void Update()
    {
        Npc.Behavior.CombatUpdate(Npc, this);
    }
}