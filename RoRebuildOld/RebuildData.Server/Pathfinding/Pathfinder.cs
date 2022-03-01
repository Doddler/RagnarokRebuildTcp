using System;
using System.Collections.Generic;
using System.Text;
using RebuildData.Server.Logging;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using Wintellect.PowerCollections;

namespace RebuildData.Server.Pathfinding
{
	public class PathNode : IComparable<PathNode>
	{
		public PathNode Parent;
		public Position Position;
		public int Steps;
		public float Distance;
		public float F;
		public float Score;
		public Direction Direction;

		public void Set(PathNode parent, Position position, float distance)
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
				Score = Parent.Score + 1;

				if (Direction.IsDiagonal())
					Score += 0.4f;
			}

			Distance = distance;
			F = Score + Distance;
		}

		public PathNode(PathNode parent, Position position, float distance)
		{
			Set(parent, position, distance);
		}

		public int CompareTo(PathNode other)
		{
			return F.CompareTo(other.F);
		}
	}

	public static class Pathfinder
	{
		private static PathNode[] nodeCache;
		private static int cachePos;
		private const int MaxDistance = 16;
		private const int MaxCacheSize = ((MaxDistance + 1) * 2) * ((MaxDistance + 1) * 2);

		//private static List<PathNode> openList = new List<PathNode>(MaxCacheSize);

		private static OrderedBag<PathNode> openBag = new OrderedBag<PathNode>();

		//private static HashSet<Position> openListPos = new HashSet<Position>();
		private static HashSet<Position> closedListPos = new HashSet<Position>();

		private static Dictionary<int, PathNode> nodeLookup = new Dictionary<int, PathNode>(MaxCacheSize);

		private static Position[] tempPath = new Position[MaxDistance + 1];

		private static int range = 0;

		private static void BuildCache()
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

		private static PathNode NextPathNode(PathNode parent, Position position, float distance)
		{
			var n = nodeCache[cachePos - 1];
			n.Set(parent, position, distance);
			cachePos--;
			return n;
		}

		private static float CalcDistance(Position pos, Position dest)
		{
			return Math.Max(0, Math.Abs(pos.X - dest.X) - range) + Math.Max(0, Math.Abs(pos.Y - dest.Y) - range);
		}

		private static bool HasPosition(List<PathNode> node, Position pos)
		{
			for (var i = 0; i < node.Count; i++)
			{
				if (node[i].Position == pos)
					return true;
			}

			return false;
		}

		private static void AddLookup(Position pos, PathNode node)
		{
			nodeLookup.Add((pos.X << 12) + pos.Y, node);
		}

		private static PathNode GetNode(Position pos)
		{
			return nodeLookup[(pos.X << 12) + pos.Y];
		}

		//private static void InsertOpenNode(PathNode node)
		//{
		//	for (var i = 0; i < openList.Count; i++)
		//	{
		//		if (node.F < openList[i].F)
		//		{
		//			openList.Insert(i, node);
		//			return;
		//		}
		//	}

		//	openList.Add(node);
		//}

		private static PathNode BuildPath(MapWalkData walkData, Position start, Position target, int maxLength, int range)
		{
			if (nodeCache == null)
				BuildCache();

			cachePos = MaxCacheSize;
			Pathfinder.range = range;

			//openList.Clear();
			openBag.Clear();
			//openListPos.Clear();
			closedListPos.Clear();
			nodeLookup.Clear();

			var current = NextPathNode(null, start, CalcDistance(start, target));

			openBag.Add(current);
			//openList.Add(current);
			AddLookup(start, current);

			while (openBag.Count > 0 && !closedListPos.Contains(target))
			{
				//current = openList[0];
				current = openBag[0];
				openBag.RemoveFirst();
				//openList.RemoveAt(0);
				//openListPos.Remove(current.Position);
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

						//if (openListPos.Contains(np))
						//{
						//	//the open list contains the neighboring cell. Check if the path from this node is better or not
						//	var oldNode = GetNode(np);
						//	var dir = (np - current.Position).GetDirectionForOffset();
						//	var distance = CalcDistance(np, target);
						//	var newF = current.Score + 1 + distance + (dir.IsDiagonal() ? 0.4f : 0f);
						//	if (newF < oldNode.F)
						//	{
						//		oldNode.Set(current, np, CalcDistance(np, target)); //swap the old parent to us if we're better
						//	}

						//	continue;
						//}

						if (closedListPos.Contains(np))
							continue;


						if (!walkData.IsCellWalkable(np))
							continue;

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
						{
							Profiler.Event(ProfilerEvent.PathFoundIndirect);
							return NextPathNode(current, np, 0);
						}

						var newNode = NextPathNode(current, np, CalcDistance(np, target));
						//openList.Add(newNode);

						//InsertOpenNode(newNode);

						openBag.Add(newNode);
						//openListPos.Add(np);
						AddLookup(np, newNode);
						closedListPos.Add(np);

						//openList.Sort((a, b) => a.F.CompareTo(b.F));
					}
				}

			}

			Profiler.Event(ProfilerEvent.PathNotFound);

			return null;
		}

		private static int CheckDirectPath(MapWalkData walkData, Position start, Position target, int maxDistance, int range, int startPos)
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

				if (pos.SquareDistance(target) <= range)
				{
					Profiler.Event(ProfilerEvent.PathFoundDirect);
					return i;
				}
			}

			return 0;
		}

		public static void CopyTempPath(Position[] path, int length)
		{
			Array.Copy(tempPath, path, length);
		}

		private static PathNode MakePath(MapWalkData walkData, Position start, Position target, int maxDistance, int range)
		{
			if (!walkData.IsCellWalkable(target))
				return null;

			var path = BuildPath(walkData, start, target, maxDistance, range);

			//openList.Clear();
			//openListPos.Clear();
			openBag.Clear();
			closedListPos.Clear();

			return path;
		}

		public static void SanityCheck(Position[] pathOut, Position start, Position target, int length, int range)
		{
			//this will break if the tiles are more than one apart
			for (var i = 0; i < length - 1; i++)
				(pathOut[i] - pathOut[i + 1]).GetDirectionForOffset();

			if (pathOut[0] != start)
				throw new Exception("First entry in path should be our starting position!");

			if (pathOut[length - 1].SquareDistance(target) > range)
				throw new Exception("Last entry is not at the expected position!");
		}

		public static int GetPathWithInitialStep(MapWalkData walkData, Position start, Position initial, Position target, Position[] pathOut, int range)
		{
			Profiler.Event(ProfilerEvent.PathfinderCall);

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

		public static int GetPath(MapWalkData walkData, Position start, Position target, Position[] pathOut, int range)
		{
			Profiler.Event(ProfilerEvent.PathfinderCall);

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
}
