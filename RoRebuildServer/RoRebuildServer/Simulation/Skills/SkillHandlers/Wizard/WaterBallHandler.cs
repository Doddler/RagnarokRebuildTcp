using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[SkillHandler(CharacterSkill.WaterBall, SkillClass.Magic, SkillTarget.Enemy)]
public class WaterBallHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null || !target.IsValidTarget(source))
            return;

        var radius = lvl switch
        {
            <= 1 => 0,
            <= 3 => 1,
            _ => 2
        };

        if (source.Character.Type == CharacterType.Monster && lvl == 10)
            radius = 3;

        using var list = EntityListPool.Get();
        var map = source.Character.Map!;
        var ch = source.Character;
        var targetId = target.Character.Id;
        var srcPos = source.Character.Position;

        var ratio = 100 + lvl * 30;
        var requireWater = false; //set this to true to make players sad

        //the actual cast of water ball that we send to the client just shows the attack motion and doesn't deal damage.
        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.WaterBall);
        res.Result = AttackResult.Invisible;
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.WaterBall, lvl, res);

        //Initial ball is always under the player, we ensure the center tile is the first to fire. Presently, the center ball always succeeds even without water.
        var e0 = World.Instance.CreateEvent(source.Entity, map, "WaterBallEvent", source.Character.Position, targetId, ratio, 0, 0, null);
        ch.AttachEvent(e0);
        list.Add(ref e0);

        //create the rest of the balls
        if (radius > 0)
        {
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    var pos = new Position(srcPos.X + x, srcPos.Y + y);

                    if (!map.WalkData.IsCellWalkable(pos))
                        continue;

                    if (requireWater && !map.WalkData.IsCellInWater(pos))
                        continue;

                    var e = World.Instance.CreateEvent(source.Entity, map, "WaterBallEvent", pos, targetId, ratio, 0, 0, null);
                    ch.AttachEvent(e);

                    list.Add(ref e);
                }
            }
        }

        //now that we have all the balls that will be firing, we can fill in their reveal and fire times to activate in a random order
        if (list.Count > 0)
        {
            list.Shuffle(1); //we don't shuffle the first entry, the player tile always fires first

            var mt = 0.667f; //res.AttackMotionTime;

            foreach (var e in list)
            {
                var obj = e.Get<Npc>();
                obj.ValuesInt[0] = targetId;
                obj.ValuesInt[1] = ratio;
                obj.ValuesInt[3] = (int)(mt * 1000); //fire time
                obj.ValuesInt[4] = 0;

                if (mt < 0.6f)
                    obj.ValuesInt[2] = 0; //reveal time
                else
                {
                    var revealTime = GameRandom.NextFloat(0, float.Min(mt - 0.667f, 1.4f));
                    obj.ValuesInt[2] = (int)(revealTime * 1000); //reveal time
                }

                obj.ResetTimer();
                obj.StartTimer(50);
                mt += 0.15f;
            }
        }
    }
}

//Each water ball ground unit is a WaterBallEvent.
//When revealTime is reached, it will become visible to players
//When startTime is reached, it will be removed and perform an attack.
//if the target is lost, dead, or untargetable, the ball expires without attacking.
public class WaterBallEvent : NpcBehaviorBase
{
    //val0 => targetId
    //val1 => damage ratio
    //val2 => revealTime
    //val3 => startTime
    //val4 => isInit

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (!npc.IsOwnerAlive)
        {
            npc.EndEvent(); //as soon as the owner dies we expire
            return;
        }

        var iTime = (int)(newTime * 1000);

        //check to see if it's time to reveal ourselves
        if (npc.ValuesInt[4] == 0 && iTime >= npc.ValuesInt[2])
        {
            //reveal
            npc.RevealAsEffect(NpcEffectType.WaterBall, "WaterBall");
            npc.ValuesInt[4] = 1;
        }

        //check to see if it's time to perform an attack
        if (iTime >= npc.ValuesInt[3])
        {
            var map = npc.Character.Map;
            
            if (npc.Owner.TryGet<CombatEntity>(out var src) && map != null)
            {
                var targetEntity = World.Instance.GetEntityById(npc.ValuesInt[0]);

                if (!targetEntity.TryGet<CombatEntity>(out var target) 
                    || !target.IsValidTarget(src) 
                    || target.Character.Position.DistanceTo(src.Character.Position) > 30)
                {
                    npc.EndEvent();
                    return;
                }

                //you need line of sight from the player to the target, OR, for monsters only, line of sight from this ground unit to the target
                if (!map.WalkData.HasLineOfSight(src.Character.Position, target.Character.Position))
                {
                    if (src.Character.Type != CharacterType.Monster || !map.WalkData.HasLineOfSight(npc.Character.Position, target.Character.Position))
                    {
                        npc.EndEvent();
                        return;
                    }
                }

                var ratio = npc.ValuesInt[1] / 100f;

                var res = src.CalculateCombatResult(target, ratio, 1, AttackFlags.Magical, CharacterSkill.WaterBall, AttackElement.Water);
                res.AttackMotionTime = 0;
                res.Time = Time.ElapsedTimeFloat + 0.7f;
                res.IsIndirect = true;

                CommandBuilder.SkillExecuteTargetedSkillAutoVis(npc.Character, target.Character,
                    CharacterSkill.WaterBall, 1, res);

                src.ExecuteCombatResult(res, false);
            }

            npc.EndEvent();
        }
    }
}

//register the water ball event so it can be used in scripts
public class NpcLoaderWaterBallEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("WaterBallEvent", new WaterBallEvent());
    }
}