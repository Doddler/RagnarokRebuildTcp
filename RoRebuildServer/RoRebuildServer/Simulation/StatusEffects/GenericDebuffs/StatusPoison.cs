using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs
{
    [StatusEffectHandler(CharacterStatusEffect.Poison, StatusClientVisibility.Everyone)]
    public class StatusPoison : StatusEffectBase
    {
        //val1 = attacker id (so they get credited for the damage)
        //val2 = attack power snapshot
        //val3 = vit penalty
        //val4 = counter so we execute only ever 2s on monsters and 3s vs players
        
        public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnUpdate;
        public override StatusUpdateResult OnUpdateTick(CombatEntity ch, ref StatusEffectState state)
        {
            state.Value4--;
            if (state.Value4 > 0)
                return StatusUpdateResult.Continue; //only do this every 3 seconds
            state.Value4 = (byte)(ch.Character.Type == CharacterType.Player ? 3 : 2);

            if (ch.Character.Type == CharacterType.Monster && (ch.HpPercent < 20 || ch.GetSpecialType() == CharacterSpecialType.Boss) && ch.Character.Monster.TimeSinceLastDamage > 3f)
                return StatusUpdateResult.Continue;

            var attackerEntity = World.Instance.GetEntityById(state.Value1);
            if (!attackerEntity.TryGet<CombatEntity>(out var attacker))
                attacker = ch;

            var damage = GameRandom.Next(state.Value2 * 90 / 100, state.Value2 * 110 / 100);
            if (ch.Character.Type == CharacterType.Player)
            {
                var remainingHp = ch.GetStat(CharacterStat.Hp);
                if (damage > remainingHp)
                    damage = remainingHp - 1; //players drop down to 1hp but don't die
            }
            
            if (damage <= 0)
                return StatusUpdateResult.Continue;
            
            
            var di = new DamageInfo()
            {
                Damage = damage,
                Result = AttackResult.NormalDamage,
                KnockBack = 0,
                Source = attacker.Entity,
                Target = ch.Entity,
                AttackSkill = CharacterSkill.NoCast,
                HitCount = 1,
                Time = 0,
                AttackMotionTime = 0,
                AttackPosition = ch.Character.Position,
                Flags = DamageApplicationFlags.NoHitLock | DamageApplicationFlags.SkipOnHitTriggers | DamageApplicationFlags.PhysicalDamage
            };
            
            ch.ExecuteCombatResult(di, false, false);

            ch.Character.Map?.AddVisiblePlayersAsPacketRecipients(ch.Character);
            CommandBuilder.AttackMulti(null, ch.Character, di, false); //make the client see no attacker
            CommandBuilder.ClearRecipients();

            return StatusUpdateResult.Continue;
        }

        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            var negativeVit = ch.GetStat(CharacterStat.Vit) / 4;
            var defPenalty = -25;
            //bosses and players only lose 10% def, normal monsters 25%
            if (ch.Character.Type == CharacterType.Player || ch.GetSpecialType() == CharacterSpecialType.Boss)
                defPenalty = -10;

            state.Value3 = (short)-negativeVit;
            state.Value4 = (byte)(ch.Character.Type == CharacterType.Player ? 3 : 2); //3s tick time on players, 2s on monsters
            ch.AddStat(CharacterStat.AddVit, state.Value3);
            ch.AddStat(CharacterStat.AddDefPercent, defPenalty);
            ch.AddStat(CharacterStat.AddSpRecoveryPercent, -999);
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            var defPenalty = (ch.Character.Type == CharacterType.Player || ch.GetSpecialType() == CharacterSpecialType.Boss) ? -10 : -25;

            ch.SubStat(CharacterStat.AddVit, state.Value3);
            ch.SubStat(CharacterStat.AddDefPercent, defPenalty);
            ch.SubStat(CharacterStat.AddSpRecoveryPercent, -999);
        }
    }
}
