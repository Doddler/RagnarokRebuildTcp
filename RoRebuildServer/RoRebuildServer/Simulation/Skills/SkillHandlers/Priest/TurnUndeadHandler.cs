using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.TurnUndead, SkillClass.Magic, SkillTarget.Enemy)]
public class TurnUndeadHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;

    public override int GetSkillRange(CombatEntity source, int lvl) => 9;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        var chInt = source.GetEffectiveStat(CharacterStat.Int);
        var chLuk = source.GetEffectiveStat(CharacterStat.Luck);
        var chLvl = source.GetStat(CharacterStat.Level);
        var enemyHp = target.GetStat(CharacterStat.Hp);
        var enemyMaxHp = target.GetStat(CharacterStat.MaxHp);

        var enemyPercent = enemyHp * 100 / enemyMaxHp;

        var chance = 2 * lvl + (chLuk + chInt + chLvl) / 10 + (100 - enemyPercent) / 5;

        var damage = enemyHp;

        if (GameRandom.Next(0, 100) > chance || target.GetSpecialType() == CharacterSpecialType.Boss)
        {
            var (min, max) = source.CalculateAttackPowerRange(true);
            var matk = GameRandom.Next(min, max);

            var baseHeal = 4 + 8 * lvl;
            var healValue = (chLvl + chInt) / 10 * baseHeal + matk / 2; //official has /8, but no matk.
            damage = healValue / 2 * lvl / 2;
        }

        if (!target.IsElementBaseType(CharacterElement.Undead1))
            damage = 1;

        var req = new AttackRequest(CharacterSkill.TurnUndead, 1f, 1, AttackFlags.Magical | AttackFlags.IgnoreDefense, AttackElement.Holy);
        req.MinAtk = damage;
        req.MaxAtk = damage;

        var res = source.CalculateCombatResultUsingSetAttackPower(target, req);
        source.ExecuteCombatResult(res, false);
        res.TimeInSeconds = 0.833f;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.TurnUndead, lvl, res);
    }
}