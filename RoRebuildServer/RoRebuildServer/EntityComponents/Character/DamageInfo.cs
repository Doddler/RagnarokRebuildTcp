using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.EntityComponents.Character;

[Flags]
public enum DamageApplicationFlags : byte
{
    None = 0,
    NoHitLock = 1,
    ReducedHitLock = 2,
    UpdatePosition = 4,
    SkipOnHitTriggers = 8
}

public struct DamageInfo
{
    public Entity Source;
    public Entity Target;
    public Position AttackPosition;
    public float Time;
    public float AttackMotionTime;
    public int Damage;
    public byte HitCount;
    public byte KnockBack;
    public AttackResult Result;
    private byte skillId;
    public DamageApplicationFlags Flags;
    public bool IsIndirect;

    public bool IsDamageResult => Result == AttackResult.NormalDamage || Result == AttackResult.CriticalDamage;

    public CharacterSkill AttackSkill
    {
        get => (CharacterSkill)skillId;
        set => skillId = (byte)value;
    }

    public void SetAttackToMiss()
    {
        Result = AttackResult.Miss;
        Damage = 0;
        HitCount = 0;
        KnockBack = 0;
        Flags = DamageApplicationFlags.NoHitLock;
    }
    

    public static DamageInfo SupportSkillResult(Entity src, Entity target, CharacterSkill skill)
    {
        return new DamageInfo()
        {
            Source = src,
            Target = target,
            AttackMotionTime = 0,
            Damage = 0,
            HitCount = 0,
            KnockBack = 0,
            skillId = (byte)skill,
            Result = AttackResult.Invisible,
            Flags = DamageApplicationFlags.NoHitLock
        };
    }

    public static DamageInfo EmptyResult(Entity src, Entity target)
    {
        return new DamageInfo()
        {
            Source = src,
            Target = target,
            AttackMotionTime = 0,
            Damage = 0,
            HitCount = 0,
            KnockBack = 0,
            skillId = 0,
            Result = AttackResult.Invisible,
            Flags = DamageApplicationFlags.NoHitLock
        };
    }
}