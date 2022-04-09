using System.Runtime.CompilerServices;
using RebuildSharedData.Data;

namespace RoRebuildServer.Simulation.Pathfinding;

public static class PositionExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Distance(this Position left, Position right)
    {
        return DistanceCache.IntDistance(left, right);
    }
}