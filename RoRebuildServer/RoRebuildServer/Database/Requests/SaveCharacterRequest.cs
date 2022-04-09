using RebuildSharedData.Data;
using RoRebuildServer.Database.Domain;

namespace RoRebuildServer.Database.Requests;

public class SaveCharacterRequest : IDbRequest
{
    public Guid Id;
    private readonly string name;
    private readonly string? map;
    private readonly Position pos;
    private readonly byte[] data;

    public SaveCharacterRequest(Guid id, string name, string? map, Position pos, int[] charData)
    {
        Id = id;
        this.name = name;
        this.map = map;
        this.pos = pos;
        this.data = new byte[charData.Length * sizeof(int)];
        Buffer.BlockCopy(charData, 0, data, 0, charData.Length * sizeof(int));
    }

    public async Task ExecuteAsync(RoContext dbContext)
    {
        var ch = new DbCharacter()
        {
            Id = Id,
            Name = name,
            Map = map,
            X = pos.X,
            Y = pos.Y,
            Data = data
        };

        dbContext.Update(ch);
        await dbContext.SaveChangesAsync();

        Id = ch.Id;
    }
}