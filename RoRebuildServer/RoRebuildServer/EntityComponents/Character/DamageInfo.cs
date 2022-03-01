using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.EntityComponents.Character;

public class DamageInfo
{
    public Entity Source;
    public Entity Target;
    public float Time;
    public short Damage;
    public byte HitCount;
    public byte KnockBack;
}