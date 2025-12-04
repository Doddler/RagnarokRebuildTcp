using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[SkillHandler(CharacterSkill.JupitelThunder, SkillClass.Magic)]
public class JupitelThunderHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        //return 0f;

        if (lvl < 0 || lvl > 10)
            lvl = 10;

        return 2f + lvl * 0.5f;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var hitCount = lvl + 2;
        var knockBack = int.Clamp(hitCount, 3, 12);

        if (target == null || !target.IsValidTarget(source))
            return;

        var res = source.CalculateCombatResult(target, 1, hitCount, AttackFlags.Magical, CharacterSkill.JupitelThunder, AttackElement.Wind);
        res.KnockBack = (byte)knockBack;

        var dist = source.Character.WorldPosition.DistanceTo(target.Character.WorldPosition);
        var distTime = dist * 0.025f;
        res.Time = Time.ElapsedTimeFloat + res.AttackMotionTime * 0.7f + distTime;

        if (!isIndirect)
            source.ApplyCooldownForAttackAction(target);

        var ch = source.Character;

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.JupitelThunder, lvl, res);

        if (lvl <= 10 || source.Character.Type == CharacterType.Player)
            source.ExecuteCombatResult(res, false);
        else
        {
            //boss jupitel thunder frontloads half the damage into the first hit, then the rest occurs at normal damage intervals
            var perHitDamage = (res.Damage * hitCount / 2) / hitCount;
            res.HitCount = 1;
            res.Damage = (res.Damage * hitCount) - (perHitDamage * hitCount);
            var time = res.Time;
            source.ExecuteCombatResult(res, false);
            res.KnockBack = 0;
            res.Damage = perHitDamage;
            res.Flags |= DamageApplicationFlags.ReducedHitLock;

            for (var i = 1; i < hitCount; i++)
            {
                res.Time = time + i * 0.2f;
                source.ExecuteCombatResult(res, false);
            }
        }
    }
}