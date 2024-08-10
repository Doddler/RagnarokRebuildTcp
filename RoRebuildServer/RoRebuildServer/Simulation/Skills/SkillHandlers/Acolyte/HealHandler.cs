using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[SkillHandler(CharacterSkill.Heal, SkillClass.Magic, SkillTarget.Ally)]
public class HealHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position)
    {
        if (target == null)
            return SkillValidationResult.InvalidTarget;
        if (source == target || source.IsValidAlly(target))
            return SkillValidationResult.Success;

        //TODO: Allow targeting undead enemies

        return SkillValidationResult.InvalidTarget;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
            return;

        if (isIndirect)
            throw new Exception($"Heal cannot (currently) be called without caster!");

        var healValue = lvl; //default to the level of the skill for monster skill shenanigans

        if (lvl <= 10)
        {
            var chLevel = source.GetStat(CharacterStat.Level);
            var statInt = source.GetStat(CharacterStat.Int);
            var matk = GameRandom.Next(source.GetStat(CharacterStat.MagicAtkMin),
                source.GetStat(CharacterStat.MagicAtkMax));

            healValue = (chLevel + statInt) / 5 * lvl * 3 + matk;

            healValue = healValue * 5 / 2; //this isn't normally part of the formula
        }

        var res = source.PrepareTargetedSkillResult(target, CharacterSkill.Heal);
        res.Damage = -healValue;
        res.Result = AttackResult.Heal;
        res.HitCount = 0;

        target.HealHp(healValue);

        var ch = source.Character;
        ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target.Character, CharacterSkill.Heal, lvl, res);
        CommandBuilder.SendHealMulti(target.Character, healValue, HealType.None);
        CommandBuilder.ClearRecipients();

        source.ApplyCooldownForAttackAction();
    }
}