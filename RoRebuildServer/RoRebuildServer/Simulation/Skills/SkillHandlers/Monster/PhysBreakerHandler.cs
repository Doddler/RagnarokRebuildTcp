using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
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

        var di = source.PrepareTargetedSkillResult(target, CharacterSkill.PhysBreaker);
        di.HitCount = 1;
        di.Time = Time.ElapsedTimeFloat + 1f;

        if (GameRandom.Next(0, 100) < 50)
        {
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

                di.Damage = currentHp * percent / 100;
            }
            else
                di.Damage = GameRandom.Next(1, 9999);

            source.ExecuteCombatResult(di, false);
        }
        else
            di.Result = AttackResult.Miss;

        source.ApplyCooldownForAttackAction(target);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.PhysBreaker, lvl, di);
    }
}