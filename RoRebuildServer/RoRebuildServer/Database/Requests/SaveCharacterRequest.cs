using System.Buffers;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Database.Requests;

public class SaveCharacterRequest : IDbRequest
{
    public Guid Id;
    private readonly string name;
    private readonly string? map;
    private readonly Position pos;
    private readonly SavePosition savePoint;
    private byte[]? data;
    private byte[]? skillData;

    public SaveCharacterRequest(Guid id, string name, string? map, Position pos, int[] charData, SavePosition savePoint, Dictionary<CharacterSkill, int>? skills)
    {
        Id = id;
        this.name = name;
        this.map = map;
        this.pos = pos;
        this.savePoint = savePoint;

        //we can reuse this, char data array never changes size
        if (data == null || data.Length != charData.Length * sizeof(int))
            data = new byte[charData.Length * sizeof(int)];

        Buffer.BlockCopy(charData, 0, data, 0, charData.Length * sizeof(int));

        if (skills == null)
        {
            skillData = null;
            return;
        }

        skillData = ArrayPool<byte>.Shared.Rent(skills.Count * 3 + 1);
        using var ms = new MemoryStream(skillData);
        using var bw = new BinaryWriter(ms);
        bw.Write((byte)skills.Count);
        foreach (var skill in skills)
        {
            bw.Write((short)skill.Key);
            bw.Write((byte)skill.Value);
        }
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
            Data = data,
            SavePoint = new DbSavePoint()
            {
                MapName = savePoint.MapName,
                X = savePoint.Position.X,
                Y = savePoint.Position.Y,
                Area = savePoint.Area,
            },
            SkillData = skillData
        };

        dbContext.Update(ch);
        await dbContext.SaveChangesAsync();

        if (skillData != null) ArrayPool<byte>.Shared.Return(skillData);
        //if(data != null) ArrayPool<byte>.Shared.Return(data);
        skillData = null;
        data = null;

        Id = ch.Id;
    }
}