using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects._1stJob
{
    [StatusEffectHandler(CharacterStatusEffect.Blessing, StatusClientVisibility.Everyone)]
    public class StatusBlessing : StatusEffectBase
    {
        public override void OnApply(CombatEntity ch, ref StatusEffectState state)
        {
            if (ch.Character.Type == CharacterType.Monster && (ch.IsElementBaseType(CharacterElement.Undead1) ||
                                                               ch.GetRace() == CharacterRace.Demon))
                state.Value4 = 1;

            if (ch.GetSpecialType() == CharacterSpecialType.Boss)
                return;

            if(state.Value4 > 0)
            {
                state.Value1 = ch.GetEffectiveStat(CharacterStat.Dex) / 2;
                state.Value2 = ch.GetEffectiveStat(CharacterStat.Int) / 2;
                state.Value3 = (short)(ch.GetEffectiveStat(CharacterStat.Str) / 2);

                ch.AddStat(CharacterStat.AddDex, -state.Value1);
                ch.AddStat(CharacterStat.AddInt, -state.Value2);
                ch.AddStat(CharacterStat.AddStr, -state.Value3);
            }
            else
            {
                ch.AddStat(CharacterStat.AddStr, state.Value1);
                ch.AddStat(CharacterStat.AddInt, state.Value1);
                ch.AddStat(CharacterStat.AddDex, state.Value1);
            }
        }

        public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
        {
            if (ch.GetSpecialType() == CharacterSpecialType.Boss)
                return;

            if (state.Value4 > 0)
            {
                ch.SubStat(CharacterStat.AddDex, -state.Value1);
                ch.SubStat(CharacterStat.AddInt, -state.Value2);
                ch.SubStat(CharacterStat.AddStr, -state.Value3);
            }
            else
            {
                ch.SubStat(CharacterStat.AddStr, state.Value1);
                ch.SubStat(CharacterStat.AddInt, state.Value1);
                ch.SubStat(CharacterStat.AddDex, state.Value1);
            }
        }
    }
}
