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
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position)
    {
        if (target == null)
            return SkillValidationResult.InvalidTarget;
        if (source == target || source.IsValidAlly(target))
            return SkillValidationResult.Success;

        //TODO: Allow targeting undead enemies

        if (target.Character.Type == CharacterType.Monster)
        {
            if (target.Character.Type == CharacterType.Monster && target.IsElementBaseType(CharacterElement.Undead1))
                return SkillValidationResult.Success;
        }

        return SkillValidationResult.InvalidTarget;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null || target.Character.State == CharacterState.Dead)
            return;

        if (isIndirect)
            throw new Exception($"Heal cannot (currently) be called without caster!");

        var ch = source.Character;
        var healValue = lvl; //default to the level of the skill for monster skill shenanigans

        if (lvl <= 10)
        {
            var chLevel = source.GetStat(CharacterStat.Level);
            var statInt = source.GetEffectiveStat(CharacterStat.Int);
            var matk = GameRandom.Next(source.GetStat(CharacterStat.MagicAtkMin),
                source.GetStat(CharacterStat.MagicAtkMax));

            healValue = (chLevel + statInt) / 5 * lvl * 3 + matk;

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
                res.Result = AttackResult.NormalDamage;

                source.ApplyCooldownForAttackAction(target);
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

            target.HealHp(healValue);
            if (source.Character.Type == CharacterType.Player)
                source.ApplyCooldownForSupportSkillAction();
            else
                source.ApplyCooldownForAttackAction();

            ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
            CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.Heal, lvl, res);
            CommandBuilder.SendHealMulti(target.Character, healValue, HealType.None);
            CommandBuilder.ClearRecipients();
        }
    }

}