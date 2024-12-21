using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using System;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief
{
    [SkillHandler(CharacterSkill.Envenom, SkillClass.Physical, SkillTarget.Enemy)]
    public class EnvenomHandler : SkillHandlerBase
    {
        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            if (target == null)
                return;

            var flags = AttackFlags.Physical;
            if (source.Character.Type == CharacterType.Player)
                flags |= AttackFlags.CanCrit;

            var res = source.CalculateCombatResult(target, 1f + lvl * 0.05f, 1, flags, CharacterSkill.Envenom, AttackElement.Poison);
            source.ApplyCooldownForAttackAction(target);
            source.ExecuteCombatResult(res, false);

            var ch = source.Character;

            CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.Envenom, lvl, res);

            if (!res.IsDamageResult)
                return;

            var race = target.GetRace();
            if (race == CharacterRace.Undead)
                return;

            var chance = 500 + lvl * 50; //50%-100%
            var resist = MathHelper.PowScaleDown(target.GetEffectiveStat(CharacterStat.Vit)); //1% resist per vit, stacking
            if (!source.CheckLuckModifiedRandomChanceVsTarget(target, (int)(chance * resist), 1000))
                return; //failed to poison

            var duration = lvl switch
            {
                < 3 => 12f, //4 ticks
                < 6 => 15f, //5 ticks
                < 9 => 18f, //6 ticks
                _ => 21f //7 ticks
            };

            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Poison, duration + 2, source.Character.Id, res.Damage / 2);
            target.AddStatusEffect(status, true, res.AttackMotionTime);
        }
    }
}
