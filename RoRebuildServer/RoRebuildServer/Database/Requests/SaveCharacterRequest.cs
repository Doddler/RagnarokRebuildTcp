using System.Buffers;
using RebuildSharedData.Data;
using RebuildSharedData.Util;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Database.Utility;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.Util;

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

    //private byte[]? skillData;
    //private byte[]? npcData;
    private byte[]? itemData;
    private byte[]? summaryData;
    private int slot;
    private int itemDataSize;
    private int dataLength;
    private int? partyId;

    public SaveCharacterRequest(string newCharacterName, int accountId)
    {
        AccountId = accountId;
        Id = Guid.Empty; //database will generate a new key for us
        name = newCharacterName;
        map = null;
        pos = Position.Invalid;
        savePoint = new SavePosition();
        //skillData = null;
        data = null;
        //npcData = null;
        itemData = null;
        summaryData = null;
        itemDataSize = 0;
        dataLength = 0;
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

        partyId = player.Party?.PartyId;

        //store player data (data, npc flags, learned skills, status effects)
        data = PlayerDataDbHelper.StorePlayerDataForDatabaseUse(player, out dataLength);

        //create character summary
        summaryData = ByteArrayPools.ArrayPoolPlayerSummary.Get();
        Array.Clear(summaryData);

        var summary = ArrayPool<int>.Shared.Rent((int)PlayerSummaryData.SummaryDataMax);
        PlayerDataDbHelper.PackPlayerSummaryData(summary, player);
        Buffer.BlockCopy(summary, 0, summaryData, 0, (int)PlayerSummaryData.SummaryDataMax * sizeof(int));
        ArrayPool<int>.Shared.Return(summary);

        //inventory data
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
            DataLength = dataLength,
            CharacterSlot = slot,
            SavePoint = new DbSavePoint()
            {
                MapName = savePoint.MapName,
                X = savePoint.Position.X,
                Y = savePoint.Position.Y,
                Area = savePoint.Area,
            },
            CharacterSummary = summaryData,
            ItemData = itemData,
            ItemDataLength = itemDataSize,
            SkillData = null,
            NpcFlags = null,
            SkillDataLength = 0,
            NpcFlagsLength = 0,
            PartyId = partyId,
            VersionFormat = PlayerDataDbHelper.CurrentPlayerSaveVersion
        };

        dbContext.Update(ch);
        await dbContext.SaveChangesAsync();

        if (summaryData != null) ByteArrayPools.ArrayPoolPlayerSummary.Return(summaryData);

        data = null; //we didn't borrow the itemData array so we don't return it

        Id = ch.Id;
    }
}