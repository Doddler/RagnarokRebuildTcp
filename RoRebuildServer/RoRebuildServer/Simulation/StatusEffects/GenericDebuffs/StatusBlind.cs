using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.GenericDebuffs
{
    [StatusEffectHandler(CharacterStatusEffect.Blind, StatusClientVisibility.Everyone)]
    public class StatusBlind : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            var fleeDown = ch.GetEffectiveStat(CharacterStat.Agi) / 4;
            var hitDown = ch.GetEffectiveStat(CharacterStat.Dex) / 4;

            state.Value1 = fleeDown;
            state.Value2 = hitDown;

            ch.SubStat(CharacterStat.AddFlee, state.Value1);
            ch.SubStat(CharacterStat.AddHit, state.Value2);

            if (ch.Character.Type == CharacterType.Monster && ch.GetSpecialType() != CharacterSpecialType.Boss)
            {
                var m = ch.Character.Monster;
                m.ChaseSight = 1;
                m.AttackSight = 1;
                ch.SetBodyState(BodyStateFlags.Blind); //bosses don't get the blind body state, their skill ranges aren't negatively affected
            }

            if (ch.Character.Type == CharacterType.Player)
            {
                if (ch.GetStat(CharacterStat.Range) > 5) //the enemy will be out of attack range
                    ch.Player.ClearTarget();
                ch.SetBodyState(BodyStateFlags.Blind);
            }
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            ch.AddStat(CharacterStat.AddFlee, state.Value1);
            ch.AddStat(CharacterStat.AddHit, state.Value2);
            ch.RemoveBodyState(BodyStateFlags.Blind);

            if (ch.Character.Type == CharacterType.Monster)
            {
                var m = ch.Character.Monster;
                m.ChaseSight = m.MonsterBase.ChaseDist;
                m.AttackSight = m.MonsterBase.ScanDist;
            }
        }
    }
}