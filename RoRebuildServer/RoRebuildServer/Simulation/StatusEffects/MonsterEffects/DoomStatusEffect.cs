using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using static System.Net.Mime.MediaTypeNames;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.Doom, StatusClientVisibility.None)]
public class DoomStatusEffect : StatusEffectBase
{
    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        if (ch.Character.Type == CharacterType.Monster)
            ch.Character.Monster.GivesExperience = false;
        var di = new DamageInfo()
        {
            Damage = 999_999_999,
            Result = AttackResult.NormalDamage,
            KnockBack = 0,
            Source = ch.Entity,
            Target = ch.Entity,
            AttackSkill = CharacterSkill.NoCast,
            HitCount = 1,
            Time = 0,
            AttackMotionTime = 0,
            AttackPosition = ch.Character.Position,
            Flags = DamageApplicationFlags.NoHitLock
        };

        ch.ExecuteCombatResult(di, false, false);
    }
}