using RoRebuildServer.Networking;

namespace RoRebuildServer.Database.Requests
{
    public class DeleteCharacterRequest : IDbRequest
    {
        private NetworkConnection connection;
        private string deleteName;
        private int slotId;

        public DeleteCharacterRequest(NetworkConnection connection, string deleteName, int slotId)
        {
            this.connection = connection;
            this.deleteName = deleteName;
            this.slotId = slotId;
        }

        public Task ExecuteAsync(RoContext dbContext)
        {
            throw new NotImplementedException();
        }
    }
}