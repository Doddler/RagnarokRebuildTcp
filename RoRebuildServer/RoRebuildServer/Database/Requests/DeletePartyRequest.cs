using Microsoft.EntityFrameworkCore;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Parties;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RoRebuildServer.Database.Requests
{
    public class DeletePartyRequest(int partyId) : IDbRequest
    {
        private int partyId = partyId;

        public async Task ExecuteAsync(RoContext dbContext)
        {
            //this is probably not necessary?
            await dbContext.Character.Where(u => u.PartyId == partyId)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(p => p.PartyId, (int?)null)
                );

            await dbContext.Parties.Where(p => p.Id == partyId).ExecuteDeleteAsync();
        }
    }
}
