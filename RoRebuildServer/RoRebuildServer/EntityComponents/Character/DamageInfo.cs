using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.EntityComponents.Character;

public class DamageInfo
{
    public Entity Source;
    public Entity Target;
    public float Time;
    public float AttackMotionTime;
    public short Damage;
    public byte HitCount;
    public byte KnockBack;
    public AttackResult Result;
}