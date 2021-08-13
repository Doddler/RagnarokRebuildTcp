using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.MapEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Assets.Scripts
{
    public class PathNode
    {
        public PathNode Parent;
        public Vector2Int Position;
        public int Steps;
        public int Distance;
        public int F;

        public void Set(PathNode parent, Vector2Int position, int distance)
        {
            //Debug.Log($"Adding {position} to path node.");
            Parent = parent;
            Position = position;
            if (Parent == null)
                Steps = 0;
            else
                Steps = Parent.Steps + 1;
            Distance = distance;
            F = Steps + Distance;
        }

        public PathNode(PathNode parent, Vector2Int position, int distance)
        {
            Set(parent, position, distance);
        }
    }

    public static class Pathfinder
    {
        private static PathNode[] nodeCache;
        private static int cachePos;
        private const int MaxCacheSize = 1000;

        private static List<PathNode> openList = new List<PathNode>(MaxCacheSize);
        //private static List<PathNode> closedList = new List<PathNode>(MaxCacheSize);

        private static HashSet<Vector2Int> openListPos = new HashSet<Vector2Int>();
        private static HashSet<Vector2Int> closedListPos = new HashSet<Vector2Int>();

        private static void BuildCache()
        {
            Debug.Log("Build path cache");
            Profiler.BeginSample("Build PathNode Cache");

            nodeCache = new PathNode[MaxCacheSize];
            for (var i = 0; i < MaxCacheSize; i++)
            {
                var n = new PathNode(null, Vector2Int.zero, 0);
                nodeCache[i] = n;
            }

            cachePos = MaxCacheSize;
            Profiler.EndSample();
        }

        private static PathNode NextPathNode(PathNode parent, Vector2Int position, int distance)
        {
            var n = nodeCache[cachePos - 1];
            n.Set(parent, position, distance);
            cachePos--;
            return n;
        }
        
        private static int CalcDistance(Vector2Int pos, Vector2Int dest)
        {
            return Mathf.Abs(pos.x - dest.x) + Mathf.Abs(pos.y - dest.y);
        }
        
        private static bool HasPosition(List<PathNode> node, Vector2Int pos)
        {
            for (var i = 0; i < node.Count; i++)
            {
                if (node[i].Position == pos)
                    return true;
            }

            return false;
        }

        private static PathNode BuildPath(RagnarokWalkData walkData, Vector2Int start, Vector2Int target)
        {
            if (nodeCache == null)
                BuildCache();
            
            cachePos = MaxCacheSize;

            openList.Clear();
            openListPos.Clear();
            closedListPos.Clear();

            var current = NextPathNode(null, start, CalcDistance(start, target));

            openList.Add(current);

            Profiler.BeginSample("Performing Path Loop");

            while (openList.Count > 0 && !closedListPos.Contains(target))
            {
                Profiler.BeginSample("Retrieving next openList entry");
                current = openList[0];
                openList.RemoveAt(0);
                openListPos.Remove(current.Position);
                closedListPos.Add(current.Position);

                Profiler.EndSample();

                if (current.Steps > 15 || current.Steps + current.Distance/2 > 15)
                    continue;

                for (var x = -1; x <= 1; x++)
                {
                    for (var y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;

                        var np = current.Position;
                        np.x += x;
                        np.y += y;

                        if (np.x < 0 || np.y < 0 || np.x >= walkData.Width || np.y >= walkData.Height)
                            continue;
                        
                        if (closedListPos.Contains(np) || openListPos.Contains(np))
                            continue;

                        if ((walkData.Cell(np).Type & CellType.Walkable) != CellType.Walkable)
                            continue;

                        if (x == -1 && y == -1)
                            if (!walkData.CellWalkable(current.Position.x - 1, current.Position.y) ||
                                !walkData.CellWalkable(current.Position.x, current.Position.y - 1))
                                continue;


                        if (x == -1 && y == 1)
                            if (!walkData.CellWalkable(current.Position.x - 1, current.Position.y) ||
                                !walkData.CellWalkable(current.Position.x, current.Position.y + 1))
                                continue;


                        if (x == 1 && y == -1)
                            if (!walkData.CellWalkable(current.Position.x + 1, current.Position.y) ||
                                !walkData.CellWalkable(current.Position.x, current.Position.y - 1))
                                continue;


                        if (x == 1 && y == 1)
                            if (!walkData.CellWalkable(current.Position.x + 1, current.Position.y) ||
                                !walkData.CellWalkable(current.Position.x, current.Position.y + 1))
                                continue;
                        
                        if (np == target)
                        {
	                        Profiler.EndSample();
	                        return NextPathNode(current, np, 0);
                        }

                        Profiler.BeginSample("Adding node to open list");

                        openList.Add(NextPathNode(current, np, CalcDistance(np, target)));
                        openListPos.Add(np);
                        closedListPos.Add(np);

                        openList.Sort((a, b) => a.F.CompareTo(b.F));
                        Profiler.EndSample();
                        //openList = openList.OrderBy(o => o.F).ToList();
                    }
                }
                
            }

            Profiler.EndSample();

            //Debug.Log("Failed to find path, cache size is: " + cachePos);

            return null;
        }

        public static int GetPath(RagnarokWalkData walkData, Vector2Int start, Vector2Int target, Vector2Int[] pathOut)
        {
	        if (!walkData.CellWalkable(target.x, target.y))
		        return 0;

            Profiler.BeginSample("Pathfinding");
            var path = BuildPath(walkData, start, target);

            openList.Clear();
            //closedList.Clear();
            openListPos.Clear();
            closedListPos.Clear();
            //closedList.Clear();

            if (path == null)
            {
                //Debug.Log($"Failed to find path ${CalcDistance(start, target)} away");
                Profiler.EndSample();
                return 0;
            }

            //var final = new Vector2Int[path.Steps+1];
            //Debug.Log($"path is {path.Steps}, creating array {final.Length} in size.");

            var steps = path.Steps+1;

            if(path.Steps >= pathOut.Length)
                Debug.LogWarning($"Whoa! This isn't good. Steps is {path.Steps} but the array is {pathOut.Length}");

            while (path != null)
            {
                //Debug.Log(path.Steps + " " + path.Position);
                pathOut[path.Steps] = path.Position;
                path = path.Parent;
            }

            Profiler.EndSample();
            return steps;
        }
    }
}
