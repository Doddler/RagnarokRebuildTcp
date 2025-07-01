using RoRebuildServer.EntityComponents;
using System.Numerics;
using RoRebuildServer.EntityComponents.Items;
using System.Buffers;
using RebuildSharedData.Util;
using RoRebuildServer.Logging;
using System.Diagnostics;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Encoders;
using RebuildSharedData.ClientTypes;
using RoRebuildServer.Database.Requests;
using System;
using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RoRebuildServer.Database.Utility;

//the thinking behind compressing player inventory data is that because we will be forced to allocate
//byte arrays when storing data to the database as we can't guarantee the array pool won't give us
//a ginormous array... so we may as well limit it's size. It's not like we can work with byte arrays
//in the database directly anyway.

public static class PlayerDataDbHelper
{
    public const int CurrentPlayerSaveVersion = 6;
    
    public static void PackPlayerSummaryData(int[] buffer, Player p)
    {
        buffer[(int)PlayerSummaryData.Level] = p.GetData(PlayerStat.Level);
        buffer[(int)PlayerSummaryData.JobId] = p.GetData(PlayerStat.Job);
        buffer[(int)PlayerSummaryData.HeadId] = p.GetData(PlayerStat.Head);
        buffer[(int)PlayerSummaryData.HairColor] = p.GetData(PlayerStat.HairId);
        buffer[(int)PlayerSummaryData.Hp] = p.GetStat(CharacterStat.Hp);
        buffer[(int)PlayerSummaryData.MaxHp] = p.GetStat(CharacterStat.MaxHp);
        buffer[(int)PlayerSummaryData.Sp] = p.GetStat(CharacterStat.Sp);
        buffer[(int)PlayerSummaryData.MaxSp] = p.GetStat(CharacterStat.MaxSp);
        buffer[(int)PlayerSummaryData.Headgear1] = p.Equipment.ItemIds[(int)EquipSlot.HeadTop];
        buffer[(int)PlayerSummaryData.Headgear2] = p.Equipment.ItemIds[(int)EquipSlot.HeadMid];
        buffer[(int)PlayerSummaryData.Headgear3] = p.Equipment.ItemIds[(int)EquipSlot.HeadBottom];
        buffer[(int)PlayerSummaryData.Str] = p.GetData(PlayerStat.Str);
        buffer[(int)PlayerSummaryData.Agi] = p.GetData(PlayerStat.Agi);
        buffer[(int)PlayerSummaryData.Int] = p.GetData(PlayerStat.Int);
        buffer[(int)PlayerSummaryData.Vit] = p.GetData(PlayerStat.Vit);
        buffer[(int)PlayerSummaryData.Dex] = p.GetData(PlayerStat.Dex);
        buffer[(int)PlayerSummaryData.Luk] = p.GetData(PlayerStat.Luk);
        buffer[(int)PlayerSummaryData.Gender] = p.GetData(PlayerStat.Gender);
    }


    public static void PackNewCharacterSummaryData(int[] buffer, int gender, int headId, int hairColor)
    {
        var maxHp = DataManager.JobMaxHpLookup[0][1];
        buffer[(int)PlayerSummaryData.Level] = 1;
        buffer[(int)PlayerSummaryData.JobId] = 0;
        buffer[(int)PlayerSummaryData.HeadId] = headId;
        buffer[(int)PlayerSummaryData.HairColor] = hairColor;
        buffer[(int)PlayerSummaryData.Hp] = maxHp;
        buffer[(int)PlayerSummaryData.MaxHp] = maxHp;
        buffer[(int)PlayerSummaryData.Gender] = gender;
    }

    public static byte[] CompressData(byte[] array, int size)
    {
        var cmpBuffer = ArrayPool<byte>.Shared.Rent(LZ4Codec.MaximumOutputSize(size));
        var srcData = array.AsSpan(0, size);
        
        var compressedSize = LZ4Codec.Encode(srcData, cmpBuffer);
        var bytesOut = new byte[compressedSize]; //can't avoid allocation here sadly ;_;
        Buffer.BlockCopy(cmpBuffer, 0, bytesOut, 0, compressedSize);
        
        ArrayPool<byte>.Shared.Return(cmpBuffer);
        
        return bytesOut;
    }


    public static byte[] DecompressData(byte[] array, int uncompressedSize)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(uncompressedSize);
        LZ4Codec.Decode(array, buffer);

        return buffer;
    }

    public static byte[]? CompressAndStorePlayerInventoryData(Player p, out int uncompressedSize)
    {
        var inventorySize = p.Inventory.TryGetSize() + p.CartInventory.TryGetSize();
        uncompressedSize = 0;
        if(inventorySize == 0)
            return null;

        inventorySize += 1 + p.Equipment.GetExpectedSerializedSize(); //the + 1 is for the version byte

        var buffer = ArrayPool<byte>.Shared.Rent(inventorySize);
        using var ms = new MemoryStream(buffer);
        using var bw = new BinaryMessageWriter(ms);
        byte[]? bytesOut = null;
        byte[]? cmpBuffer = null;

        try
        {
            bw.Write((byte)0); //version
            p.Inventory.TryWrite(bw, false);
            if (CurrentPlayerSaveVersion != 4)
                p.CartInventory.TryWrite(bw, false); //starting v4 cart will be loaded separately
            p.Equipment.Serialize(bw);

            //we can come in under, the equipment array might not all be used. We should never, however, go above, or we risk crashing the server
            if (ms.Position > inventorySize)
                ServerLogger.LogWarning(
                    $"SaveCharacterRequest allocated a buffer size of {inventorySize} bytes, but it used {ms.Position} bytes instead (over by {ms.Position - inventorySize})");

            uncompressedSize = (int)ms.Position;
            bytesOut = CompressData(buffer, uncompressedSize);
            
        }
        catch (Exception e)
        {
            ServerLogger.LogError(
                $"Failed to save character data for player {p}. Our rented buffer was {buffer.Length} in size and we expected an inventory size of {inventorySize} bytes, but it was insufficient.");
            ServerLogger.LogError(e.ToString());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if(cmpBuffer != null)
                ArrayPool<byte>.Shared.Return(cmpBuffer);
        }

        return bytesOut;
    }

    public static void DecompressPlayerInventoryData(LoadCharacterRequest req, byte[] inventoryData, int uncompressedSize, int saveVersion)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(uncompressedSize);
        LZ4Codec.Decode(inventoryData, buffer);

        using var ms = new MemoryStream(buffer);
        using var br = new BinaryMessageReader(ms);
        var version = br.ReadByte();
        req.Inventory = CharacterBag.TryRead(br);
        if(saveVersion != 4)
            req.Cart = CharacterBag.TryRead(br);
        //req.Storage = CharacterBag.TryRead(br);

        if (req.Inventory != null)
        {
            req.EquipState = new ItemEquipState();
            req.EquipState.DeSerialize(br, req.Inventory);
        }

        ArrayPool<byte>.Shared.Return(buffer);
    }

    public static void LoadVersion0PlayerInventoryData(LoadCharacterRequest req, DbCharacter ch)
    {
        using var ms = new MemoryStream(ch.ItemData!);
        using var br = new BinaryMessageReader(ms);
        req.Inventory = CharacterBag.TryRead(br);
        req.Cart = CharacterBag.TryRead(br);
        br.ReadByte(); //flag for storage, but version 0 it will always be empty

        if (req.Inventory != null)
        {
            req.EquipState = new ItemEquipState();
            req.EquipState.DeSerialize(br, req.Inventory);
        }
    }

    public static byte[] CompressAndStorePlayerStorage(Player p, out int uncompressedSize)
    {
        Debug.Assert(p.StorageInventory != null);

        uncompressedSize = 1 + p.StorageInventory.GetByteSize();
        var buffer = ArrayPool<byte>.Shared.Rent(uncompressedSize);
        using var ms = new MemoryStream(buffer);
        using var bw = new BinaryMessageWriter(ms);
        
        bw.Write((byte)0); //version
        p.StorageInventory.TryWrite(bw, false);
        
        var srcData = buffer.AsSpan(0, (int)ms.Position);
        var cmpData = ArrayPool<byte>.Shared.Rent(LZ4Codec.MaximumOutputSize(srcData.Length));
        var compressedSize = LZ4Codec.Encode(srcData, cmpData);
        var byteOut = new byte[compressedSize];
        Buffer.BlockCopy(cmpData, 0, byteOut, 0, compressedSize);
        
        ArrayPool<byte>.Shared.Return(buffer);
        ArrayPool<byte>.Shared.Return(cmpData);

        return byteOut;
    }

    public static CharacterBag? DecompressPlayerStorage(byte[] storageData, int uncompressedSize)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(uncompressedSize);
        LZ4Codec.Decode(storageData, buffer);

        using var ms = new MemoryStream(buffer);
        using var br = new BinaryMessageReader(ms);
        var version = br.ReadByte();
        var bagData = CharacterBag.TryRead(br);
        
        ArrayPool<byte>.Shared.Return(buffer);

        return bagData;
    }

    public static byte[] StorePlayerDataForDatabaseUse(Player player, out int decompressedSize)
    {
        var charData = player.CharData;

        var dataLen = 32;
        dataLen += charData.Length * sizeof(int);
        dataLen += player.NpcFlags == null ? 4 : player.NpcFlags.Count * 8;
        dataLen += player.LearnedSkills.Count * 32;
        dataLen += player.CombatEntity.StatusContainer == null ? 8 : player.CombatEntity.StatusContainer.TotalStatusEffectCount * 16;
        dataLen += 32 * 4; //space for map memos

        var chData = ArrayPool<byte>.Shared.Rent(dataLen);
        var ms = new MemoryStream(chData);
        var bw = new BinaryMessageWriter(ms);

        bw.Write((short)charData.Length);
        var dataSize = charData.Length * sizeof(int);
        Buffer.BlockCopy(charData, 0, chData, (int)ms.Position, dataSize);
        bw.Seek(dataSize, SeekOrigin.Current); //skip ahead

        DbHelper.WriteDictionaryWithEnumStringKeys(bw, player.LearnedSkills);
        //DbHelper.WriteDictionary(bw, player.LearnedSkills);
        DbHelper.WriteDictionary(bw, player.NpcFlags);

        //status effects
        var effectPos = (int)ms.Position;
        bw.Write((byte)0); //size, we'll update this later as we don't know which status effects we're actually saving
        bw.Write((short)CharacterStatusEffect.StatusEffectMax);
        var len = player.CombatEntity.StatusContainer?.Serialize(bw) ?? 0;
        decompressedSize = (int)ms.Position;

        if (len > 0)
        {
            bw.Seek(effectPos, SeekOrigin.Begin);
            bw.Write((byte)len); //jump back to where the size is written and update it
            bw.Seek(decompressedSize, SeekOrigin.Begin);
        }

        for (var i = 0; i < 4; i++)
            player.MemoLocations[i].Serialize(bw);

        decompressedSize = (int)ms.Position;
        
        //compress player data
        var data = PlayerDataDbHelper.CompressData(chData, decompressedSize);
        ArrayPool<byte>.Shared.Return(chData);
        
        return data;
    }

    public static byte[] StoreNewPlayerDataForDatabase(int[] charData, out int decompressedSize)
    {
        var realCharDataLen = (int)PlayerStat.PlayerStatsMax;
        
        var dataLen = 32;
        dataLen += realCharDataLen * sizeof(int);

        var chData = ArrayPool<byte>.Shared.Rent(dataLen);
        var ms = new MemoryStream(chData);
        var bw = new BinaryMessageWriter(ms);
        
        bw.Write((short)realCharDataLen);
        var dataSize = realCharDataLen * sizeof(int);
        Buffer.BlockCopy(charData, 0, chData, (int)ms.Position, dataSize);
        bw.Seek(dataSize, SeekOrigin.Current); //skip ahead

        DbHelper.WriteDictionaryWithEnumStringKeys<CharacterSkill>(bw, null);
        DbHelper.WriteDictionary(bw, null);
        bw.Write((byte)0); //status effects
        bw.Write(0); //4 bytes for 4 empty warp destinations

        decompressedSize = (int)ms.Position;

        //compress player data
        var data = PlayerDataDbHelper.CompressData(chData, decompressedSize);
        ArrayPool<byte>.Shared.Return(chData);

        return data;
    }
    
    public static void RestorePlayerDataFromDatabaseV3(byte[] bytes, int decompressedSize, Player player, int saveVersion)
    {
        var data = DecompressData(bytes, decompressedSize);

        var ms = new MemoryStream(data);
        var br = new BinaryMessageReader(ms);

        var dataLen = (int)br.ReadInt16() * 4;
        if (dataLen <= player.CharData.Length * 4) //we can add fields, but if we take them away it's all bad
            Buffer.BlockCopy(data, (int)ms.Position, player.CharData, 0, dataLen);
        else
            ServerLogger.LogWarning($"Player '{player.Name}' character data does not match the expected size. Player will be loaded with default data.");
        
        ms.Seek(dataLen, SeekOrigin.Current);

        if (saveVersion <= 3)
        {
            player.LearnedSkills = DbHelper.ReadDictionary<CharacterSkill>(br) ?? new Dictionary<CharacterSkill, int>();
            player.LearnedSkills.Clear(); //skill ids have changed, reset (we will save them as strings next time they save
        }
        else
            player.LearnedSkills = DbHelper.ReadDictionaryWithEnumStringKeys<CharacterSkill>(br) ?? new Dictionary<CharacterSkill, int>();
        player.NpcFlags = DbHelper.ReadDictionary(br);
        player.CombatEntity.TryDeserializeStatusContainer(br, saveVersion);

        for (var i = 0; i < 4; i++)
        {
            if (ms.Position >= decompressedSize)
                break; //backwards compatibility with characters saved with none or too few warp slots
            player.MemoLocations[i] = MapMemoLocation.DeSerialize(br);
        }

        ArrayPool<byte>.Shared.Return(data);
    }
}