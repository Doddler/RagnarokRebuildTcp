using Microsoft.Extensions.ObjectPool;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildZoneServer.Networking;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents.Util;

public class AreaOfEffectPoolPolicy : IPooledObjectPolicy<AreaOfEffect>
{
    public AreaOfEffect Create()
    {
        return new AreaOfEffect();
    }

    public bool Return(AreaOfEffect obj)
    {
        obj.Reset();
        return true;
    }
}

public class AreaOfEffect
{
    public Entity SourceEntity;
    public Area Area;

    public TargetingInfo TargetingInfo;
    public AoeType Type;

    public float NextTick;
    public float Expiration;

    public float TickRate;

    public int Value1;
    public int Value2;

    public bool IsActive;

    public AreaOfEffect()
    {
        SourceEntity = Entity.Null;
        Area = Area.Zero;
        Type = AoeType.Inactive;
        TickRate = 99999;
        Value1 = 0;
        Value2 = 0;
        TargetingInfo = new TargetingInfo();
        Expiration = float.MaxValue;
        NextTick = float.MaxValue;
        IsActive = false;
    }
    
    public void Init(Entity sourceEntity, Area area, AoeType type, TargetingInfo targetingInfo, float duration, float tickRate, int value1, int value2)
    {
        SourceEntity = sourceEntity;
        Area = area;
        Type = type;
        TickRate = tickRate;
        Value1 = value1;
        Value2 = value2;
        TargetingInfo = targetingInfo;
        Expiration = Time.ElapsedTimeFloat + duration;
        NextTick = Time.ElapsedTimeFloat + tickRate;
        IsActive = true;
    }

    public void Reset()
    {
        SourceEntity = Entity.Null;
        IsActive = false;
    }

    //HasTouchedAoE checks if we are entering an aoe we were not previously in. If we are already in the aoe nothing happens.
    public bool HasTouchedAoE(Position initial, Position newPos)
    {
        if (Area.Contains(initial))
            return false;
        
        return Area.Contains(newPos);
    }

    public void OnAoETouch(WorldObject character)
    {
        if (character.Type == CharacterType.Player && Type == AoeType.NpcTouch)
        {
            if (SourceEntity.IsAlive() && SourceEntity.Type == EntityType.Npc)
            {
                SourceEntity.Get<Npc>().OnTouch(character.Entity.Get<Player>());
            }
        }
    }
    
    public void Update(Map map)
    {
        if (Expiration < 0 || NextTick < 0)
            return;

        if (Time.ElapsedTimeFloat < NextTick)
            return;

        if (Time.ElapsedTimeFloat > Expiration)
            return;

        NextTick += TickRate;
    }
}