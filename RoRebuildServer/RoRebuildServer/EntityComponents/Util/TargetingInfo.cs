using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.EntityComponents.Util;

[Flags]
public enum TargetingType : byte
{
    None = 0,
    Player = 1,
    Party = 2,
    Faction = 4,
    Enemies = 8
}


public struct TargetingInfo
{
    public Entity SourceEntity;
    public int Faction;
    public int Party;
    public TargetingType TargetingType;
    public bool IsPvp;

    public TargetingInfo()
    {
        SourceEntity = Entity.Null;
        Faction = 0;
        Party = 0;
        TargetingType = TargetingType.None;
        IsPvp = false;
    }

    public void Init(ref Entity sourceEntity, int faction, int party, TargetingType targetingType, bool isPvp)
    {
        SourceEntity = sourceEntity;
        Faction = faction;
        Party = party;
        TargetingType = targetingType;
        IsPvp = isPvp;
    }

    public void Reset()
    {
        SourceEntity = Entity.Null;
        TargetingType = TargetingType.None;
    }

    public bool IsValidTarget(CombatEntity target)
    {
        if ((TargetingType & TargetingType.Player) != 0)
        {
            if (target.Entity.Type == EntityType.Player)
                return true;
        }

        if ((TargetingType & TargetingType.Party) != 0)
        {
            if (target.Party > 0 && SourceEntity.Type == target.Entity.Type)
                return target.Party == Party;
        }

        if ((TargetingType & TargetingType.Faction) != 0)
        {
            return target.Faction == Faction;
        }

        if ((TargetingType & TargetingType.Enemies) != 0)
        {
            if (IsPvp && target.Party > 0 && Party > 0 && target.Party != Party)
                return true;

            if (target.Faction != Faction)
                return true;
        }

        return false;
    }
}