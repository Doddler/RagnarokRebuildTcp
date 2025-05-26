using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[SkillHandler(CharacterSkill.WaterBall, SkillClass.Magic)]
public class WaterBallHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => lvl;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        if (source.Character.Type == CharacterType.Player)
        {
            if (source.Character.Map == null)
                return SkillValidationResult.Failure;

            var radius = lvl switch
            {
                <= 1 => 0,
                <= 3 => 1,
                _ => 2
            };

            if (!source.Character.Map.WalkData.HasWaterNearby(source.Character.Position, radius))
                return SkillValidationResult.MustBeStandingInWater;
        }

        return base.ValidateTarget(source, target, position, lvl);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null || !target.IsValidTarget(source) || source.Character.Map == null)
            return;

        var radius = lvl switch
        {
            <= 1 => 0,
            <= 3 => 1,
            _ => 2
        };

        if (source.Character.Type == CharacterType.Monster && lvl == 10)
            radius = 3;

        var map = source.Character.Map;
        var ch = source.Character;
        var targetId = target.Character.Id;
        var srcPos = source.Character.Position;
        var waterTiles = 999;
        var maxTiles = (radius * 2 + 1) * (radius * 2 + 1);
        var ratio = 100 + lvl * 30;

        if (source.Character.Type == CharacterType.Player)
            waterTiles = map.WalkData.CountNearbyWaterTiles(srcPos, radius);

        //the actual cast of water ball that we send to the client just shows the attack motion and doesn't deal damage.
        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.WaterBall);
        res.Result = AttackResult.Invisible;
        if (!isIndirect)
            source.ApplyCooldownForAttackAction(target);

        source.ExecuteCombatResult(res, false);
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.WaterBall, lvl, res);

        Span<Position> posList = stackalloc Position[maxTiles];
        var posCount = 1;
        posList[0] = srcPos;

        if (radius > 0)
        {
            //identify all walkable cells within the cast radius
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    var pos = new Position(srcPos.X + x, srcPos.Y + y);

                    if (!map.WalkData.IsCellWalkable(pos))
                        continue;

                    posList[posCount++] = pos;
                }
            }

            //shuffle the list
            for (var i = 1; i < posCount; i++)
            {
                var j = GameRandom.Next(1, posCount);
                if (i != j)
                    (posList[j], posList[i]) = (posList[i], posList[j]);
            }
        }

        var mt = 0.667f; //first ball fires 2/3 of a second after the skill is cast

        posCount = int.Min(posCount, waterTiles); //we only spawn as many balls as there are water tiles in a 7x7 area around you

        for (var i = 0; i < posCount; i++)
        {
            var e = World.Instance.CreateEvent(source.Entity, map, "WaterBallEvent", posList[i], targetId, ratio, 0, 0, null);
            ch.AttachEvent(e);

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

                CommandBuilder.SkillExecuteTargetedSkillAutoVis(npc.Character, target.Character, CharacterSkill.WaterBall, 1, res);

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