using RebuildSharedData.Data;
using RoRebuildServer.Logging;
using RoRebuildServer.Simulation.Pathfinding;

namespace RoRebuildServer.Data.Map;

[Flags]
public enum CellType : byte
{
    None = 0,
    Walkable = 1,
    Water = 2,
    Snipable = 4,
    SeeThrough = Walkable | Snipable,
}

public class MapWalkData
{
    public int Width;
    public int Height;
    public Area Bounds;
    private byte[] cellData;

    public bool IsPositionInBounds(Position p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
    public bool IsPositionInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
    public bool IsCellWalkable(int x, int y) => x < Width && y < Height && (cellData[x + y * Width] & 1) == 1;
    public bool IsCellWalkable(Position p) => (cellData[p.X + p.Y * Width] & 1) == 1;
    public bool IsCellSnipable(int x, int y) => (cellData[x + y * Width] & 4) == 4;
    public bool IsCellSnipable(Position p) => (cellData[p.X + p.Y * Width] & 4) == 4;
    public bool IsCellInWater(int x, int y) => IsPositionInBounds(x, y) && (cellData[x + y * Width] & 2) == 2;
    public bool IsCellInWater(Position p) => IsPositionInBounds(p) && (cellData[p.X + p.Y * Width] & 2) == 2;
    public bool DoesCellBlockLos(int x, int y) => (cellData[x + y * Width] & (byte)CellType.SeeThrough) == 0;
    public bool DoesCellBlockLos(Position p) => (cellData[p.X + p.Y * Width] & (byte)CellType.SeeThrough) == 0;

    public bool IsCellAdjacentToWall(Position p)
    {
        if (p.X <= 1 || p.X >= Width - 1 || p.Y <= 1 || p.Y >= Height - 1)
            return true; //it's next to the edge of the map, which is good enough for a wall. Plus we don't out of bounds our lookups.

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (!IsCellWalkable(p.X + x, p.Y + y))
                    return true;
            }
        }

        return false;
    }

    public Position FindWalkdableCellOnMap(int tries)
    {
        var pos = Position.Invalid;
        var count = 0;

        do
        {
            if (count > tries)
                break;
            pos = new Position(GameRandom.NextInclusive(0, Width - 1), GameRandom.NextInclusive(0, Height - 1));
            count++;
        } while (!IsCellWalkable(pos));

        return pos;
    }

    public Position FindWalkableCellOnMap()
    {
        //we assume we will find a walkable cell on a map, so we have no max attempts specified.
        //If we have a map with no of few walkable cells this could hard lock the server however.

        var pos = Position.Invalid;

        do
        {
            pos = Bounds.Shrink(4, 4).RandomInArea();
        } while (!IsCellWalkable(pos));

        return pos;
    }

    public bool FindWalkableCellInArea(Area area, out Position p, int maxAttempts = 100)
    {
        p = new Position();

        area = area.ClipAndNormalize(Bounds);

        var attempt = 0;

        do
        {
            if(area.MaxX < area.MinX || area.MaxY < area.MinY)
                ServerLogger.LogError("WAAAA");

            p = new Position(GameRandom.NextInclusive(area.MinX, area.MaxX), GameRandom.NextInclusive(area.MinY, area.MaxY));
            if (attempt > maxAttempts)
                return false;
            attempt++;
        } while (!IsCellWalkable(p));

        return true;
    }

    /// <summary>
    /// Starting from a random point within an area, scans tiles one at a time until a walkable tile is found.
    /// </summary>
    /// <param name="area">The area you want to find a walkable cell in.</param>
    /// <param name="p">The output area.</param>
    /// <returns>If a walkable tile was found.</returns>
    public bool ScanForWalkableCell(Area area, out Position p)
    {
        area = area.ClipAndNormalize(Bounds);

        var max = area.Width * area.Height;
        var start = GameRandom.NextInclusive(0, max - 1);
        var pos = start + 1;

        while (start != pos)
        {
            pos = pos % max;

            var x = pos % area.Width;
            var y = (pos - x) / area.Height;

            if (IsCellWalkable(x, y))
            {
                p = new Position(x, y);
                return true;
            }

            pos++;
        }

        p = Position.Invalid;
        return false;
    }


    public Position CalcKnockbackFromPosition(Position chPos, Position attackSrc, int maxDist)
    {
        var x0 = chPos.X;
        var y0 = chPos.Y;
        var x1 = chPos.X + ((chPos.X - attackSrc.X) * 50);
        var y1 = chPos.Y + ((chPos.Y - attackSrc.Y) * 50);

        //algorithm from https://rosettacode.org/wiki/Bitmap/Bresenham%27s_line_algorithm#C.23
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = (dx > dy ? dx : -dy) / 2;
        var target = chPos;
        //var dist = 0;
        for (; ; )
        {
            if (x0 == x1 && y0 == y1) break;
            if (!IsCellWalkable(x0, y0))
                return target;
            target = new Position(x0, y0);
            var e2 = err;
            if (e2 > -dx) { err -= dy; x0 += sx; }
            if (e2 < dy) { err += dx; y0 += sy; }
            if(chPos.DistanceTo(target) >= maxDist) return target;
        }

        return target;
    }

    public bool HasLineOfSight(Position pos1, Position pos2)
    {
        var x0 = pos1.X;
        var y0 = pos1.Y;
        var x1 = pos2.X;
        var y1 = pos2.Y;

        //algorithm from https://rosettacode.org/wiki/Bitmap/Bresenham%27s_line_algorithm#C.23
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = (dx > dy ? dx : -dy) / 2;
        for (; ; )
        {
            if (x0 == x1 && y0 == y1) break;
            if (DoesCellBlockLos(x0, y0))
                return false;
            var e2 = err;
            if (e2 > -dx) { err -= dy; x0 += sx; }
            if (e2 < dy) { err += dx; y0 += sy; }
        }

        return true;
    }

    public MapWalkData(string name)
    {
        var walkPath = ServerConfig.DataConfig.WalkPathData;
        if (string.IsNullOrEmpty(walkPath))
            throw new Exception("Configuration did not include a valid WalkPathData value!");

        var path = Path.Combine(walkPath, name);


        //ServerLogger.Log("Loading path data from " + name);

        try
        {
            using var fs = new FileStream(path, FileMode.Open);
            using var br = new BinaryReader(fs);

            Width = br.ReadInt32();
            Height = br.ReadInt32();

            Bounds = new Area(0, 0, Width - 1, Height - 1);

            cellData = br.ReadBytes(Width * Height);
        }
        catch (Exception)
        {
            ServerLogger.LogError($"Failed to load map walk data for file {path}");

            Width = 1024; //unreasonably large, but there'll be errors if it isn't big enough.
            Height = 1024;

            Bounds = new Area(0, 0, Width - 1, Height - 1);
            cellData = new byte[Width * Height];
            for (var i = 0; i < Width * Height; i++)
                cellData[i] = (byte)CellType.Walkable;

            //throw;
        }
    }
}