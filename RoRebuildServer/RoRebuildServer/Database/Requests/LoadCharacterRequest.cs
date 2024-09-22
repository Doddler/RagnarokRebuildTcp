using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Util;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Database.Requests;

public class LoadCharacterRequest : IDbRequest
{
    public readonly Guid Id;
    public string Name;
    public string Map;
    public Position Position;
    public SavePosition SavePosition;
    public Dictionary<CharacterSkill, int>? SkillsLearned;
    public Dictionary<string, int>? NpcFlags;
    public CharacterBag? Inventory;
    public CharacterBag? Cart;
    public CharacterBag? Storage;
    public ItemEquipState? EquipState;

    public byte[]? Data;
    public bool HasCharacter;

    public LoadCharacterRequest(Guid id)
    {
        this.Id = id;
        Name = string.Empty;
        Map = string.Empty;
        Position = Position.Invalid;
        SavePosition = null!;
        EquipState = null;
    }

    public async Task ExecuteAsync(RoContext dbContext)
    {
        try
        {
            var ch = await dbContext.Character.AsNoTracking().FirstOrDefaultAsync(c => c.Id == Id);
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

            var skills = DbHelper.ReadDictionary<CharacterSkill>(ch.SkillData);
            SkillsLearned = skills ?? new Dictionary<CharacterSkill, int>();

            NpcFlags = DbHelper.ReadDictionary(ch.NpcFlags);

            if (ch.ItemData != null && ch.ItemData.Length >= 3)
            {
                using var ms = new MemoryStream(ch.ItemData);
                using var br = new BinaryMessageReader(ms);
                Inventory = CharacterBag.TryRead(br);
                Cart = CharacterBag.TryRead(br);
                Storage = CharacterBag.TryRead(br);

                if (Inventory != null)
                {
                    EquipState = new ItemEquipState();
                    EquipState.DeSerialize(br, Inventory);
                }
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