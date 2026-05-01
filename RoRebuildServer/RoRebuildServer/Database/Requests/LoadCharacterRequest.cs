using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Database.Utility;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Parties;

namespace RoRebuildServer.Database.Requests;

public class PartyLoadResult
{
    public int PartyId;
    public required string PartyName;
    public Guid? OwnerId;
    public List<PartyLoadCharacter> Characters = null!; //ef core won't give us back a null object so we can suppress the nullability warning
}

public class PartyLoadCharacter
{
    public Guid Id;
    public string Name = null!;
}

public class LoadCharacterRequest : IDbRequest
{
    public readonly int AccountId;
    public Guid Id;
    public int CharacterSlot;
    public string Name;
    public string? Map;
    public Position Position;
    public SavePosition SavePosition;
    public Dictionary<CharacterSkill, int>? SkillsLearned;
    public Dictionary<string, int>? NpcFlags;
    public CharacterBag? Inventory;
    public CharacterBag? Cart;
    public ItemEquipState? EquipState;
    public Party? Party;

    public byte[]? Data;
    public bool HasCharacter;
    public int DataLength;
    public int SaveVersion;

    public LoadCharacterRequest(int accountId, string character)
    {
        AccountId = accountId;
        Name = character;
        Map = string.Empty;
        Position = Position.Invalid;
        SavePosition = new SavePosition();
        EquipState = null;
    }

    public async Task ExecuteAsync(RoContext dbContext)
    {
        try
        {
            var ch = await dbContext.Character.AsNoTracking().FirstOrDefaultAsync(c => c.AccountId == AccountId && c.Name == Name);
            if (!NetworkManager.ConnectedAccounts.TryGetValue(AccountId, out var connection))
                return;

            if (ch == null)
            {
                CommandBuilder.ErrorMessage(connection, $"Failed to log in on character {Name}.");
                HasCharacter = false;
                return;
            }

            Name = ch.Name;
            Id = ch.Id;
            if (ch.Map != null)
                Map = ch.Map;
            Position = new Position(ch.X, ch.Y);
            Data = ch.Data;
            DataLength = ch.DataLength;
            CharacterSlot = ch.CharacterSlot;
            SaveVersion = ch.VersionFormat;

            //party stuff
            if (ch.PartyId != null)
            {
                if (World.Instance.TryFindPartyById(ch.PartyId.Value, out var party))
                    Party = party;
                else
                {
                    var dbParty = await dbContext.Parties.AsNoTracking().Where(p => p.Id == ch.PartyId).Include(p => p.Characters)
                        .Select(p => new PartyLoadResult()
                        {
                            PartyId = p.Id,
                            PartyName = p.PartyName,
                            Characters = p.Characters.Where(m => m.PartyId == p.Id).Select(m => new PartyLoadCharacter() { Id = m.Id, Name = m.Name }).ToList()
                        }).FirstOrDefaultAsync();
                    if (dbParty != null)
                    {
                        Party = new Party(dbParty);
                        if (!World.Instance.TryAddParty(Party)) //if another query somehow beat us to the punch
                            if (World.Instance.TryFindPartyById(ch.PartyId.Value, out party))
                                Party = party;
                    }
                }
            }

            //save point
            if (ch.SavePoint != null)
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(ch.SavePoint.MapName), "Map name should never be empty");

                SavePosition = new SavePosition() { MapName = ch.SavePoint.MapName, Position = new Position(ch.SavePoint.X, ch.SavePoint.Y), Area = ch.SavePoint.Area };
            }
            else
                SavePosition = new SavePosition();

            //load skills and flags
            if (SaveVersion < 3)
            {
                var skills = DbHelper.ReadDictionary<CharacterSkill>(ch.SkillData);
                SkillsLearned = skills ?? new Dictionary<CharacterSkill, int>();

                NpcFlags = DbHelper.ReadDictionary(ch.NpcFlags);
            }

            //load items
            if (ch.ItemData != null)
            {
                if (ch.VersionFormat == 0)
                    PlayerDataDbHelper.LoadVersion0PlayerInventoryData(this, ch);
                else
                {
                    if (ch.ItemDataLength > 0)
                        PlayerDataDbHelper.DecompressPlayerInventoryData(this, ch.ItemData, ch.ItemDataLength, SaveVersion);
                }
            }


            HasCharacter = true;

            World.Instance.FinalizeEnterServer(this, connection);
        }
        catch (Exception ex)
        {
            ServerLogger.LogError($"Failed to perform LoadCharacterRequest: {ex}");
            throw;
        }
    }
}