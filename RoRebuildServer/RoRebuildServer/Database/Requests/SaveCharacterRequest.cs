using System.Buffers;
using System.Drawing;
using System.Numerics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RebuildZoneServer.Networking;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Logging;

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
    private byte[]? npcData;
    private byte[]? itemData;

    public SaveCharacterRequest(string newCharacterName)
    {
        Id = Guid.Empty; //database will generate a new key for us
        name = newCharacterName;
        map = null;
        pos = Position.Invalid;
        savePoint = new SavePosition();
        skillData = null;
        data = null;
        npcData = null;
        itemData = null;
    }

    public SaveCharacterRequest(Player player)
    {
        var character = player.Character;

        Id = player.Id;
        name = player.Name;
        map = character.Map?.Name;
        pos = character.Position;
        savePoint = player.SavePosition;

        var charData = player.CharData;

        //we can reuse this, char data array never changes size
        if (data == null || data.Length != charData.Length * sizeof(int))
            data = new byte[charData.Length * sizeof(int)];

        Buffer.BlockCopy(charData, 0, data, 0, charData.Length * sizeof(int));

        skillData = DbHelper.BorrowArrayAndWriteDictionary(player.LearnedSkills);
        npcData = DbHelper.BorrowArrayAndWriteDictionary(player.NpcFlags);

        var inventorySize = player.Inventory.TryGetSize() + player.CartInventory.TryGetSize() + player.StorageInventory.TryGetSize();
        if (inventorySize > 0)
        {
            //add 3 bytes since each bag will also write if it exists or not
            var buffer = ArrayPool<byte>.Shared.Rent(inventorySize + 3); 
            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryMessageWriter(ms);
            player.Inventory.TryWrite(bw, false);
            player.CartInventory.TryWrite(bw, false);
            player.StorageInventory.TryWrite(bw, false);

            itemData = buffer;
        }
        else
            itemData = null;
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
            SkillData = skillData,
            NpcFlags = npcData,
            ItemData = itemData
        };

        dbContext.Update(ch);
        await dbContext.SaveChangesAsync();

        if (skillData != null) ArrayPool<byte>.Shared.Return(skillData);
        if (npcData != null) ArrayPool<byte>.Shared.Return(npcData);
        if (itemData != null) ArrayPool<byte>.Shared.Return(itemData);

        skillData = null;
        data = null;

        Id = ch.Id;
    }
}