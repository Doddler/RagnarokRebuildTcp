using System;
using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RebuildZoneServer.Networking;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.Utility;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Database.Requests;

public class SaveCharacterRequest : IDbRequest
{
    public Guid Id;
    public int AccountId;
    private readonly string name;
    private readonly string? map;
    private readonly Position pos;
    private readonly SavePosition savePoint;
    private byte[]? data;
    private byte[]? skillData;
    private byte[]? npcData;
    private byte[]? itemData;
    private byte[]? summaryData;
    private int slot;
    private int itemDataSize;

    public SaveCharacterRequest(string newCharacterName, int accountId)
    {
        AccountId = accountId;
        Id = Guid.Empty; //database will generate a new key for us
        name = newCharacterName;
        map = null;
        pos = Position.Invalid;
        savePoint = new SavePosition();
        skillData = null;
        data = null;
        npcData = null;
        itemData = null;
        summaryData = null;
        itemDataSize = 0;
    }

    public SaveCharacterRequest(Player player)
    {
        var character = player.Character;

        AccountId = player.Connection.AccountId;
        Id = player.Id;
        name = player.Name;
        map = character.Map?.Name;
        pos = character.Position;
        savePoint = player.SavePosition;
        slot = player.CharacterSlot;

        var charData = player.CharData;

        //we should reuse this, char data array never changes size
        if (data == null || data.Length != charData.Length * sizeof(int))
            data = new byte[charData.Length * sizeof(int)];

        Buffer.BlockCopy(charData, 0, data, 0, charData.Length * sizeof(int));

        skillData = DbHelper.BorrowArrayAndWriteDictionary(player.LearnedSkills);
        npcData = DbHelper.BorrowArrayAndWriteDictionary(player.NpcFlags);
        summaryData = ArrayPool<byte>.Shared.Rent((int)PlayerSummaryData.SummaryDataMax * sizeof(int));
        Array.Clear(summaryData);

        var summary = ArrayPool<int>.Shared.Rent((int)PlayerSummaryData.SummaryDataMax);
        PlayerDataDbHelper.PackPlayerSummaryData(summary, player);
        Buffer.BlockCopy(summary, 0, summaryData, 0, (int)PlayerSummaryData.SummaryDataMax * sizeof(int));
        ArrayPool<int>.Shared.Return(summary);

        itemData = PlayerDataDbHelper.CompressAndStorePlayerInventoryData(player, out itemDataSize);
    }

    public async Task ExecuteAsync(RoContext dbContext)
    {
        var ch = new DbCharacter()
        {
            Id = Id,
            AccountId = AccountId,
            Name = name,
            Map = map,
            X = pos.X,
            Y = pos.Y,
            Data = data,
            CharacterSlot = slot,
            SavePoint = new DbSavePoint()
            {
                MapName = savePoint.MapName,
                X = savePoint.Position.X,
                Y = savePoint.Position.Y,
                Area = savePoint.Area,
            },
            SkillData = skillData,
            NpcFlags = npcData,
            CharacterSummary = summaryData,
            ItemData = itemData,
            ItemDataLength = itemDataSize,
            VersionFormat = 1
        };
        
        dbContext.Update(ch);
        await dbContext.SaveChangesAsync();

        if (skillData != null) ArrayPool<byte>.Shared.Return(skillData);
        if (npcData != null) ArrayPool<byte>.Shared.Return(npcData);
        if (summaryData != null) ArrayPool<byte>.Shared.Return(summaryData);

        skillData = null;
        data = null; //we didn't borrow the itemData array so we don't return it

        Id = ch.Id;
    }
}