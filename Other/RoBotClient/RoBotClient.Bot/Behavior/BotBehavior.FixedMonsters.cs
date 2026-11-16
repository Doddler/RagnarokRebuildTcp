using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RoBotClient.Bot.GameData;

namespace RoBotClient.Bot.Behavior;

// Fixed monsters (Mandragora, Geographer...) can't chase. A ranged bot that outranges one kills it untouched, but only
// from a cell that no OTHER fixed monster reaches and that has line of sight; left to the server's attack-walk it
// stops at the first in-range cell, often inside a neighbour plant's reach (Bot5 died that way on prt_fild02).
// Geographers also heal allies within 9 cells below 60% HP (themselves included), which can make a fight unwinnable.
public sealed partial class BotBehavior
{
    // The server's attack range test: straight-line distance rounded to whole cells (DistanceCache.IntDistance).
    private static int ServerDistance(int x0, int y0, int x1, int y1) =>
        (int)Math.Round(Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)));

    private DateTime _nextFiringWalk;
    private Position _firingWalkGoal;

    /// <summary>True when the bot shoots this monster from out of its reach (fixed monster, bot's range longer).</summary>
    private bool SnipesFixed(Snapshot snap, MonsterInfo target) =>
        _data?.Monster(target.ClassId) is { } db && IsMonsterImmobile(db) && InferAttackRange(snap) > db.Range + 1;

    // A fixed monster other than the target that would hit someone standing on (x, y). One cell of margin for rounding.
    private bool InOtherFixedReach(Snapshot snap, int x, int y, int targetId)
    {
        foreach (var m in snap.Monsters)
        {
            if (m.Id == targetId || (m.MaxHp > 0 && m.Hp <= 0)) continue;
            if (_data?.Monster(m.ClassId) is not { } db || !IsMonsterImmobile(db)) continue;
            if (ServerDistance(x, y, m.Pos.X, m.Pos.Y) <= db.Range + 1) return true;
        }
        return false;
    }

    private bool IsFiringCell(Snapshot snap, WalkMap map, MonsterInfo target, int reach, int range, int x, int y)
    {
        var d = ServerDistance(x, y, target.Pos.X, target.Pos.Y);
        return d > reach + 1 && d <= range && map.IsWalkable(x, y) && !InOtherFixedReach(snap, x, y, target.Id)
               && !CellNearHazard(x, y) && map.HasLineOfSight(x, y, target.Pos.X, target.Pos.Y);
    }

    /// <summary>Nearest cell to shoot <paramref name="target"/> from untouched: the bot's own cell when it already
    /// qualifies, otherwise the closest qualifying cell. Null when there is none (or no walk data).</summary>
    private Position? FindFiringSpot(Snapshot snap, MonsterInfo target)
    {
        if (_data?.GetWalkMap(snap.Map) is not { } map || _data.Monster(target.ClassId) is not { } db) return null;
        var range = InferAttackRange(snap);
        if (IsFiringCell(snap, map, target, db.Range, range, snap.SelfPos.X, snap.SelfPos.Y)) return snap.SelfPos;
        Position? best = null;
        var bestWalk = int.MaxValue;
        for (var dx = -range; dx <= range; dx++)
            for (var dy = -range; dy <= range; dy++)
            {
                int x = target.Pos.X + dx, y = target.Pos.Y + dy;
                var walk = Dist(snap.SelfPos, new Position(x, y));
                if (walk >= bestWalk || !IsFiringCell(snap, map, target, db.Range, range, x, y)) continue;
                best = new Position(x, y);
                bestWalk = walk;
            }
        return best;
    }

    /// <summary>Hunting a fixed monster it outranges: walk to a safe firing cell before shooting. Returns true while it
    /// owns the tick (walking there, or giving up on a target with no safe cell); false once in place, so the normal
    /// attack runs from there.</summary>
    private async Task<bool> HoldFiringSpotAsync(Snapshot snap, MonsterInfo target, CancellationToken ct)
    {
        if (!SnipesFixed(snap, target)) return false;
        var spot = FindFiringSpot(snap, target);
        if (spot is not { } goal)
        {
            OnLog?.Invoke($"No safe cell to shoot '{target.Name}' from (other fixed monsters cover them all) — skipping it.");
            _unreachable.Add(_targetId);
            _targetId = 0;
            _targetClass = -1;
            await _bot.StopAsync(ct);
            return true;
        }
        if (goal.X == snap.SelfPos.X && goal.Y == snap.SelfPos.Y) return false;
        if (DateTime.UtcNow >= _nextFiringWalk || goal.X != _firingWalkGoal.X || goal.Y != _firingWalkGoal.Y)
        {
            _nextFiringWalk = DateTime.UtcNow.AddSeconds(1);
            _firingWalkGoal = goal;
            await _bot.WalkToAsync(goal.X, goal.Y, ct);
        }
        return true;
    }

    // ---- healers ----

    // Monsters whose skill script heals allies: Heal level, cooldown, ally HP % that triggers it
    // (server Script/MonsterSkills). ponytail: hand-copied; add rows as more healers show up.
    private static readonly Dictionary<string, (int Level, double Cooldown, double BelowPercent)> Healers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GEOGRAPHER"] = (10, 5.5, 60), // TryCast(Heal, 10, 100%, 0.5s cast, 5s)[FindAllyBelowHpPercent(60)]
    };
    private const int HealerRadius = 9; // FindAllyBelowHpPercent gathers allies within 9 cells (square), itself included

    /// <summary>HP per second one healer restores: the server's Heal formula, (level + INT) / 10 x (4 + 8 x skill level),
    /// without the magic-attack half it adds (unknown here, so this undercounts a little).</summary>
    private static double HealPerSecond(MonsterDbEntry db) =>
        Healers.TryGetValue(db.Code ?? "", out var h) ? (db.Level + db.Int) / 10 * (4 + 8 * h.Level) / h.Cooldown : 0;

    /// <summary>Healing a visible target would receive per second, from healers in view within reach of it.</summary>
    private double HealingOn(Snapshot snap, MonsterInfo target)
    {
        double total = 0;
        foreach (var m in snap.Monsters)
            if ((m.MaxHp <= 0 || m.Hp > 0) && Dist(m.Pos, target.Pos) <= HealerRadius && _data?.Monster(m.ClassId) is { } db)
                total += HealPerSecond(db);
        return total;
    }

    /// <summary>Stretch a forecast for healing on the target: below the heal threshold the bot's damage is offset by
    /// <paramref name="healPerSecond"/>, so that part takes longer (and costs more HP); out-healed means unwinnable.</summary>
    private static BattleForecast WithHealing(BattleForecast f, int maxHp, double healPerSecond, double belowPercent = 60)
    {
        if (healPerSecond <= 0 || f.SecondsToKillMonster <= 0 || maxHp <= 0) return f;
        var dps = maxHp / f.SecondsToKillMonster;
        var healed = belowPercent / 100;
        if (dps <= healPerSecond) return f with { CanWin = false, SecondsToKillMonster = 9999, ExpectedDamageTaken = f.ExpectedDamageTaken * 100 };
        var seconds = (1 - healed) * maxHp / dps + healed * maxHp / (dps - healPerSecond);
        var scale = seconds / f.SecondsToKillMonster;
        return f with
        {
            SecondsToKillMonster = seconds,
            ExpectedDamageTaken = f.ExpectedDamageTaken * scale,
            AttacksToKill = (int)Math.Ceiling(f.AttacksToKill * scale),
            CanWin = f.CanWin && (f.ExpectedDamageTaken <= 0 || seconds < f.SecondsToKillMe),
        };
    }
}
