using Microsoft.EntityFrameworkCore;

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