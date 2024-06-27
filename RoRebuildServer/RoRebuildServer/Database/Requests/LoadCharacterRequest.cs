using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Database.Requests;

public class LoadCharacterRequest : IDbRequest
{
    public readonly Guid Id;
    public string Name;
    public string Map;
    public Position Position;
    public SavePosition SavePosition;
    public Dictionary<CharacterSkill, int> SkillsLearned;

    public byte[]? Data;
    public bool HasCharacter;

    public LoadCharacterRequest(Guid id)
    {
        this.Id = id;
        Name = string.Empty;
        Map = string.Empty;
        Position = Position.Invalid;
        SavePosition = null!;
    }

    public async Task ExecuteAsync(RoContext dbContext)
    {
        try
        {
            var ch = await dbContext.Character.FirstOrDefaultAsync(c => c.Id == Id);
            if (ch == null)
            {
                HasCharacter = false;
                return;
            }

            Name = ch.Name;
            if (ch.Map != null)
                Map = ch.Map;
            Position = new Position(ch.X, ch.Y);
            Data = ch.Data;
            if (ch.SavePoint != null)
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(ch.SavePoint.MapName), "Map name should never be empty");

                SavePosition = new SavePosition()
                {
                    MapName = ch.SavePoint.MapName,
                    Position = new Position(ch.SavePoint.X, ch.SavePoint.Y),
                    Area = ch.SavePoint.Area
                };
            }
            else
                SavePosition = new SavePosition();

            SkillsLearned = new Dictionary<CharacterSkill, int>();
            if (ch.SkillData != null && ch.SkillData.Length > 0)
            {
                using var ms = new MemoryStream(ch.SkillData);
                using var br = new BinaryReader(ms);
                var count = br.ReadByte();
                for (var i = 0; i < count; i++)
                    SkillsLearned.Add((CharacterSkill)br.ReadInt16(), br.ReadByte());
            }

            HasCharacter = true;
        }
        catch (Exception ex)
        {
            ServerLogger.LogError($"Failed to perform LoadCharacterRequest: {ex}");
            throw;
        }
    }
}