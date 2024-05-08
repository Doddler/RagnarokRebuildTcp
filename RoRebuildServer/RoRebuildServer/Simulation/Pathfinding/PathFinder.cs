using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Logging;
using Wintellect.PowerCollections;

namespace RoRebuildServer.Simulation.Pathfinding;

public class PathNode : IComparable<PathNode>
{
    public PathNode? Parent;
    public Position Position;
    public int Steps;
    public int Distance;
    public int F;
    public int Score;
    public Direction Direction;

    public void Set(PathNode? parent, Position position, int distance)
    {
        Parent = parent;
        Position = position;
        if (Parent == null)
        {
            Steps = 0;
            Score = 0;
            Direction = Direction.None;
        }
        else
        {
            Direction = (position - Parent.Position).GetDirectionForOffset();

            Steps = Parent.Steps + 1;
            Score = Parent.Score + 10;

            if (Direction.IsDiagonal())
                Score += 4;
        }

        Distance = distance;
        F = Score + Distance;
    }

    public PathNode(PathNode? parent, Position position, int distance)
    {
        Set(parent, position, distance);
    }

    public int CompareTo(PathNode? other)
    {
        return F.CompareTo(other!.F); //the things we do to pass ide nullability checks
    }
}
	
public class Pathfinder
{
    private PathNode[]? nodeCache;
    private int cachePos;
    public const int MaxDistance = 16;
    private const int MaxCacheSize = ((MaxDistance + 1) * 2) * ((MaxDistance + 1) * 2);

    //private static List<PathNode> openList = new List<PathNode>(MaxCacheSize);

    private OrderedBag<PathNode> openBag = new OrderedBag<PathNode>();
    private HashSet<Position> closedListPos = new HashSet<Position>();

    //private Dictionary<int, PathNode> nodeLookup = new Dictionary<int, PathNode>(MaxCacheSize);

    private Position[] tempPath = new Position[MaxDistance + 1];

    private int pathRange = 0;

    private void BuildCache()
    {
        ServerLogger.Log("Build path cache");

        nodeCache = new PathNode[MaxCacheSize];
        for (var i = 0; i < MaxCacheSize; i++)
        {
            var n = new PathNode(null, Position.Zero, 0);
            nodeCache[i] = n;
        }

        cachePos = MaxCacheSize;

    }

    private PathNode NextPathNode(PathNode? parent, Position position, int distance)
    {
        Debug.Assert(nodeCache != null);

        var n = nodeCache[cachePos - 1];
        n.Set(parent, position, distance);
        cachePos--;
        return n;
    }

    private int CalcDistance(Position pos, Position dest)
    {
        return (Math.Max(0, Math.Abs(pos.X - dest.X) - pathRange) + Math.Max(0, Math.Abs(pos.Y - dest.Y) - pathRange));
    }

    private bool HasPosition(List<PathNode> node, Position pos)
    {
        for (var i = 0; i < node.Count; i++)
        {
            if (node[i].Position == pos)
                return true;
        }

        return false;
    }

    //private void AddLookup(Position pos, PathNode node)
    //{
    //    nodeLookup.Add((pos.X << 12) + pos.Y, node);
    //}

    //private PathNode GetNode(Position pos)
    //{
    //    return nodeLookup[(pos.X << 12) + pos.Y];
    //}
    
    private PathNode? BuildPath(MapWalkData walkData, Position start, Position target, int maxLength, int range)
    {
        if (nodeCache == null)
            BuildCache();

        cachePos = MaxCacheSize;
        pathRange = range;

        openBag.Clear();
        closedListPos.Clear();
        
        var current = NextPathNode(null, start, CalcDistance(start, target));

        openBag.Add(current);
        
        while (openBag.Count > 0 && !closedListPos.Contains(target))
        {
            current = openBag[0];
            openBag.RemoveFirst();
            closedListPos.Add(current.Position);

            if (current.Steps > maxLength || current.Steps + current.Distance / 2 > maxLength)
                continue;

            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    var np = current.Position;
                    np.X += x;
                    np.Y += y;

                    if (np.X < 0 || np.Y < 0 || np.X >= walkData.Width || np.Y >= walkData.Height)
                        continue;

                    if (closedListPos.Contains(np))
                        continue;


                    if (!walkData.IsCellWalkable(np))
                        continue;

                    //you can only move diagonally if it doesn't cut across an un-walkable tile.
                    if (x == -1 && y == -1)
                        if (!walkData.IsCellWalkable(current.Position.X - 1, current.Position.Y) ||
                            !walkData.IsCellWalkable(current.Position.X, current.Position.Y - 1))
                            continue;

                    if (x == -1 && y == 1)
                        if (!walkData.IsCellWalkable(current.Position.X - 1, current.Position.Y) ||
                            !walkData.IsCellWalkable(current.Position.X, current.Position.Y + 1))
                            continue;

                    if (x == 1 && y == -1)
                        if (!walkData.IsCellWalkable(current.Position.X + 1, current.Position.Y) ||
                            !walkData.IsCellWalkable(current.Position.X, current.Position.Y - 1))
                            continue;

                    if (x == 1 && y == 1)
                        if (!walkData.IsCellWalkable(current.Position.X + 1, current.Position.Y) ||
                            !walkData.IsCellWalkable(current.Position.X, current.Position.Y + 1))
                            continue;

                    if (np.SquareDistance(target) <= range)
                        return NextPathNode(current, np, 0);

                    var newNode = NextPathNode(current, np, CalcDistance(np, target));

                    openBag.Add(newNode);
                    closedListPos.Add(np);
                }
            }
        }

        return null;
    }

    private int CheckDirectPath(MapWalkData walkData, Position start, Position target, int maxDistance, int range, int startPos)
    {
        var pos = start;
        tempPath[startPos] = pos;
        var i = startPos + 1;
        
        while (i < maxDistance)
        {
            if (pos.X > target.X + range)
                pos.X--;
            if (pos.X < target.X - range)
                pos.X++;
            if (pos.Y > target.Y + range)
                pos.Y--;
            if (pos.Y < target.Y - range)
                pos.Y++;

            if (!walkData.IsCellWalkable(pos))
                return 0;

            tempPath[i] = pos;
            i++;

            if (pos.InRange(target, range))
            {
                //Profiler.Event(ProfilerEvent.PathFoundDirect);
                return i;
            }
        }

        return 0;
    }

    public void CopyTempPath(Position[] path, int length)
    {
        Array.Copy(tempPath, path, length);
    }

    private PathNode? MakePath(MapWalkData walkData, Position start, Position target, int maxDistance, int range)
    {
        if (!walkData.IsCellWalkable(target))
            return null;

        var path = BuildPath(walkData, start, target, maxDistance, range);

        openBag.Clear();
        closedListPos.Clear();

        return path;
    }

    public void SanityCheck(Position[] pathOut, Position start, Position target, int length, int range)
    {
        //this will break if the tiles are more than one apart
        for (var i = 0; i < length - 1; i++)
            (pathOut[i] - pathOut[i + 1]).GetDirectionForOffset();

        if (pathOut[0] != start)
            throw new Exception("First entry in path should be our starting position!");
        
        if (pathOut[length - 1].SquareDistance(target) > range)
            throw new Exception("Last entry is not at the expected position!");
    }

    private void CopyPathIntoPositionArray(PathNode? path, Position[] pathOut, int startIndex)
    {
        while (path != null)
        {
            pathOut[path.Steps + startIndex] = path.Position;
            path = path.Parent;
        }
    }

    public int GetPathWithinAttackRange(MapWalkData walkData, Position start, Position initialNextTile, Position target, Position[] pathOut, int attackRange)
    {
        var hasFirstStep = initialNextTile != Position.Invalid;
        var pathStart = hasFirstStep ? initialNextTile : start; //if we have a next tile already set, use that as the start.
        var maxDistance = hasFirstStep ? MaxDistance - 1 : MaxDistance;
        var firstStep = hasFirstStep ? 1 : 0;

        if (hasFirstStep && initialNextTile == target) 
            return 0; //no need to change path if we're already next to the target

        if (DistanceCache.InRange(pathStart, target, attackRange) && walkData.HasLineOfSight(pathStart, target))
            return 0; //our start position is already in line of sight

        //first check if a straight line exists
        var direct = CheckDirectPath(walkData, pathStart, target, maxDistance, 1, firstStep);
        if (direct > 0)
        {
            if (hasFirstStep)
                tempPath[0] = start;
                
            //since we know we can get to the target's own cell, we know at some point you'll be in attack range and line of sight.
            direct = GetStepsToAttackRange(walkData, tempPath, target, direct, attackRange);
            Array.Copy(tempPath, 0, pathOut, 0, direct);

#if DEBUG 
            SanityCheck(pathOut, start, target, direct, attackRange); //verify the path is valid and ends within range
#endif
            return direct;
        }

        var path = MakePath(walkData, pathStart, target, maxDistance, 1); //find a path that goes directly to the target
        if (path == null)
            return 0;

        var steps = path.Steps + 1 + firstStep;

        CopyPathIntoPositionArray(path, pathOut, firstStep);
        if (hasFirstStep)
            pathOut[0] = start;

        steps = GetStepsToAttackRange(walkData, pathOut, target, steps, attackRange); //shrink path to only the point where you can attack from

#if DEBUG
        if(steps > 0)
            SanityCheck(pathOut, start, target, steps, attackRange);  //verify the path is valid and ends within range
#endif
        return steps;
    }
    private int GetStepsToAttackRange(MapWalkData walkData, Position[] path, Position target, int length, int range)
    {
        for (var i = 1; i < length - 1; i++)
        {
            if (DistanceCache.InRange(path[i], target, range) && walkData.HasLineOfSight(path[i], target))
                return i + 1; //the +1 is because element 0 of our path is the start position
        }

        return length;
    }

    public int GetPathWithinAttackRange(MapWalkData walkData, Position start, Position target, Position[] pathOut, int attackRange)
    {
        return GetPathWithinAttackRange(walkData, start, Position.Invalid, target, pathOut, attackRange);
    }

    public int GetPathWithInitialStep(MapWalkData walkData, Position start, Position initial, Position target, Position[]? pathOut, int range)
    {
        if (initial == target)
            return 0;

        if (Math.Abs(start.X - target.X) > MaxDistance || Math.Abs(start.Y - target.Y) > MaxDistance)
            return 0;

        var direct = CheckDirectPath(walkData, initial, target, MaxDistance - 1, range, 1);
        if (direct > 0)
        {
            tempPath[0] = start;
            if (pathOut == null)
                return direct;
            CopyTempPath(pathOut, direct);
#if DEBUG
				SanityCheck(pathOut, start, target, direct, range);
#endif

            return direct;
        }

        var path = MakePath(walkData, initial, target, MaxDistance - 1, range);
        if (path == null)
            return 0;

        var steps = path.Steps + 1;

        if (pathOut == null)
            return steps;

        if (path.Steps >= pathOut.Length)
            ServerLogger.LogWarning($"Whoa! This isn't good. Steps is {path.Steps} but the array is {pathOut.Length}");

        while (path != null)
        {
            pathOut[path.Steps + 1] = path.Position;
            path = path.Parent;
        }

        pathOut[0] = start;

#if DEBUG
			SanityCheck(pathOut, start, target, steps + 1, range);
#endif

        return steps + 1; //add initial first step
    }

    public bool HasPath(MapWalkData walkData, Position start, Position target, int range)
    {
        if (start == target)
            return true;

        if (Math.Abs(start.X - target.X) > MaxDistance || Math.Abs(start.Y - target.Y) > MaxDistance)
            return false;

        var direct = CheckDirectPath(walkData, start, target, MaxDistance, range, 0);
        if (direct > 0)
            return true;

        var path = MakePath(walkData, start, target, MaxDistance, range);

        return path != null;
    }

    public int GetPath(MapWalkData walkData, Position start, Position target, Position[]? pathOut, int range)
    {
        if (start == target)
            return 0;

        if (Math.Abs(start.X - target.X) > MaxDistance || Math.Abs(start.Y - target.Y) > MaxDistance)
            return 0;

        var direct = CheckDirectPath(walkData, start, target, MaxDistance, range, 0);
        if (direct > 0)
        {
            if (pathOut == null)
                return direct;

            CopyTempPath(pathOut, direct);
#if DEBUG
				SanityCheck(pathOut, start, target, direct, range);
#endif
            return direct;
        }

        var path = MakePath(walkData, start, target, MaxDistance, range);
        if (path == null)
            return 0;

        var steps = path.Steps + 1;

        if (pathOut == null)
            return steps;

#if DEBUG
			if (path.Steps >= pathOut.Length)
				ServerLogger.LogWarning($"Whoa! This isn't good. Steps is {path.Steps} but the array is {pathOut.Length}");
#endif

        while (path != null)
        {
            pathOut[path.Steps] = path.Position;
            path = path.Parent;
        }

#if DEBUG
			SanityCheck(pathOut, start, target, steps, range);
#endif

        return steps;
    }
}