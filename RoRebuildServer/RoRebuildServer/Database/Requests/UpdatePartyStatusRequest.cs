using Microsoft.EntityFrameworkCore;

namespace RoRebuildServer.Database.Requests
{
    public class UpdatePartyStatusRequest(Guid playerId, int partyId) : IDbRequest
    {
        private Guid playerId = playerId;
        private int partyId = partyId;

        public async Task ExecuteAsync(RoContext dbContext)
        {
            if (partyId > 0)
            {
                await dbContext.Character.Where(u => u.Id == playerId)
                    .ExecuteUpdateAsync(s =>
                        s.SetProperty(p => p.PartyId, partyId)
                    );
            }
            else
            {
                await dbContext.Character.Where(u => u.Id == playerId)
                    .ExecuteUpdateAsync(s =>
                        s.SetProperty(p => p.PartyId, (int?)null)
                    );
            }
        }
    }
}