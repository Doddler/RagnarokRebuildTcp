using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects
{
    [StatusEffectHandler(CharacterStatusEffect.DarkBlessing, StatusClientVisibility.Owner)]
    public class DarkBlessingStatus : StatusEffectBase
    {
        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            if (ch.Character.Type != CharacterType.Player || ch.Character.State == CharacterState.Dead)
                return;

            var curHp = ch.GetStat(CharacterStat.Hp);

            var attackerEntity = World.Instance.GetEntityById(state.Value1);
            if (!attackerEntity.TryGet<CombatEntity>(out var attacker))
                attacker = ch;

            var di = new DamageInfo()
            {
                Damage = curHp - 1,
                Result = AttackResult.NormalDamage,
                KnockBack = 0,
                Source = attacker.Entity,
                Target = ch.Entity,
                AttackSkill = CharacterSkill.NoCast,
                HitCount = 1,
                Time = 0,
                AttackMotionTime = 0,
                AttackPosition = ch.Character.Position,
                Flags = DamageApplicationFlags.NoHitLock | DamageApplicationFlags.SkipOnHitTriggers
            };

            ch.ClearDamageQueue(); //no attack queued prior to this effect matters now
            ch.ExecuteCombatResult(di, false, false);
            
            ch.Character.Map?.AddVisiblePlayersAsPacketRecipients(ch.Character);
            CommandBuilder.AttackMulti(null, ch.Character, di, false); //make the client see no attacker
            CommandBuilder.ClearRecipients();
        }
    }
}
