using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Map;
using Wintellect.PowerCollections;

namespace RoRebuildServer.Simulation.Pathfinding;

public enum PathNodeType : int
{
    Unexplored,
    Open,
    Closed
};

public class PathNode : IComparable<PathNode>
{
    public PathNode? Parent;
    public Position Position;
    public int Steps;
    public int Cost;
    public int Total;
    public PathNodeType Type;

    //I'll probably regret not zeroing out the other values but I'll trust that Set will get called before they're ever used.
    public void Init(Position pos)
    {
        Parent = null;
        Position = pos;
        Type = PathNodeType.Unexplored;
    }

    public void Set(PathNode? parent, int steps, int cost, int total, PathNodeType type)
    {
        Parent = parent;
        Steps = steps;
        Cost = cost;
        Total = total;
        Type = type;
    }
    
#if DEBUG
    public string TracePath() => Parent != null ? Parent.TracePath() + (Position - Parent.Position).GetDirectionForOffset().NumPadDirection() : "0";
    public override string ToString() => $"({Position}: Path:{TracePath()} Steps:{Steps} Cost:{Cost} Total:{Total})";
#endif

    public int CompareTo(PathNode? other)
    {
        return Total.CompareTo(other!.Total);
    }
}

public class PathFinder
{
    private PathNode[]? nodeCache;
    private int cachePos;
    public const int MaxDistance = 16;
    private const int MaxCacheSize = ((MaxDistance + 1) * 2) * ((MaxDistance + 1) * 2) + 2; //the +2 is for our dummy nodes in GetPathWithInitialStep

    private Position start;
    private Position target;
    private MapWalkData walkData = null!;
    private int proximity;

    private readonly OrderedBag<PathNode> openBag = new();
    private readonly Dictionary<Position, PathNode> nodeList = new(); //you could probably replace this with an array to improve performance, it wouldn't even need to be that big.

    private int DistanceCost(Position pos) => (Math.Max(0, Math.Abs(pos.X - target.X) - proximity) + Math.Max(0, Math.Abs(pos.Y - target.Y) - proximity)) * 10;
    private bool IsConnected(Position pos, int tx, int ty) => IsConnected(pos.X, pos.Y, tx, ty);
    private bool IsConnected(int x, int y, int tx, int ty)
    {
        if (!walkData.IsCellWalkable(tx, ty)) return false;
        return walkData.IsCellWalkable(x, ty) && walkData.IsCellWalkable(tx, y);
    }

    private void BuildCache()
    {
        //ServerLogger.Log("Building path cache");

        nodeCache = new PathNode[MaxCacheSize];
        for (var i = 0; i < MaxCacheSize; i++)
        {
            var n = new PathNode() { Cost = 0, Position = Position.Invalid, Total = 0, Type = PathNodeType.Unexplored };
            nodeCache[i] = n;
        }

        cachePos = MaxCacheSize;
    }

    private void ResetPath()
    {
        if (nodeCache == null)
            BuildCache();

        cachePos = MaxCacheSize;

        openBag.Clear();
        nodeList.Clear();
        //Array.Clear(usedNodes);
    }
    
    private PathNode GetNode(Position pos)
    {
        Debug.Assert(nodeCache != null);

        if (nodeList.TryGetValue(pos, out var node))
            return node;

        var n = nodeCache[cachePos - 1];
        n.Init(pos);
        cachePos--;
        nodeList.Add(pos, n);
        return n;
    }

    //Used to add a position to a path without having it in our closed list.
    private PathNode GetDummyNode(Position pos)
    {
        Debug.Assert(nodeCache != null);

        var n = nodeCache[cachePos - 1];
        n.Init(pos);
        n.Steps = 0; //the dummy node is always a starting point
        cachePos--;
        return n;
    }

    private void VisitConnectingNode(PathNode parent, int cost, Position pos)
    {
        if (parent.Steps >= MaxDistance)
            return;

        var newCost = parent.Cost + cost;
        var node = GetNode(pos);
        if (node.Type != PathNodeType.Unexplored)
        {
            if (node.Cost <= newCost) return;

            if (node.Type == PathNodeType.Open)
                openBag.Remove(node);

            if (node.Type == PathNodeType.Closed)
                node.Type = PathNodeType.Open;
        }
        node.Set(parent, parent.Steps + 1, newCost, newCost + DistanceCost(pos), PathNodeType.Open);
        openBag.Add(node);
    }

    private PathNode? BuildPath()
    {
        while (openBag.Count > 0)
        {
            var node = openBag[0];
            if (node.Position == target || node.Position.SquareDistance(target) <= proximity)
                return node;

            openBag.RemoveFirst();

            var x = node.Position.X; //just for readability
            var y = node.Position.Y;

            if (IsConnected(node.Position, x + 1, y - 1)) VisitConnectingNode(node, 14, new Position(x + 1, y - 1)); //SE
            if (IsConnected(node.Position, x + 1, y + 0)) VisitConnectingNode(node, 10, new Position(x + 1, y + 0)); //E
            if (IsConnected(node.Position, x + 1, y + 1)) VisitConnectingNode(node, 14, new Position(x + 1, y + 1)); //NE
            if (IsConnected(node.Position, x + 0, y + 1)) VisitConnectingNode(node, 10, new Position(x + 0, y + 1)); //N
            if (IsConnected(node.Position, x - 1, y + 1)) VisitConnectingNode(node, 14, new Position(x - 1, y + 1)); //NW
            if (IsConnected(node.Position, x - 1, y + 0)) VisitConnectingNode(node, 10, new Position(x - 1, y + 0)); //W
            if (IsConnected(node.Position, x - 1, y - 1)) VisitConnectingNode(node, 14, new Position(x - 1, y - 1)); //SW
            if (IsConnected(node.Position, x + 0, y - 1)) VisitConnectingNode(node, 10, new Position(x + 0, y - 1)); // S

            node.Type = PathNodeType.Closed;
        }

        return null;
    }

    private void PreparePathfinder(MapWalkData walkData, Position start, Position target, int range)
    {
        this.target = target;
        this.walkData = walkData;
        this.proximity = range;
        this.start = start;

        ResetPath();
        var initialNode = GetNode(start);
        initialNode.Set(null, 0, 0, DistanceCost(start), PathNodeType.Open);
        openBag.Add(initialNode);
    }

    public bool HasPath(MapWalkData mapWalkData, Position startPosition, Position destination, int proximityToTarget)
    {
        if (startPosition == destination || startPosition.SquareDistance(destination) > MaxDistance)
            return false;

        PreparePathfinder(mapWalkData, startPosition, destination, proximityToTarget);

        var finalNode = BuildPath();
        return finalNode != null;

    }

    public int GetPath(MapWalkData mapWalkData, Position startPosition, Position destination, Position[] pathOut, int proximityToTarget)
    {
        if (startPosition == destination || startPosition.SquareDistance(destination) > MaxDistance)
            return 0;

        PreparePathfinder(mapWalkData, startPosition, destination, proximityToTarget);

        var finalNode = BuildPath();
        if (finalNode == null)
            return 0;

        var totalSteps = finalNode.Steps + 1;

        while (finalNode != null)
        {
            pathOut[finalNode.Steps] = finalNode.Position;
            finalNode = finalNode.Parent;
        }

#if DEBUG
        SanityCheck(pathOut, startPosition, destination, totalSteps, proximityToTarget);
#endif

        return totalSteps;
    }


    public int GetPathWithInitialStep(MapWalkData mapWalkData, Position startPosition, Position initialStep, Position destination, Position[] pathOut, int proximityToTarget, float currentStepProgress)
    {
        if (startPosition == destination || startPosition.SquareDistance(destination) > MaxDistance)
            return 0;

        //start pathfinding from the cell we are currently traveling into (initialStep), not the cell we currently occupy.
        PreparePathfinder(mapWalkData, initialStep, destination, proximityToTarget);
        openBag[0].Parent = GetDummyNode(startPosition); //because we're already moving we need to make sure the path we generate starts from our current startPosition.
        openBag[0].Steps = 1;
        
        //add a second starting node with the reverse of our current path, but this time we're coming from initialStep and moving back into our starting cell.
        var baseCost = (start - initialStep).IsOffsetDiagonal() ? 14 : 10;
        var doubleBackProgress = (int)((1 - currentStepProgress) * baseCost);
        var doubleBackNode = GetNode(startPosition);
        doubleBackNode.Set(GetDummyNode(initialStep), 1, 0, doubleBackProgress + DistanceCost(startPosition), PathNodeType.Open);
        openBag.Add(doubleBackNode);

        var finalNode = BuildPath();
        if (finalNode == null) return 0;

        var totalSteps = finalNode.Steps + 1;

        while (finalNode != null)
        {
            pathOut[finalNode.Steps] = finalNode.Position;
            finalNode = finalNode.Parent;
        }

#if DEBUG
        var swap = totalSteps > 1 && pathOut[1] == startPosition; //check if our path has us double back to our currently occupied cell
        SanityCheck(pathOut, swap ? initialStep : startPosition, target, totalSteps, proximity);
#endif
        
        return totalSteps;
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
}
