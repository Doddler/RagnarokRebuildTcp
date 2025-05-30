﻿using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage
{
    public abstract class TargetedSpellBase : SkillHandlerBase
    {
        public abstract CharacterSkill GetSkill();
        public abstract AttackElement GetElement();
        
        public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
        {
            //return 0f;

            if (lvl < 0 || lvl > 10)
                lvl = 10;

            return 0.6f + lvl * 0.4f;
        }

        public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
        {
            if (lvl < 0 || lvl > 10)
                lvl = 10;

            if (target == null || !target.IsValidTarget(source))
                return;

            var res = source.CalculateCombatResult(target, 1, lvl, AttackFlags.Magical, GetSkill(), GetElement());
            res.IsIndirect = isIndirect;

            if (!isIndirect)
            {
                source.ApplyAfterCastDelay(1f, ref res);
                source.ApplyCooldownForAttackAction(target);
            }

            source.ExecuteCombatResult(res, false);
            res.Time = Time.ElapsedTimeFloat + 0.6f;

            var ch = source.Character;

            CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, GetSkill(), lvl, res);
        }
    }
}
