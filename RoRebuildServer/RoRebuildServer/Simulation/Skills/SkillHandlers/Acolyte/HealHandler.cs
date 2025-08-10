using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[SkillHandler(CharacterSkill.Heal, SkillClass.Magic, SkillTarget.Any)]
public class HealHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null)
            return SkillValidationResult.InvalidTarget;
        if (source == target || source.IsValidAlly(target))
            return StandardValidation(source, target, position);

        if (target.Character.Type == CharacterType.Monster)
        {
            if (target.Character.Type == CharacterType.Monster && target.IsElementBaseType(CharacterElement.Undead1))
                return StandardValidation(source, target, position);
        }

        if (target.Character.Type == CharacterType.Player && target.IsElementBaseType(CharacterElement.Undead1))
            return SkillValidationResult.Failure;

        return SkillValidationResult.InvalidTarget;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null || target.Character.State == CharacterState.Dead)
            return;

        var ch = source.Character;
        var healValue = lvl; //default to the level of the skill for monster skill shenanigans

        if (lvl <= 10)
        {
            var chLevel = source.GetStat(CharacterStat.Level);
            var statInt = source.GetEffectiveStat(CharacterStat.Int);
            var (min, max) = source.CalculateAttackPowerRange(true);
            var matk = GameRandom.Next(min, max);

            var baseHeal = 4 + 8 * lvl;
            healValue = (chLevel + statInt) / 10 * baseHeal + matk / 2; //official has /8, but no matk.

        }

        if (source.Character.Type == CharacterType.Player && target.Character.Type == CharacterType.Monster)
        {
            var monBase = target.Character.Monster.MonsterBase;
            var element = target.GetElement();
            if (element.IsElementBaseType(CharacterElement.Undead1))
            {
                var res = source.PrepareTargetedSkillResult(target, CharacterSkill.Heal);
                var mod = DataManager.ElementChart.GetAttackModifier(AttackElement.Holy, element);
                res.Damage = (healValue / 2) * mod / 100;
                res.HitCount = 1;
                res.AttackMotionTime = 0.5f;
                res.Result = AttackResult.NormalDamage;

                if (!isIndirect)
                {
                    source.ApplyAfterCastDelay(1f, ref res);
                    source.ApplyCooldownForAttackAction(target);
                }

                target.ExecuteCombatResult(res, false, false);

                ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
                CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.Heal, lvl, res);
                CommandBuilder.SendHealMulti(target.Character, healValue, HealType.None);
                CommandBuilder.ClearRecipients();
            }
        }
        else
        {
            var res = source.PrepareTargetedSkillResult(target, CharacterSkill.Heal);
            res.Damage = -healValue;
            res.Result = AttackResult.Heal;
            res.HitCount = 0;
            res.AttackMotionTime = 0;
            res.IsIndirect = isIndirect;
            res.Time = Time.ElapsedTimeFloat;

            target.HealHp(healValue);
            if (!isIndirect)
            {
                source.ApplyAfterCastDelay(1f);

                if (source.Character.Type == CharacterType.Player)
                    source.ApplyCooldownForSupportSkillAction();
                else
                    source.ApplyCooldownForAttackAction();
            }

            ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.Heal, lvl, res);
            CommandBuilder.SendHealMulti(target.Character, healValue, HealType.None);
            CommandBuilder.ClearRecipients();
        }
    }

}