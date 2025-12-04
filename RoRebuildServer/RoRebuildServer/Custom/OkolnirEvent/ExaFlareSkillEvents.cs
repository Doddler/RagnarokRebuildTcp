using System.Diagnostics;
using RebuildSharedData.Data;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation.Util;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.Networking;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Custom.OkolnirEvent;

public class ExaflareControlEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.ValuesInt[0] = 0;
        npc.ValuesInt[1] = 0;
        npc.ValuesInt[2] = param1;
        npc.StartTimer(250);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > npc.ValuesInt[0] / 1000f)
        {
            var westX = 117;
            var eastX = 146;
            var northY = 150;
            var southY = 121;

            if (npc.ValuesInt[1] > 0 && GameRandom.Next(0, 4) >= 4 - npc.ValuesInt[1])
                //if(true)
            {
                //diagonal
                var xSet = GameRandom.Next(0, 10);
                var ySet = GameRandom.Next(0, 10);
                var xDivide = GameRandom.Next(0, 2);
                var yDivide = GameRandom.Next(0, 2); //xDivide == 0 ? 1 : 0;

                if (GameRandom.Next(0, 2) == 0)
                {
                    if (yDivide > 0)
                        SpawnWave(npc, new Position(westX, southY + ySet), 7, 2, 2);
                    else
                        SpawnWave(npc, new Position(westX, northY - ySet), 1, 2, -2);
                }
                else
                {
                    if (yDivide > 0)
                        SpawnWave(npc, new Position(eastX, southY + ySet), 5, -2, 2);
                    else
                        SpawnWave(npc, new Position(eastX, northY - ySet), 3, -2, -2);
                }

                if (GameRandom.Next(0, 2) == 0)
                {
                    if (xDivide > 0)
                        SpawnWave(npc, new Position(westX + xSet, southY), 7, 2, 2);
                    else
                        SpawnWave(npc, new Position(eastX - xSet, southY), 5, -2, 2);
                }
                else
                {
                    if (xDivide > 0)
                        SpawnWave(npc, new Position(westX + xSet, northY), 1, 2, -2);
                    else
                        SpawnWave(npc, new Position(eastX - ySet, northY), 3, -2, -2);
                }
            }
            else
            {
                var pos = npc.Character.Position; //first set will always be the boss's current target
                if (npc.ValuesInt[1] > 0)
                {
                    pos = new Position(GameRandom.NextInclusive(westX, eastX), GameRandom.Next(southY, northY));

                    using var list = EntityListPool.Get();
                    npc.Character.Map?.GatherPlayersInArea(Area.CreateAroundPoint(135, 133, 32, 34), list, true);
                    if (list.Count >= 0 && list[GameRandom.Next(0, list.Count)].TryGet<WorldObject>(out var newTarget))
                        pos = newTarget.Position;
                }

                if (pos.X < westX || pos.X > eastX)
                    pos.X = GameRandom.NextInclusive(westX, eastX);
                if (pos.Y < southY || pos.Y > northY)
                    pos.Y = GameRandom.NextInclusive(southY, northY);

                if (GameRandom.Next(0, 2) == 0)
                    SpawnWave(npc, new Position(westX, pos.Y), 0, 3, 0);
                else
                    SpawnWave(npc, new Position(eastX, pos.Y), 4, -3, 0);

                if (GameRandom.Next(0, 2) == 0)
                    SpawnWave(npc, new Position(pos.X, southY), 6, 0, 3);
                else
                    SpawnWave(npc, new Position(pos.X, northY), 2, 0, -3);
            }

            npc.ValuesInt[1]++;
            npc.ValuesInt[0] = npc.ValuesInt[1] < npc.ValuesInt[2] ? npc.ValuesInt[1] * 3000 : 999999;
        }

        if (newTime > 20f + 5f * npc.ValuesInt[2])
            npc.EndEvent();
    }

    private void SpawnWave(Npc npc, Position pos, int direction, int xStep, int yStep)
    {
        npc.CreateEvent("ExaflareRowEvent", pos, xStep, yStep, direction);
    }
}

public class ExaflareOutOfEventControlEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.ValuesInt[0] = 0;
        npc.ValuesInt[1] = 0;
        npc.ValuesInt[2] = param1;
        npc.StartTimer(250);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > npc.ValuesInt[0] / 1000f)
        {
            if (!npc.Owner.TryGet<WorldObject>(out var owner) || owner.Map == null)
            {
                npc.EndEvent();
                return;
            }

            var westX = owner.Map.ScanLineOfSightForWallInDirection(npc.Character.Position, owner.Position, 16, Direction.West).X;
            var eastX = owner.Map.ScanLineOfSightForWallInDirection(npc.Character.Position, owner.Position, 16, Direction.East).X;
            var northY = owner.Map.ScanLineOfSightForWallInDirection(npc.Character.Position, owner.Position, 16, Direction.North).Y;
            var southY = owner.Map.ScanLineOfSightForWallInDirection(npc.Character.Position, owner.Position, 16, Direction.South).Y;

            if (npc.ValuesInt[1] > 0 && GameRandom.Next(0, 4) >= 4 - npc.ValuesInt[1])
            {
                //diagonal
                var xSet = GameRandom.Next(0, 10);
                var ySet = GameRandom.Next(0, 10);
                var xDivide = GameRandom.Next(0, 2);
                var yDivide = GameRandom.Next(0, 2); //xDivide == 0 ? 1 : 0;

                if (GameRandom.Next(0, 2) == 0)
                {
                    if (yDivide > 0)
                        SpawnWave(npc, new Position(westX, southY + ySet), 7, 2, 2);
                    else
                        SpawnWave(npc, new Position(westX, northY - ySet), 1, 2, -2);
                }
                else
                {
                    if (yDivide > 0)
                        SpawnWave(npc, new Position(eastX, southY + ySet), 5, -2, 2);
                    else
                        SpawnWave(npc, new Position(eastX, northY - ySet), 3, -2, -2);
                }

                if (GameRandom.Next(0, 2) == 0)
                {
                    if (xDivide > 0)
                        SpawnWave(npc, new Position(westX + xSet, southY), 7, 2, 2);
                    else
                        SpawnWave(npc, new Position(eastX - xSet, southY), 5, -2, 2);
                }
                else
                {
                    if (xDivide > 0)
                        SpawnWave(npc, new Position(westX + xSet, northY), 1, 2, -2);
                    else
                        SpawnWave(npc, new Position(eastX - ySet, northY), 3, -2, -2);
                }
            }
            else
            {
                var pos = npc.Character.Position; //first set will always be the boss's current target
                if (npc.ValuesInt[1] > 0)
                {
                    pos = new Position(GameRandom.NextInclusive(westX, eastX), GameRandom.Next(southY, northY));

                    using var list = EntityListPool.Get();
                    npc.Character.Map?.GatherPlayersInArea(Area.CreateAroundPoint(owner.Position, 32, 32), list, true);
                    if (list.Count > 0 && list[GameRandom.Next(0, list.Count)].TryGet<WorldObject>(out var newTarget))
                        pos = newTarget.Position;
                }

                npc.MoveNpc(pos.X, pos.Y);

                if (pos.X < westX || pos.X > eastX)
                    pos.X = GameRandom.NextInclusive(westX, eastX);
                if (pos.Y < southY || pos.Y > northY)
                    pos.Y = GameRandom.NextInclusive(southY, northY);

                if (GameRandom.Next(0, 2) == 0)
                    SpawnWave(npc, new Position(westX, pos.Y), 0, 3, 0);
                else
                    SpawnWave(npc, new Position(eastX, pos.Y), 4, -3, 0);

                if (GameRandom.Next(0, 2) == 0)
                    SpawnWave(npc, new Position(pos.X, southY), 6, 0, 3);
                else
                    SpawnWave(npc, new Position(pos.X, northY), 2, 0, -3);
            }

            npc.ValuesInt[1]++;
            npc.ValuesInt[0] = npc.ValuesInt[1] < npc.ValuesInt[2] ? npc.ValuesInt[1] * 3000 : 999999;
        }

        if (newTime > 20f + 5f * npc.ValuesInt[2])
            npc.EndEvent();
    }

    private void SpawnWave(Npc npc, Position pos, int direction, int xStep, int yStep)
    {
        pos.ClampToArea(npc.Character.Map!.MapBounds);
        npc.CreateEvent("ExaflareRowSkipWallEvent", pos, xStep, yStep, direction);
    }
}

public class ExaflareRowEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.ValuesInt[0] = param1;
        npc.ValuesInt[1] = param2;
        npc.ValuesInt[2] = param3;
        npc.ValuesInt[3] = 0;

        npc.StartTimer();

        npc.CreateEvent("ExaflareBlastEvent", npc.Character.Position, 0, npc.ValuesInt[2]);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        Debug.Assert(npc.Character.Map != null);

        if (newTime >= 3f && npc.ValuesInt[3] == 0)
        {
            npc.MoveNpcRelative(npc.ValuesInt[0], npc.ValuesInt[1]);
            //npc.Character.Position += new Position(npc.ValuesInt[0], npc.ValuesInt[1]);
            if (!npc.Character.Map.WalkData.IsCellWalkable(npc.Character.Position))
            {
                npc.ValuesInt[3] = 1; //we can't just end the event here or the children get removed, so we wait it out
                return;
            }

            npc.CreateEvent("ExaflareBlastEvent", npc.Character.Position, 1);
            npc.SetTimer(2400);
        }

        if (newTime > 15f)
            npc.EndEvent();
    }
}

public class ExaflareRowSkipWallEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.ValuesInt[0] = param1;
        npc.ValuesInt[1] = param2;
        npc.ValuesInt[2] = param3;
        npc.ValuesInt[3] = 0;

        npc.StartTimer();

        npc.CreateEvent("ExaflareBlastEvent", npc.Character.Position, 0, npc.ValuesInt[2]);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        Debug.Assert(npc.Character.Map != null);

        if (newTime >= 3f && npc.ValuesInt[3] == 0)
        {
            var newPos = new Position(npc.Character.Position.X + npc.ValuesInt[0], npc.Character.Position.Y + npc.ValuesInt[1]);

            if (!npc.Character.Map.MapBounds.Contains(newPos))
            {
                npc.EndEvent();
                return;
            }

            npc.MoveNpc(newPos.X, newPos.Y);

            //npc.Character.Position += new Position(npc.ValuesInt[0], npc.ValuesInt[1]);
            if (npc.Character.Map.WalkData.IsCellWalkable(newPos))
                npc.CreateEvent("ExaflareBlastEvent", newPos, 1);
            npc.SetTimer(2400);
        }

        if (newTime > 15f)
            npc.EndEvent();
    }
}

public class ExaflareBlastEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.StartTimer();
        if (param1 == 0)
        {
            npc.PlayEffectAtMyLocation("ExaflareMaster", param2);
            npc.TimerStart += 2.4f; //push the timer into the future
            return;
        }

        if (param2 == 0)
            npc.PlayEffectAtMyLocation("ExaflareSub");
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (lastTime < 1.2f && newTime >= 1.2f)
        {
            npc.PlayEffectAtMyLocation("ThunderStorm");
        }

        if (newTime > 1.28f)
        {
            using var el = EntityListPool.Get();
            npc.Character.Map?.GatherPlayersInRange(npc.Character.Position, 3, el, false);
            npc.Character.Map?.AddVisiblePlayersAsPacketRecipients(npc.Character);
            foreach (var e in el)
            {
                if (!e.TryGet<CombatEntity>(out var target) || target.Character.State == CharacterState.Dead || !target.IsTargetable)
                    continue;

                ServerLogger.Log($"Exaflare hit check at distance: {npc.Character.WorldPosition.DistanceTo(target.Character.WorldPosition)}");

                if (npc.Character.WorldPosition.DistanceTo(target.Character.WorldPosition) > 3.6f)
                    continue;

                var req = new AttackRequest(CharacterSkill.ThunderStorm, 1, 5, AttackFlags.Magical | AttackFlags.IgnoreDefense, AttackElement.Wind);
                req.MinAtk = 200;
                req.MaxAtk = 350;

                var res = target.CalculateDamageTakenFromAnonymousSource(req);
                res.AttackMotionTime = 0;
                res.Time = Time.ElapsedTimeFloat;
                res.IsIndirect = true;

                CommandBuilder.AttackMulti(target.Character, target.Character, res, false);
                target.QueueDamage(res);
            }

            CommandBuilder.ClearRecipients();

            npc.EndEvent();
        }
    }
}