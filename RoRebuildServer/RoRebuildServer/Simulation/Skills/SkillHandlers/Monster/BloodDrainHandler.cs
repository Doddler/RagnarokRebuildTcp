using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.BloodDrain, SkillClass.Physical)]
public class BloodDrainHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 7;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        lvl = 1; //ignore all other levels, ti does nothing

        if (target == null || !target.IsValidTarget(source))
            return;

        var req = new AttackRequest(CharacterSkill.BloodDrain, 1f, 1, AttackFlags.Physical, AttackElement.Dark);
        var res = source.CalculateCombatResult(target, req);

        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.BloodDrain, lvl, res);

        if (res.Damage > 0)
        {
            var res2 = DamageInfo.SupportSkillResult(source.Entity, source.Entity, CharacterSkill.BloodDrain);
            res2.Damage = -res.Damage;
            res2.Time = Time.ElapsedTimeFloat + 0.8f;
            //res2.AttackMotionTime = res.AttackMotionTime + 1.2f;
            res2.Result = AttackResult.Heal;

            source.QueueDamage(res2);
        }
    }
}