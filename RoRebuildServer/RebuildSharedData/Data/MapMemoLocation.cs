using RebuildSharedData.Util;

namespace RebuildSharedData.Data;

public struct MapMemoLocation
{
    public string? MapName;
    public Position Position;

    public static MapMemoLocation DeSerialize(IBinaryMessageReader br)
    {
        var location = new MapMemoLocation();
        if (br.ReadByte() == 1)
        {
            location.MapName = br.ReadString();
            location.Position = new Position(br.ReadInt16(), br.ReadInt16());
        }
        
        return location;
    }
    
    public void Serialize(IBinaryMessageWriter bw)
    {
        if(string.IsNullOrWhiteSpace(MapName))
            bw.Write((byte)0);
        else
        {
            bw.Write((byte)1);
            bw.Write(MapName);
            bw.Write((short)Position.X);
            bw.Write((short)Position.Y);
        }
    }
}