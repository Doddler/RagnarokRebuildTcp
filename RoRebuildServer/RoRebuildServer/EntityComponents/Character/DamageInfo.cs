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
    SkipOnHitTriggers = 8,
    PhysicalDamage = 16,
    MagicalDamage = 32
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

    public int DisplayDamage
    {
        get
        {
            if (Target.TryGet<CombatEntity>(out var target) && target.HasBodyState(BodyStateFlags.Hallucination))
            {
                var adjust = GameRandom.NextInclusive(1, 10) * GameRandom.NextInclusive(1, 10);
                if (adjust == 1)
                    return GameRandom.Next(1, 32000);
                var d = Damage > 0 ? Damage : GameRandom.Next(1, 100);
                var min = int.Clamp(d / adjust, 1, d);
                var max = int.Clamp(d * adjust, d, 32000);
                return GameRandom.Next(min, max);
            }

            return Damage;
        }
    }

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