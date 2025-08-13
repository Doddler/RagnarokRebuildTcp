using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Parties;

namespace RoRebuildServer.Database.Requests;

public class CreatePartyRequest : IDbRequest
{
    public NetworkConnection Connection;
    public string PartyName;
    public Party? CreatedParty;
    public bool IsComplete;
    public bool HasFailed;
    public int InvitePlayerOnSuccess;

    public CreatePartyRequest(NetworkConnection connection, string partyName)
    {
        Connection = connection;
        PartyName = partyName;
    }

    private void FailureResult(string? clientMessage)
    {
        HasFailed = true;
        IsComplete = true;
        if(!string.IsNullOrWhiteSpace(clientMessage))
            CommandBuilder.ErrorMessage(Connection, clientMessage);
    }

    public async Task ExecuteAsync(RoContext dbContext)
    {
        if (Connection.Character == null || Connection.Player == null || !Connection.IsConnectedAndInGame)
        {
            FailureResult("Could not create party.");
            return;
        }

        if (Connection.Player.Party != null)
        {
            FailureResult("You are already in a party.");
            return;
        }
        
        var partyExists = await dbContext.Parties.AnyAsync(p => p.PartyName == PartyName);
        if (partyExists)
        {
            FailureResult("Failed to create party, a party of that name already exists.");
            return;
        }

        var party = new DbParty { PartyName = PartyName };

        try
        {
            dbContext.Update(party);
            await dbContext.SaveChangesAsync();
            await dbContext.Character.Where(u => u.Id == Connection.Player.Id)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(p => p.PartyId, party.Id)
                );

            CreatedParty = new Party(party, Connection);
            if (!World.Instance.TryAddParty(CreatedParty))
            {
                ServerLogger.LogWarning($"The player {Connection.Player.Name} crated party {CreatedParty.PartyId} but it's somehow already in the party list.");
                FailureResult("A problem occured while creating your party.");
                return;
            }
            Connection.Player.Party = CreatedParty;
            CommandBuilder.NotifyNearbyPlayersOfPartyChangeAutoVis(Connection.Player);
            CommandBuilder.AcceptPartyInvite(Connection.Player);

            IsComplete = true;
        }
        catch(Exception e)
        {
            ServerLogger.LogWarning($"Create character failed due to exception. Exception: " + e.Message);
            FailureResult("Error: Could not create party.");
        }
    }
}