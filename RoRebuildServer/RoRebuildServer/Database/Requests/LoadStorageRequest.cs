using RoRebuildServer.EntityComponents.Items;

namespace RoRebuildServer.Database.Requests
{
    public class StorageLoadRequest : IDbRequest
    {
        public bool IsComplete;
        public Guid PlayerId;
        public CharacterBag? StorageBag;

        public StorageLoadRequest(Guid playerId)
        {
            PlayerId = playerId;
            IsComplete = false;
            StorageBag = null;
        }

        public Task ExecuteAsync(RoContext dbContext)
        {
            throw new NotImplementedException();
        }
    }
}
