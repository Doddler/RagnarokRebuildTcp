using Microsoft.EntityFrameworkCore;
using RebuildSharedData.Data;

namespace RoRebuildServer.Database.Requests;

public class LoadCharacterRequest : IDbRequest
{
    public readonly Guid Id;
    public string Name;
    public string Map;
    public Position Position;
    public byte[]? Data;
    public bool HasCharacter;

    public LoadCharacterRequest(Guid id)
    {
        this.Id = id;
        Name = string.Empty;
        Map = string.Empty;
        Position = Position.Invalid;
    }

    public async Task ExecuteAsync(RoContext dbContext)
    {
        var ch = await dbContext.Character.FirstOrDefaultAsync(c => c.Id == Id);
        if (ch == null)
        {
            HasCharacter = false;
            return;
        }

        Name = ch.Name;
        if(ch.Map != null)
            Map = ch.Map;
        Position = new Position(ch.X, ch.Y);
        Data = ch.Data;
        HasCharacter = true;
    }
}