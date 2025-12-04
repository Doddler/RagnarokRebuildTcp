using RebuildSharedData.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Simulation.Util;
using RebuildSharedData.Enum;
using RoRebuildServer.EntitySystem;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;

namespace RoRebuildServer.Custom.OkolnirEvent;

public class EarthShakerSkillEvent : NpcBehaviorBase
{
    private const float ConeAngle = 15f;

    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.StartTimer();

        var primaryTarget = Entity.Invalid;
        if (npc.Owner.TryGet<CombatEntity>(out var owner))
        {
            if (owner.Character.Type == CharacterType.Monster)
                primaryTarget = owner.Character.Monster.Target;
            if (owner.Character.Type == CharacterType.Player)
                primaryTarget = owner.Player.Target;
        }

        var targets = EntityListPool.Get();
        using var potentialTargets = EntityListPool.Get();

        //npc.Character.Map?.GatherPlayersInArea(Area.CreateAroundPoint(npc.Character.Position, 14, 14), potentialTargets, true);
        owner.Character.Map?.GatherAllPlayersInViewDistance(owner.Character.Position, potentialTargets);
        for (var i = 0; i < potentialTargets.Count; i++)
        {
            if (!potentialTargets[i].TryGet<CombatEntity>(out var ce) || !ce.IsValidTarget(owner, false, true))
            {
                potentialTargets.SwapFromBack(i);
                i--;
            }
        }

        if (potentialTargets.Count <= 0)
            return;

        if (potentialTargets.Count > param1)
            potentialTargets.Remove(ref primaryTarget); //if we have more than the desired targets, we exclude the primary target

        if (potentialTargets.Count <= param1)
        {
            targets.CopyEntities(potentialTargets);
            for (var i = 0; i < targets.Count; i++)
                if (potentialTargets[i].TryGet<CombatEntity>(out var ce))
                    ce.AddStatusEffect(CharacterStatusEffect.SpecialTarget, 4000);
        }
        else
        {
            for (var i = 0; i < param1; i++)
            {
                //copy param1 random entities over into the target list
                var sel = GameRandom.Next(potentialTargets.Count);
                targets.Add(potentialTargets[sel]);

                if (potentialTargets[sel].TryGet<CombatEntity>(out var ce))
                    ce.AddStatusEffect(CharacterStatusEffect.SpecialTarget, 3500);

                potentialTargets.SwapFromBack(sel);
            }
        }

        if (targets.Count > 0)
        {
            var first = targets[0];
            owner.Character.LookAtEntity(ref first);
        }

        npc.TargetList = targets;

        npc.ValuesInt[0] = 4000;
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (npc.TargetList == null)
        {
            //we can't kill the event in the init phase cause it breaks things, so we kill it at first opportunity here
            npc.EndEvent();
            return;
        }

        if (newTime > npc.ValuesInt[0] / 1000f)
        {
            if (!npc.Owner.TryGet<CombatEntity>(out var owner) || owner.Character.Map == null)
            {
                npc.EndEvent();
                return;
            }

            using var allEntities = EntityListPool.Get();
            owner.Character.Map.GatherAllPlayersInViewDistance(owner.Character.Position, allEntities);

            foreach (var entity in npc.TargetList)
            {
                if (entity.TryGet<CombatEntity>(out var target))
                {
                    if (!target.CanBeTargeted(owner, false, true))
                        continue;

                    CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(owner.Character, target.Character.Position, CharacterSkill.EarthShaker, 1); //visual effect

                    var res = owner.CalculateCombatResult(target, 1.5f, 1, AttackFlags.Magical, CharacterSkill.EarthAttack, AttackElement.Earth);
                    res.AttackMotionTime = 0;
                    res.Time = Time.ElapsedTimeFloat + owner.Character.Position.DistanceTo(target.Character.Position) * 0.02f;
                    res.IsIndirect = true;

                    //CommandBuilder.SkillExecuteTargetedSkillAutoVis(owner.Character, target.Character, CharacterSkill.EarthShaker, 1, res);

                    owner.ExecuteCombatResult(res, false);
                    CommandBuilder.SkillExecuteIndirectAutoVisibility(owner.Character, target.Character, res); //damage to targeted player

                    var offset = target.Character.Position - owner.Character.Position;
                    var angle = (MathF.Atan2(offset.X, offset.Y) * MathHelper.Rad2Deg) % 360;

                    foreach (var e in allEntities)
                    {
                        if (e == entity)
                            continue;
                        if (!e.TryGet<CombatEntity>(out var potentialTarget))
                            continue;
                        if (!potentialTarget.IsValidTarget(owner, false, true))
                            continue;

                        var offset2 = potentialTarget.Character.Position - owner.Character.Position;
                        var angle2 = (MathF.Atan2(offset2.X, offset2.Y) * MathHelper.Rad2Deg) % 360;

                        var diff = 180 - MathF.Abs(MathF.Abs(angle - angle2) - 180);
                        if (diff < -ConeAngle || diff > ConeAngle)
                            continue;

                        res = owner.CalculateCombatResult(potentialTarget, 1.5f, 3, AttackFlags.Magical, CharacterSkill.EarthAttack, AttackElement.Earth);
                        res.AttackMotionTime = 0;
                        res.Time = Time.ElapsedTimeFloat + owner.Character.Position.DistanceTo(potentialTarget.Character.Position) * 0.02f;
                        res.IsIndirect = true;

                        owner.ExecuteCombatResult(res, false);
                        CommandBuilder.SkillExecuteIndirectAutoVisibility(owner.Character, potentialTarget.Character, res); //damage to player in cone
                    }
                }
            }

            npc.EndEvent();
        }
    }
}