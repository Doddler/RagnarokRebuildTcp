using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.PhysBreaker, SkillClass.Magic)]
public class PhysBreakerHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null || !target.IsValidTarget(source))
            return;

        DamageInfo res;

        if (GameRandom.Next(0, 100) < 50)
        {
            int damage;
            if (target.Character.Type == CharacterType.Player)
            {
                var currentHp = target.GetStat(CharacterStat.Hp);
                var percent = lvl switch
                {
                    1 => 10,
                    2 => 12,
                    3 => 16,
                    4 => 25,
                    _ => 50
                };

                damage = currentHp * percent / 100;
            }
            else
                damage = GameRandom.Next(1, 9999);

            var req = new AttackRequest()
            {
                MinAtk = damage,
                MaxAtk = damage,
                Flags = AttackFlags.Magical | AttackFlags.IgnoreDefense | AttackFlags.IgnoreEvasion | AttackFlags.NoDamageModifiers | AttackFlags.NoElement | AttackFlags.NoTriggers,
                HitCount = 1,
                AttackMultiplier = 1,
                SkillSource = CharacterSkill.PhysBreaker
            };
            res = source.CalculateCombatResultUsingSetAttackPower(target, req);
            res.Time = Time.ElapsedTimeFloat + 0.65f;

            source.ExecuteCombatResult(res, false);
        }
        else
        {
            res = source.PrepareTargetedSkillResult(target, CharacterSkill.PhysBreaker);
            res.SetAttackToMiss();
            res.HitCount = 1;
            res.Time = Time.ElapsedTimeFloat + 0.65f;
        }



        source.ApplyCooldownForAttackAction(target);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.PhysBreaker, lvl, res);
    }
}