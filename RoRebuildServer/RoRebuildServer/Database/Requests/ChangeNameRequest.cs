using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Database.Requests;

//the player takes the new name once IsComplete is set
public class ChangeNameRequest : IDbRequest
{
    public NetworkConnection Connection;
    public Guid CharacterId;
    public string NewName;
    public bool IsComplete;
    public bool HasFailed;

    public ChangeNameRequest(NetworkConnection connection, Guid characterId, string newName)
    {
        Connection = connection;
        CharacterId = characterId;
        NewName = newName;
    }

    private void FailureResult(string clientMessage)
    {
        HasFailed = true;
        IsComplete = true;
        CommandBuilder.ErrorMessage(Connection, clientMessage);
    }

    public async Task ExecuteAsync(RoContext dbContext)
    {
        var doesNameExist = await DbHelper.IsCharacterNameTaken(dbContext, NewName, CharacterId);
        if (doesNameExist)
        {
            FailureResult("A character with that name already exists.");
            return;
        }

        try
        {
            await dbContext.Character.Where(c => c.Id == CharacterId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, NewName));
        }
        catch (Exception e)
        {
            ServerLogger.LogWarning($"Change name failed due to exception. Exception: " + e.Message);
            FailureResult("A character with that name already exists.");
            return;
        }

        IsComplete = true;
    }
}
